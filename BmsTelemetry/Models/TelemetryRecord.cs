public class TelemetryRecord
{
    public string Ip { get; set; } = string.Empty;
    public string DeviceKey { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    public string DataKey { get; set; } = string.Empty;
    public string DataValue { get; set; } = string.Empty;

    public DateTime Timestamp { get; set; }
}
