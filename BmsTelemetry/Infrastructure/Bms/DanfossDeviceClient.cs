public sealed class DanfossDeviceClient : BaseDeviceClient
{
    public DanfossDeviceClient(IBmsTransport transport) : base(transport)
    { }

    protected override async Task RunAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var request = new HttpRequestMessage(HttpMethod.Get, "/status");

            var response = await _transport.SendAsync(request, ct);

            // parse / update state here
        }
    }
}
