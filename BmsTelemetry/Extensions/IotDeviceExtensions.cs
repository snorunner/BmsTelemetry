public static class IotDeviceExtensions
{
    public static void AddIotDevice(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        // Load settings directly
        var generalSettings = config.GetSection("GeneralSettings").Get<GeneralSettings>();

        // Development override
        if (env.IsDevelopment())
        {
            services.AddSingleton<IIotDevice, ConsoleIotDevice>();
            // services.AddSingleton<IIotDevice, FileIotDevice>();
            // services.AddSingleton<IIotDevice, VoidIotDevice>();
            // services.AddSingleton<IIotDevice, AzureIotDevice>();
            return;
        }

        // Production logic
        if (generalSettings?.use_cloud ?? true)
        {
            services.AddSingleton<IIotDevice, AzureIotDevice>();
        }
        else
        {
            services.AddSingleton<IIotDevice>(new FileIotDevice("localmessages.jsonl"));
        }
    }
}
