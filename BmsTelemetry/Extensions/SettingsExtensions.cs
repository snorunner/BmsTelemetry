public static class SettingsExtensions
{
    public static void AddAppSettings(this IServiceCollection services, IConfiguration config, IHostEnvironment env)
    {
        // Ensure appsettings.json exists (your custom logic)
        var configPath = Path.Combine(env.ContentRootPath, "appsettings.json");
        // SettingsConfigurator.EnsureConfig(configPath);

        // Bind strongly-typed settings
        services.Configure<AzureSettings>(config.GetSection("AzureSettings"));
        services.Configure<GeneralSettings>(config.GetSection("GeneralSettings"));
        services.Configure<NetworkSettings>(config.GetSection("NetworkSettings"));
        services.Configure<LoggingSettings>(config.GetSection("LoggingSettings"));
    }
}
