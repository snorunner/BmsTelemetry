using System.Text;
using System.Text.Json.Nodes;

public class E2Protocol
{
    private readonly IBmsTransport _transport;
    private readonly ILogger _logger;

    public E2Protocol(IBmsTransport transport, ILoggerFactory loggerFactory)
    {
        _transport = transport;
        _logger = loggerFactory.CreateLogger<E2Protocol>();
    }

    // MAIN ENTRY POINT
    public async Task<JsonNode?> SendCommandAsync(
        string method,
        JsonArray? parameters,
        CancellationToken ct)
    {
        var request = BuildRequest(method, parameters);


        var response = await _transport.SendAsync(request, ct, method);

        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        return await TranslateAsync(response);
    }

    // REQUEST BUILDER
    private HttpRequestMessage BuildRequest(string method, JsonArray? parameters)
    {
        var payload = new JsonObject
        {
            ["id"] = "0",
            ["method"] = method,
        };

        if (parameters is not null)
            payload["params"] = parameters;

        var request = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = new StringContent(
                payload.ToJsonString(),
                Encoding.UTF8,
                "application/json")
        };

        request.Headers.ConnectionClose = true;

        return request;
    }

    // TRANSLATION (same as old BaseDeviceOperation)
    private async Task<JsonNode?> TranslateAsync(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        try
        {
            return JsonNode.Parse(json);
        }
        catch
        {
            return null;
        }
    }
}
