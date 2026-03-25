public record NetworkSettings
{
    public List<DeviceSettings> bms_devices { get; init; } = new();
}
