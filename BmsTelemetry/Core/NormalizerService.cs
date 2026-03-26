using System.Text.Json.Nodes;

public sealed class NormalizerService
{
    public JsonObject Normalize(
        string deviceIp,
        string deviceType,
        string dataAddress,
        JsonObject? rawData)
    {
        var flat = new Dictionary<string, object?>();

        if (rawData is not null)
            FlattenJson(rawData, flat, prefix: "");

        var dataObject = new JsonObject();

        foreach (var kvp in flat)
        {
            dataObject[kvp.Key] = kvp.Value switch
            {
                null => null,
                string s => JsonValue.Create(s),
                bool b => JsonValue.Create(b),
                int i => JsonValue.Create(i),
                long l => JsonValue.Create(l),
                double d => JsonValue.Create(d),
                _ => JsonValue.Create(kvp.Value?.ToString())
            };
        }

        return new JsonObject
        {
            ["device_key"] = $"{deviceType}:{dataAddress}",
            ["ip"] = deviceIp,
            ["data"] = dataObject
        };
    }

    private void FlattenJson(JsonNode node, Dictionary<string, object?> output, string prefix)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var kvp in obj)
                {
                    var key = string.IsNullOrEmpty(prefix)
                        ? kvp.Key
                        : $"{prefix}__{kvp.Key}";

                    if (kvp.Value is null)
                    {
                        output[key] = null;
                    }
                    else
                    {
                        FlattenJson(kvp.Value, output, key);
                    }
                }
                break;

            case JsonArray arr:
                for (int i = 0; i < arr.Count; i++)
                {
                    var key = $"{prefix}[{i}]";
                    FlattenJson(arr[i]!, output, key);
                }
                break;

            case JsonValue val:
                output[prefix] = val.GetValue<object?>();
                break;
        }
    }
}
