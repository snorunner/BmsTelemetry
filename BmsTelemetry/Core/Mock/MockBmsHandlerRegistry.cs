public class MockBmsHandlerRegistry : IBmsHandlerRegistry
{
    private Dictionary<string, IBmsHandler> bmsHandlers = new();

    public MockBmsHandlerRegistry()
    {
        bmsHandlers.Add("123.456.789", new MockBmsHandler("123.456.789"));
        bmsHandlers.Add("420.666.67", new MockBmsHandler("420.666.67"));
    }

    public void RegisterDevice(IBmsHandler handler)
    {
        bmsHandlers.Add(handler.DeviceIP, handler);
    }

    public IBmsHandler? GetBmsHandler(string deviceIP)
    {
        return bmsHandlers.GetValueOrDefault(deviceIP);
    }

    public IReadOnlyCollection<IBmsHandler> GetHandlers()
    {
        return bmsHandlers.Values;
    }
}
