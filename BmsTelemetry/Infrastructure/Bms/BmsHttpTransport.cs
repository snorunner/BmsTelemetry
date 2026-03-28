using MihaZupan;

public class BmsHttpTransport : IBmsTransport
{
    private readonly Uri _endpoint;
    private readonly GeneralSettings _generalSettings;
    private readonly ILogger<BmsHttpTransport> _logger;

    private HttpClient? _httpClient;
    private readonly SemaphoreSlim _lock = new(1, 1);

    public BmsHttpTransport(
        Uri endpoint,
        GeneralSettings generalSettings,
        ILoggerFactory loggerFactory)
    {
        _endpoint = endpoint;
        _generalSettings = generalSettings;
        _logger = loggerFactory.CreateLogger<BmsHttpTransport>();
    }

    public async Task<HttpResponseMessage?> SendAsync(
        HttpRequestMessage request,
        CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct);
        request.RequestUri = _endpoint;
        _logger.LogDebug($"Sending request to {request.RequestUri}");

        try
        {
            BuildClientIfNeeded();

            int attempt = 0;

            while (true)
            {
                HttpRequestMessage clonedRequest = await CloneHttpRequestMessageAsync(request);

                try
                {
                    attempt++;

                    var response = await _httpClient!.SendAsync(clonedRequest, ct);

                    return response;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception) when (attempt < _generalSettings.http_retry_count)
                {
                    // retry
                }
                finally
                {
                    try
                    {
                        await Task.Delay(
                            TimeSpan.FromSeconds(_generalSettings.http_request_delay_seconds),
                            ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // swallow delay cancellation so Ctrl+C is clean
                    }
                }
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        // Copy headers
        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        // Copy content (if any)
        if (request.Content != null)
        {
            var ms = new MemoryStream();
            await request.Content.CopyToAsync(ms);
            ms.Position = 0;

            clone.Content = new StreamContent(ms);

            foreach (var header in request.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        clone.Version = request.Version;

        return clone;
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
            PooledConnectionLifetime = _generalSettings.keep_alive
                ? TimeSpan.FromMinutes(10)
                : TimeSpan.Zero,
            PooledConnectionIdleTimeout = TimeSpan.Zero
        };
    }
}
