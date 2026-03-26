using System.Text.Json.Nodes;

public class BmsSupervisor : BackgroundService
{
    private readonly IBmsHandlerRegistry _registry;
    private readonly IIotDevice _iotDevice;
    private readonly ILogger<BmsSupervisor> _logger;

    private DateTime lastHealthPayloadTime;

    public BmsSupervisor(IBmsHandlerRegistry registry, IIotDevice iotDevice, ILogger<BmsSupervisor> logger)
    {
        _registry = registry;
        _iotDevice = iotDevice;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var healthTelemetry = new JsonArray();

            foreach (var handler in _registry.GetHandlers())
            {
                await handler.EvaluateAsync(stoppingToken);

                healthTelemetry.Add(BuildHealthPayload(handler));
            }

            if (ShouldSend())
                await ReportTelemetry(healthTelemetry, stoppingToken);

            await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken); // check every 10s
        }
    }

    private async Task ReportTelemetry(JsonArray healthTelemetry, CancellationToken ct = default)
    {
        _logger.LogInformation("Sending health telemetry to IoTHub...");
        await _iotDevice.SendMessageAsync(healthTelemetry, ct);
        lastHealthPayloadTime = DateTime.UtcNow;
    }

    private JsonObject BuildHealthPayload(IBmsHandler handler)
    {
        var payload = new JsonObject();
        payload["ip"] = handler.DeviceIP;
        payload["device_key"] = $"{handler.DeviceType}:HealthTelemetry";

        var dataobj = new JsonObject();
        dataobj["LastSuccess"] = handler.LastSuccess.ToUniversalTime();
        dataobj["LastFailure"] = handler.LastFailure.ToUniversalTime();
        dataobj["ConsecutiveFailures"] = handler.ConsecutiveFailures;
        dataobj["Status"] = handler.Status.ToString();
        payload["data"] = dataobj;

        return payload;
    }

    private bool ShouldSend()
    {
        if (DateTime.UtcNow - lastHealthPayloadTime >= TimeSpan.FromMinutes(5))
            return true;
        return false;
    }
}
