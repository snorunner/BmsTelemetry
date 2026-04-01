using System.Text.Json.Nodes;

public sealed class DanfossDeviceClient : BaseDeviceClient
{
    private readonly DanfossProtocol _protocol;
    private readonly ILogger<DanfossDeviceClient> _logger;
    private readonly IIotDevice _iotDevice;

    // Data
    private JsonNode? _sensors;

    public DanfossDeviceClient(IBmsTransport transport, ILoggerFactory loggerFactory, IIotDevice iotDevice) : base(transport)
    {
        _logger = loggerFactory.CreateLogger<DanfossDeviceClient>();
        _protocol = new DanfossProtocol(transport, loggerFactory);
        _iotDevice = iotDevice;
    }

    protected override async Task RunAsync(CancellationToken ct)
    {
        // initialize
        // _sensors = await ReadSensorsAsync(ct);

        while (!ct.IsCancellationRequested)
        {
            // poll
            _sensors = await ReadSensorsAsync(ct);
            await Task.Delay(10_000, ct);
        }
        //
        // need to add a worker to just do it and then print to console!
        //
        // todo: implement send to azure (not yet)
    }

    // Helpers

    private async Task<JsonNode?> ExecuteWithTracking(
        Func<Task<JsonNode?>> action)
    {
        try
        {
            var result = await action();

            if (result == null)
            {
                RegisterFailure();
                return null;
            }

            RegisterSuccess();
            return result;
        }
        catch (OperationCanceledException)
        {
            throw; // let cancellation propagate
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Request failed");
            RegisterFailure();
            return null;
        }
    }

    // Interaction methods

    private Task<JsonNode?> ReadSensorsAsync(CancellationToken ct)
    {
        return ExecuteWithTracking(() =>
            _protocol.SendCommandAsync("read_sensors", null, ct));
    }

    private Task<JsonNode?> ReadHvacUnitAsync(string ahindex, CancellationToken ct)
    {
        return ExecuteWithTracking(() =>
            _protocol.SendCommandAsync("read_hvac_unit", new Dictionary<string, string>() { ["ahindex"] = ahindex }, ct));
    }

}
