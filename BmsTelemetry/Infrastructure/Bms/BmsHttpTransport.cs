using MihaZupan;

public class BmsHttpTransport : IBmsTransport
{
    private readonly Uri _endpoint;
    private readonly GeneralSettings _generalSettings;

    private HttpClient? _httpClient;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BmsHttpTransport(
        Uri endpoint,
        GeneralSettings generalSettings)
    {
        _endpoint = endpoint;
        _generalSettings = generalSettings;
    }

    public async Task<HttpResponseMessage?> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);

        try
        {
            BuildClientIfNeeded();

            var response = await _httpClient!.SendAsync(request, ct);

            await Task.Delay(
                TimeSpan.FromSeconds(_generalSettings.http_request_delay_seconds),
                ct);

            return response;
        }
        finally
        {
            _lock.Release();
        }
    }

    private void BuildClientIfNeeded()
    {
        if (_generalSettings.keep_alive && _httpClient is not null)
            return;

        _httpClient?.Dispose();

        var handler = CreateHandler();

        var client = new HttpClient(handler)
        {
            Timeout = TimeSpan.FromSeconds(
                _generalSettings.http_timeout_delay_seconds),
            BaseAddress = _endpoint
        };

        if (!_generalSettings.keep_alive)
        {
            client.DefaultRequestHeaders.ConnectionClose = true;
        }

        _httpClient = client;
    }

    private HttpMessageHandler CreateHandler()
    {
        if (OperatingSystem.IsLinux())
        {
            return new HttpClientHandler
            {
                Proxy = new HttpToSocks5Proxy("127.0.0.1", 1080),
                UseProxy = true
            };
        }

        return new SocketsHttpHandler
        {
            ConnectTimeout = TimeSpan.FromSeconds(
                _generalSettings.http_timeout_delay_seconds),
            PooledConnectionLifetime = _generalSettings.keep_alive
                ? TimeSpan.FromMinutes(10)
                : TimeSpan.Zero,
            PooledConnectionIdleTimeout = TimeSpan.Zero
        };
    }
}
