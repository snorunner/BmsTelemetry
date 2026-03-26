using Microsoft.Extensions.Options;

public class BmsHandlerRegistry : IBmsHandlerRegistry
{
    private Dictionary<string, IBmsHandler> bmsHandlers = new();

    public BmsHandlerRegistry(IOptions<NetworkSettings> networkSettings, IOptions<GeneralSettings> generalSettings)
    {
        var netsettings = networkSettings.Value.bms_devices;
        var gensettings = generalSettings.Value;

        foreach (var entry in netsettings)
        {
            var handler = IBmsHandlerFactory.Create(entry, gensettings);
            RegisterDevice(handler);
        }
    }

    public void RegisterDevice(IBmsHandler handler)
    {
        bmsHandlers.TryAdd(handler.DeviceIP, handler);
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
