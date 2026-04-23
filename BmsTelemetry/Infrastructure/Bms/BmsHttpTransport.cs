using MihaZupan;

public class BmsHttpTransport : IBmsTransport
{
    public Uri _endpoint { get; init; }
    private readonly GeneralSettings _generalSettings;
    private readonly ILogger<BmsHttpTransport> _logger;

    private HttpClient? _httpClient;

    // Only used for rate limiting timing
    private readonly SemaphoreSlim _rateLock = new(1, 1);
    private DateTime _lastRequestTime = DateTime.MinValue;

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
        BuildClientIfNeeded();

        request.RequestUri = _endpoint;

        var requestName = context is not null
            ? $"request `{context}`"
            : "request";

        int maxRetries = _generalSettings.http_retry_count;
        int attempt = 0;

        while (true)
        {
            await WaitForRateLimitAsync(ct);

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

                _logger.LogDebug("Successfully received {requestName}", requestName);

                return response; // ✅ RETURN IMMEDIATELY (no delay after success)
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex) when (attempt < maxRetries)
            {
                LogRetry(ex, requestName, attempt, maxRetries);
                await DelayWithBackoffAsync(attempt, ct);
            }
            catch (Exception ex) when (attempt < maxRetries)
            {
                LogRetry(ex, requestName, attempt, maxRetries);
                await DelayWithBackoffAsync(attempt, ct);
            }
            catch (HttpRequestException ex)
            {
                LogFinalFailure(ex, requestName);
                return null;
            }
            catch (Exception ex)
            {
                LogFinalFailure(ex, requestName);
                return null;
            }
        }
    }

    // -------------------------
    // Rate Limiting (FIXED)
    // -------------------------
    private async Task WaitForRateLimitAsync(CancellationToken ct)
    {
        var minDelay = TimeSpan.FromSeconds(_generalSettings.http_request_delay_seconds);

        await _rateLock.WaitAsync(ct);

        try
        {
            var now = DateTime.UtcNow;
            var elapsed = now - _lastRequestTime;

            if (elapsed < minDelay)
            {
                var delay = minDelay - elapsed;
                await Task.Delay(delay, ct);
            }

            _lastRequestTime = DateTime.UtcNow;
        }
        finally
        {
            _rateLock.Release();
        }
    }

    // -------------------------
    // Retry Backoff (ONLY on failure)
    // -------------------------
    private async Task DelayWithBackoffAsync(int attempt, CancellationToken ct)
    {
        try
        {
            var baseDelaySeconds = _generalSettings.http_request_delay_seconds;

            var backoff = Math.Pow(2, attempt - 1);
            var jitterMs = Random.Shared.Next(0, 1000);

            var delay = TimeSpan.FromSeconds(baseDelaySeconds * backoff)
                        + TimeSpan.FromMilliseconds(jitterMs);

            if (delay > TimeSpan.FromSeconds(30))
                delay = TimeSpan.FromSeconds(30);

            await Task.Delay(delay, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // swallow for graceful shutdown
        }
    }

    // -------------------------
    // Logging helpers
    // -------------------------
    private void LogRetry(Exception ex, string requestName, int attempt, int maxRetries)
    {
        if (ex is HttpRequestException httpEx)
        {
            var statusCode = httpEx.StatusCode?.ToString() ?? "no-status";

            _logger.LogWarning(ex,
                "{Request} failed with status {StatusCode}. Retrying ({Attempt}/{MaxRetries})",
                requestName,
                statusCode,
                attempt,
                maxRetries);
        }
        else
        {
            _logger.LogWarning(ex,
                "{Request} failed with unexpected error. Retrying ({Attempt}/{MaxRetries})",
                requestName,
                attempt,
                maxRetries);
        }
    }

    private void LogFinalFailure(Exception ex, string requestName)
    {
        if (ex is HttpRequestException httpEx)
        {
            var statusCode = httpEx.StatusCode?.ToString() ?? "no-status";

            _logger.LogError(ex,
                "{Request} failed permanently with status {StatusCode}",
                requestName,
                statusCode);
        }
        else
        {
            _logger.LogError(ex,
                "{Request} failed permanently with unexpected error",
                requestName);
        }
    }

    // -------------------------
    // HttpClient lifecycle
    // -------------------------
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

    // -------------------------
    // Request cloning
    // -------------------------
    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage request)
    {
        var clone = new HttpRequestMessage(request.Method, request.RequestUri);

        foreach (var header in request.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

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
}
