public interface IBmsTransport
{
    Task<HttpResponseMessage?> SendAsync(HttpRequestMessage request, CancellationToken ct);
}
