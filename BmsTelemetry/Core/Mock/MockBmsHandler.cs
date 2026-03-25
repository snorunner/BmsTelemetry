public class MockBmsHandler : IBmsHandler
{
    public string DeviceIP { get; init; }
    public BmsType DeviceType { get; init; }
    public ConnectionStatus Connection { get; init; }
    public BmsHandlerStatus Status { get; init; }

    public MockBmsHandler(string deviceIP)
    {
        DeviceIP = deviceIP;
        Connection = ConnectionStatus.Unknown;
    }

    public void Start()
    { }

    public void Stop()
    { }
}
