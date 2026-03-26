public sealed class E2DeviceClient : BaseDeviceClient
{
    public E2DeviceClient(IBmsTransport transport) : base(transport)
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
