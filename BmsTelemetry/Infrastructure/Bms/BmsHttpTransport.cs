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
        CancellationToken ct = default,
        string? context = null)
    {
        await _lock.WaitAsync(ct);

        try
        {
            BuildClientIfNeeded();

            request.RequestUri = _endpoint;

            var requestName = context is not null
                ? $"request `{context}`"
                : "request";

            int maxRetries = _generalSettings.http_retry_count;
            int attempt = 0;

            var baseDelaySeconds = _generalSettings.http_request_delay_seconds;

            while (true)
            {
                HttpRequestMessage clonedRequest = await CloneHttpRequestMessageAsync(request);

                _logger.LogDebug(
                    "Sending {Request} to {Uri} (attempt {Attempt})",
                    requestName,
                    clonedRequest.RequestUri,
                    attempt + 1);

                try
                {
                    attempt++;

                    var response = await _httpClient!.SendAsync(clonedRequest, ct);

                    if (!response.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException(
                            $"Request failed with status code {(int)response.StatusCode}",
                            null,
                            response.StatusCode);
                    }

                    return response;
                }
                catch (OperationCanceledException) when (ct.IsCancellationRequested)
                {
                    // graceful shutdown (Ctrl+C etc.)
                    throw;
                }
                catch (HttpRequestException ex) when (attempt < maxRetries)
                {
                    var statusCode = ex.StatusCode?.ToString() ?? "no-status";

                    _logger.LogWarning(ex,
                        "{Request} failed with status {StatusCode}. Retrying ({Attempt}/{MaxRetries})",
                        requestName,
                        statusCode,
                        attempt,
                        maxRetries);
                }
                catch (Exception ex) when (attempt < maxRetries)
                {
                    _logger.LogWarning(ex,
                        "{Request} failed with unexpected error. Retrying ({Attempt}/{MaxRetries})",
                        requestName,
                        attempt,
                        maxRetries);
                }
                catch (HttpRequestException ex)
                {
                    var statusCode = ex.StatusCode?.ToString() ?? "no-status";

                    _logger.LogError(ex,
                        "{Request} failed permanently with status {StatusCode}",
                        requestName,
                        statusCode);

                    return null;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "{Request} failed permanently with unexpected error",
                        requestName);

                    return null;
                }
                finally
                {
                    try
                    {
                        var backoff = Math.Pow(2, attempt - 1);

                        var jitterMs = Random.Shared.Next(0, 1000);

                        var delay = TimeSpan.FromSeconds(baseDelaySeconds * backoff)
                            + TimeSpan.FromMilliseconds(jitterMs);

                        await Task.Delay(delay, ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        // swallow cancellation during delay for clean shutdown
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
