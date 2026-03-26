using System.Text.Json.Nodes;

public class BmsSupervisor : BackgroundService
{
    private readonly IBmsHandlerRegistry _registry;
    private readonly IIotDevice _iotDevice;

    public BmsSupervisor(IBmsHandlerRegistry registry, IIotDevice iotDevice)
    {
        _registry = registry;
        _iotDevice = iotDevice;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            foreach (var handler in _registry.GetHandlers())
            {
                // Simple policy: restart failed clients
                // if (handler.Status == BmsHandlerStatus.Failed)
                // {
                //     await handler.StopAsync();
                //     await handler.StartAsync(stoppingToken);
                // }

                ReportTelemetry(stoppingToken);
            }

            await Task.Delay(30000, stoppingToken); // check every 30 seconds
        }
    }

    private void ReportTelemetry(CancellationToken ct = default)
    {
        var payload = new JsonObject { ["data"] = "success?" };
        _iotDevice.SendMessageAsync(payload, ct);
    }
}
