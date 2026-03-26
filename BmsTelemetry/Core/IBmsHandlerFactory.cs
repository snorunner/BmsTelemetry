public static class IBmsHandlerFactory
{
    public static IBmsHandler Create(DeviceSettings deviceSettings, GeneralSettings generalSettings)
    {
        var client = IBmsHandlerFactory.GetClientForSettings(deviceSettings, generalSettings);
        return new BmsHandler(deviceSettings, client);
    }

    private static IBmsClient GetClientForSettings(DeviceSettings deviceSettings, GeneralSettings generalSettings)
    {
        switch (deviceSettings.device_type)
        {
            case BmsType.EmersonE2:
                return new E2DeviceClient(
                    new BmsHttpTransport(
                        new Uri($"http://{deviceSettings.IP}:14106/JSON-RPC"),
                        generalSettings
                    )
                );
            case BmsType.Danfoss:
                return new DanfossDeviceClient(
                    new BmsHttpTransport(
                        new Uri($"http://{deviceSettings.IP}/http/xml.cgi"),
                        generalSettings
                    )
                );
            case BmsType.EmersonE3:
                return new E3DeviceClient(
                    new BmsHttpTransport(
                        new Uri($"http://{deviceSettings.IP}/cgi-bin/mgw.cgi"),
                        generalSettings
                    )
                );
            default:
                throw new NotImplementedException($"Device type {deviceSettings.device_type} is not implemented!");
        }
    }
}
