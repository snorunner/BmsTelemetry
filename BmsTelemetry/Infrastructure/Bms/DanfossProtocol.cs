using System.Text;
using System.Text.Json.Nodes;
using System.Xml;
using System.Xml.Linq;

public class DanfossProtocol
{
    private readonly IBmsTransport _transport;
    private readonly ILogger _logger;

    private readonly Dictionary<string, string> _requiredParams = new()
    {
        ["lang"] = "e",
        ["units"] = "U"
    };

    public DanfossProtocol(IBmsTransport transport, ILoggerFactory loggerFactory)
    {
        _transport = transport;
        _logger = loggerFactory.CreateLogger<DanfossProtocol>();
    }

    // EXISTING METHOD (unchanged behavior)
    public async Task<JsonNode?> SendCommandAsync(
        string action,
        IDictionary<string, string>? extraParams,
        CancellationToken ct)
    {
        var request = BuildRequest(action, extraParams);

        var response = await _transport.SendAsync(request, ct, action);

        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        return await TranslateAsync(response);
    }

    // NEW EXTENSIBLE METHOD
    public async Task<JsonNode?> SendCommandWithBodyAsync(
        string action,
        IDictionary<string, string>? extraParams,
        Action<XElement> modifyXml,
        CancellationToken ct)
    {
        var request = BuildRequest(action, extraParams, modifyXml);

        var response = await _transport.SendAsync(request, ct, action);

        if (response is null || !response.IsSuccessStatusCode)
        {
            return null;
        }

        return await TranslateAsync(response);
    }

    // CENTRALIZED REQUEST BUILDER WITH HOOK
    private HttpRequestMessage BuildRequest(
        string action,
        IDictionary<string, string>? extraParams,
        Action<XElement>? modifyXml = null)
    {
        var attributes = new List<XAttribute>();

        // Required params
        attributes.AddRange(_requiredParams.Select(kv => new XAttribute(kv.Key, kv.Value)));

        // Action
        attributes.Add(new XAttribute("action", action));

        // Extra params
        if (extraParams is not null)
        {
            attributes.AddRange(extraParams.Select(kv => new XAttribute(kv.Key, kv.Value)));
        }

        // Root element
        var element = new XElement("cmd", attributes);

        // EXTENSION POINT
        modifyXml?.Invoke(element);

        var xml = element.ToString(SaveOptions.DisableFormatting);

        var request = new HttpRequestMessage(HttpMethod.Post, "")
        {
            Content = new StringContent(xml, Encoding.UTF8, "application/xml")
        };

        request.Headers.ConnectionClose = true;

        return request;
    }

    private async Task<JsonNode?> TranslateAsync(HttpResponseMessage response)
    {
        var xmlString = await response.Content.ReadAsStringAsync();

        var xmlDoc = new XmlDocument();
        xmlDoc.LoadXml(xmlString);

        var jsonText = Newtonsoft.Json.JsonConvert.SerializeXmlNode(xmlDoc);

        return JsonNode.Parse(jsonText);
    }
}
