public class BmsHandler : IBmsHandler
{
    private readonly object _stateLock = new();
    private readonly ILogger<BmsHandler> _logger;

    public string DeviceIP { get; init; }
    public BmsType DeviceType { get; init; }

    public ConnectionStatus Connection { get; private set; } = ConnectionStatus.Unknown;
    public BmsHandlerStatus Status { get; private set; } = BmsHandlerStatus.Idle;

    public int ConsecutiveFailures { get; private set; }
    public DateTime LastSuccess { get; private set; } = DateTime.MinValue;
    public DateTime LastFailure { get; private set; } = DateTime.MinValue;

    private readonly IBmsClient _bmsClient;
    private readonly GeneralSettings _generalSettings;

    public BmsHandler(DeviceSettings deviceSettings, GeneralSettings generalSettings, IBmsClient bmsClient, ILoggerFactory loggerFactory)
    {
        DeviceIP = deviceSettings.IP;
        DeviceType = deviceSettings.device_type;
        _bmsClient = bmsClient;
        _generalSettings = generalSettings;
        _logger = loggerFactory.CreateLogger<BmsHandler>();

        // Subscribe to client status updates
        if (_bmsClient is BaseDeviceClient client)
        {
            client.OnStatusChanged += UpdateStatus;
        }
    }

    private void UpdateStatus(ClientStatusUpdate update)
    {
        lock (_stateLock)
        {
            Connection = update.Connection;
            LastSuccess = update.LastSuccess;
            LastFailure = update.LastFailure;
            ConsecutiveFailures = update.ConsecutiveFailures;

            // Map client state to simplified handler state
            Status = update.Connection == ConnectionStatus.Connected
                ? BmsHandlerStatus.Polling
                : BmsHandlerStatus.Idle;
        }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        lock (_stateLock)
        {
            if (Status == BmsHandlerStatus.Polling)
                return; // already running
        }
        await _bmsClient.StartAsync(ct);
        lock (_stateLock)
        {
            Status = BmsHandlerStatus.Polling;
            ConsecutiveFailures = 0;
        }
    }

    public async Task StopAsync()
    {
        await _bmsClient.StopAsync();
        lock (_stateLock)
        {
            Status = BmsHandlerStatus.Stopped;
        }
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
                _ = StopAsync();
                return;
            }

            // Restart if stopped/idle and last failure was > 30 min ago
            if ((Status == BmsHandlerStatus.Stopped || Status == BmsHandlerStatus.Idle) &&
                (DateTime.UtcNow - LastFailure) > TimeSpan.FromMinutes(30))
            {
                _logger.LogInformation($"Starting {this.DeviceIP} after a cooldown period.");
                _ = StartAsync(ct);
            }
        }
    }
}
