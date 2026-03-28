public abstract class BaseDeviceClient : IBmsClient
{
    protected readonly IBmsTransport _transport;
    private CancellationTokenSource? _cts;

    private Task? _executionTask;

    public BaseDeviceClient(IBmsTransport transport)
    {
        _transport = transport;
    }

    public abstract IAsyncEnumerable<ClientCommand> GetPollingSequenceAsync(CancellationToken ct);
}
