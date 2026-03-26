public class BmsHandler : IBmsHandler
{
    public string DeviceIP { get; init; }
    public BmsType DeviceType { get; init; }
    public ConnectionStatus Connection { get; set; }
    public BmsHandlerStatus Status { get; set; }

    private readonly IBmsClient _bmsClient;

    public BmsHandler(DeviceSettings deviceSettings, IBmsClient bmsClient)
    {
        DeviceIP = deviceSettings.IP;
        DeviceType = deviceSettings.device_type;
        Connection = ConnectionStatus.Unknown;
        Status = BmsHandlerStatus.Idle;

        _bmsClient = bmsClient;
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _bmsClient.StartAsync(ct);
    }

    public async Task StopAsync()
    {
        await _bmsClient.StopAsync();
    }
}
