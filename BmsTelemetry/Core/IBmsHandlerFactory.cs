using Microsoft.Extensions.Options;

public class IBmsHandlerFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly GeneralSettings _generalSettings;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IIotDevice _iotDevice;

    public IBmsHandlerFactory(
        ILoggerFactory loggerFactory,
        IOptions<GeneralSettings> generalSettings,
        IIotDevice iotDevice,
        IServiceScopeFactory scopeFactory)
    {
        _loggerFactory = loggerFactory;
        _generalSettings = generalSettings.Value;
        _iotDevice = iotDevice;
        _scopeFactory = scopeFactory;
    }

    public IBmsHandler Create(DeviceSettings deviceSettings)
    {
        var client = GetClientForSettings(deviceSettings, _generalSettings);

        return new BmsHandler(deviceSettings, _generalSettings, client, _loggerFactory, _scopeFactory);
    }

    private IBmsClient GetClientForSettings(DeviceSettings deviceSettings, GeneralSettings generalSettings)
    {
        switch (deviceSettings.device_type)
        {
            // case BmsType.EmersonE2:
            //     return new E2DeviceClient(
            //         new BmsHttpTransport(
            //             new Uri($"http://{deviceSettings.IP}:14106/JSON-RPC"),
            //             generalSettings,
            //             _loggerFactory
            //         )
            //     );
            case BmsType.Danfoss:
                return new DanfossDeviceClient(
                    new BmsHttpTransport(
                        new Uri($"http://{deviceSettings.IP}/http/xml.cgi"),
                        generalSettings,
                        _loggerFactory
                    ),
                    _loggerFactory,
                    _iotDevice
                );
            // case BmsType.EmersonE3:
            //     return new E3DeviceClient(
            //         new BmsHttpTransport(
            //             new Uri($"http://{deviceSettings.IP}/cgi-bin/mgw.cgi"),
            //             generalSettings,
            //             _loggerFactory
            //         )
            //     );
            default:
                throw new NotImplementedException($"Device type {deviceSettings.device_type} is not implemented!");
        }
    }
}
