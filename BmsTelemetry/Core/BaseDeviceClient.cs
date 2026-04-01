public abstract class BaseDeviceClient : IBmsClient
{
    protected readonly IBmsTransport _transport;
    private CancellationTokenSource? _cts;

    private Task? _executionTask;

    protected int _consecutiveFailures;

    public BaseDeviceClient(IBmsTransport transport)
    {
        _transport = transport;
    }

    public Task StartAsync(CancellationToken ct)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _executionTask = RunAsync(_cts.Token);
        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        if (_cts == null)
            return;

        _cts.Cancel();

        if (_executionTask != null)
        {
            try
            {
                await _executionTask;
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
        }
    }

    public event Action<ClientStatusUpdate>? OnStatusChanged;

    protected void RaiseStatus(ClientStatusUpdate update)
    {
        OnStatusChanged?.Invoke(update);
    }

    protected abstract Task RunAsync(CancellationToken ct);

    protected void RegisterSuccess()
    {
        _consecutiveFailures = 0;

        RaiseStatus(new ClientStatusUpdate(
            ConnectionStatus.Connected,
            DateTime.UtcNow,
            DateTime.MinValue,
            _consecutiveFailures
        ));
    }

    protected void RegisterFailure()
    {
        _consecutiveFailures++;

        RaiseStatus(new ClientStatusUpdate(
            ConnectionStatus.Disconnected,
            DateTime.MinValue,
            DateTime.UtcNow,
            _consecutiveFailures
        ));
    }

}
