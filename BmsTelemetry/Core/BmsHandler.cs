using System.Threading.Channels;
using System.Text.Json.Nodes;
using EFCore.BulkExtensions;

public class BmsHandler : IBmsHandler
{
    // Internals
    private readonly IBmsClient _bmsClient;
    private readonly GeneralSettings _generalSettings;
    private readonly CancellationTokenSource _cts = new();
    private readonly object _stateLock = new();
    private readonly Channel<HandlerCommand> _queue = Channel.CreateUnbounded<HandlerCommand>();
    private readonly ILogger<BmsHandler> _logger;
    private readonly IServiceScopeFactory _scopeFactory;

    public Task LoopTask { get; }
    private IAsyncEnumerator<ClientCommand>? _pollEnumerator;

    // Identification info
    public string DeviceIP { get; init; }
    public BmsType DeviceType { get; init; }

    // State info
    public ConnectionStatus Connection { get; private set; } = ConnectionStatus.Disconnected;
    public BmsHandlerStatus Status { get; private set; } = BmsHandlerStatus.Stopped;
    public int ConsecutiveFailures { get; private set; } = 0;
    public DateTime LastSuccess { get; private set; } = DateTime.MinValue;
    public DateTime LastFailure { get; private set; } = DateTime.MinValue;

    public BmsHandler(
        DeviceSettings deviceSettings,
        GeneralSettings generalSettings,
        IBmsClient bmsClient,
        ILoggerFactory loggerFactory,
        IServiceScopeFactory scopeFactory)
    {
        DeviceIP = deviceSettings.IP;
        DeviceType = deviceSettings.device_type;
        _bmsClient = bmsClient;
        _generalSettings = generalSettings;
        _logger = loggerFactory.CreateLogger<BmsHandler>();
        _scopeFactory = scopeFactory;

        LoopTask = Task.Run(() => RunAsync(_cts.Token));
    }

    public ValueTask EnqueueStart()
    {
        Status = BmsHandlerStatus.Polling;
        return _queue.Writer.WriteAsync(HandlerCommand.Start());
    }

    public ValueTask EnqueueStop()
    {
        Status = BmsHandlerStatus.Stopped;
        return _queue.Writer.WriteAsync(HandlerCommand.Stop());
    }

    private async Task RunAsync(CancellationToken ct)
    {
        await foreach (var cmd in _queue.Reader.ReadAllAsync(ct))
        {
            switch (cmd.Type)
            {
                case HandlerCommandType.Start:
                    await ProcessStartAsync(ct);
                    break;

                case HandlerCommandType.Stop:
                    _cts.Cancel();
                    break;

                case HandlerCommandType.PollStep:
                    await ExecutePollStepAsync(cmd.ClientCmd!, ct);
                    break;
            }
        }
    }

    private async Task ProcessStartAsync(CancellationToken ct)
    {
        _pollEnumerator = _bmsClient.GetPollingSequenceAsync(ct).GetAsyncEnumerator(ct);

        await TryScheduleNextPollStep(ct);
    }

    private async Task TryScheduleNextPollStep(CancellationToken ct)
    {
        if (_queue.Reader.Count > 0)
            return;

        if (_pollEnumerator == null)
            return;

        if (await _pollEnumerator.MoveNextAsync())
        {
            await _queue.Writer.WriteAsync(HandlerCommand.Poll(_pollEnumerator.Current), ct);
        }
        else
        {
            await _pollEnumerator.DisposeAsync();

            _pollEnumerator = _bmsClient.GetPollingSequenceAsync(ct).GetAsyncEnumerator(ct);

            await TryScheduleNextPollStep(ct);
        }
    }

    private async Task ExecutePollStepAsync(ClientCommand cmd, CancellationToken ct)
    {
        _logger.LogInformation("Executing poll step {Step}", cmd.Name);

        JsonNode? json = null;

        try
        {
            json = await cmd.Action(ct);
        }
        catch
        {
            // let json stay null and fail in next step
        }

        if (json is null)
        {
            _logger.LogWarning("Poll step {Step} returned null", cmd.Name);
            ConsecutiveFailures++;
            LastFailure = DateTime.UtcNow;
            Connection = ConnectionStatus.Disconnected;
            await TryScheduleNextPollStep(ct);
            return;
        }

        ConsecutiveFailures = 0;
        Connection = ConnectionStatus.Connected;
        LastSuccess = DateTime.UtcNow;

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var dataArray = json["data"] as JsonArray;
        if (dataArray is null)
        {
            await TryScheduleNextPollStep(ct);
            return;
        }

        var incomingItems = new List<TelemetryRecord>();
        foreach (var item in dataArray)
        {
            var obj = item!.AsObject();
            var deviceKey = obj["device_key"]?.ToString() ?? "?";
            var dataObj = obj["data"]!.AsObject();

            foreach (var kvp in dataObj)
            {
                incomingItems.Add(new TelemetryRecord
                {
                    Ip = DeviceIP,
                    DeviceKey = $"{DeviceType}:{deviceKey}",
                    DataKey = kvp.Key,
                    DataValue = kvp.Value?.ToString() ?? "?",
                    Timestamp = DateTime.UtcNow,
                    Source = cmd.Name
                });
            }
        }

        if (!incomingItems.Any())
        {
            await TryScheduleNextPollStep(ct);
            return;
        }

        // BULK UPSERT
        await db.BulkInsertOrUpdateAsync(incomingItems, cancellationToken: ct);

        await TryScheduleNextPollStep(ct);
    }

    // Called by supervisor/background service to apply smart conditions
    public async Task EvaluateAsync(CancellationToken ct)
    {
        lock (_stateLock)
        {
            // Stop if too many consecutive failures
            if (ConsecutiveFailures >= 5 && Status != BmsHandlerStatus.Stopped)
            {
                _logger.LogWarning($"Stopping {this.DeviceIP} due to excessive failures.");
                EnqueueStop();
                return;
            }

            // Restart if stopped/idle and last failure was > 30 min ago
            if ((Status == BmsHandlerStatus.Stopped) &&
                (DateTime.UtcNow - LastFailure) > TimeSpan.FromMinutes(30))
            {
                _logger.LogInformation($"Starting {this.DeviceIP} after a cooldown period.");
                EnqueueStart();
            }
        }
    }
}
