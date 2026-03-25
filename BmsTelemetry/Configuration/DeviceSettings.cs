using System.Text.Json.Serialization;

public record DeviceSettings
{
    [JsonPropertyName("ip")]
    public string IP { get; init; } = string.Empty;

    // [JsonPropertyName("device_type")]
    public BmsType device_type { get; init; }
}
