public abstract class BaseDeviceClient : IBmsClient
{
    protected readonly IBmsTransport _transport;
    private CancellationTokenSource? _cts;

    private Task? _executionTask;

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
            await _executionTask;
    }

    public event Action<ClientStatusUpdate>? OnStatusChanged;

    protected void RaiseStatus(ClientStatusUpdate update)
    {
        OnStatusChanged?.Invoke(update);
    }

    protected abstract Task RunAsync(CancellationToken ct);
}
