public class BmsHandler : IBmsHandler
{
    public string DeviceIP { get; init; }
    public BmsType DeviceType { get; init; }
    public ConnectionStatus Connection { get; set; }
    public BmsHandlerStatus Status { get; set; }
    public DateTime LastSuccess { get; set; }
    public DateTime LastFailure { get; set; }
    public int ConsecutiveFailures { get; set; }

    private readonly IBmsClient _bmsClient;

    public BmsHandler(DeviceSettings deviceSettings, IBmsClient bmsClient)
    {
        DeviceIP = deviceSettings.IP;
        DeviceType = deviceSettings.device_type;
        Connection = ConnectionStatus.Unknown;
        Status = BmsHandlerStatus.Idle;
        LastSuccess = DateTime.MinValue;
        LastFailure = DateTime.MinValue;
        ConsecutiveFailures = 0;

        _bmsClient = bmsClient;

        if (_bmsClient is BaseDeviceClient client)
        {
            client.OnStatusChanged += update =>
            {
                Connection = update.Connection;
                LastSuccess = update.LastSuccess;
                LastFailure = update.LastFailure;
                ConsecutiveFailures = update.ConsecutiveFailures;
            };
        }
    }

    public async Task StartAsync(CancellationToken ct = default)
    {
        await _bmsClient.StartAsync(ct);
        Status = BmsHandlerStatus.Polling;
    }

    public async Task StopAsync()
    {
        await _bmsClient.StopAsync();
        Status = BmsHandlerStatus.Stopped;
    }
}
