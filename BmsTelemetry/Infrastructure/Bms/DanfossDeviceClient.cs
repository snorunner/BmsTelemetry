using System.Text.Json.Nodes;
using System.Text.Json;
using System.Runtime.CompilerServices;

public sealed class DanfossDeviceClient : IBmsClient
{
    private readonly DanfossProtocol _protocol;
    private readonly ILogger<DanfossDeviceClient> _logger;
    private readonly IBmsTransport _transport;
    private readonly IIotDevice _iotDevice;

    public DanfossDeviceClient(IBmsTransport transport, ILoggerFactory loggerFactory, IIotDevice iotDevice)
    {
        _logger = loggerFactory.CreateLogger<DanfossDeviceClient>();
        _protocol = new DanfossProtocol(transport, loggerFactory);
        _iotDevice = iotDevice;
        _transport = transport;
    }

    public async IAsyncEnumerable<ClientCommand> GetPollingSequenceAsync([EnumeratorCancellation] CancellationToken ct)
    {
        yield return new ClientCommand(
            "ReadInputsAsync",
            async ct2 => await ReadInputsAsync(ct2)
        );

        yield return new ClientCommand(
            "ReadRelaysAsync",
            async ct2 => await ReadRelaysAsync(ct2)
        );

        yield return new ClientCommand(
            "ReadSensorsAsync",
            async ct2 => await ReadSensorsAsync(ct2)
        );

        yield return new ClientCommand(
            "ReadVarOutsAsync",
            async ct2 => await ReadVarOutsAsync(ct2)
        );
    }

    // Interaction methods

    private async Task<JsonNode?> ReadSensorsAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_sensors", null, ct);
        if (response is null)
            return null;

        return InjectedNodetypeParse("2", "sensor", response);
    }

    private async Task<JsonNode?> ReadRelaysAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_relays", null, ct);
        if (response is null)
            return null;

        return InjectedNodetypeParse("1", "relay", response);
    }

    private async Task<JsonNode?> ReadInputsAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_inputs", null, ct);
        if (response is null)
            return null;

        return InjectedNodetypeParse("0", "input", response);
    }

    private async Task<JsonNode?> ReadVarOutsAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_var_outs", null, ct);
        if (response is null)
            return null;

        return InjectedNodetypeParse("3", "var_output", response);
    }

    // Helper methods

    private JsonObject InjectedNodetypeParse(string nodeType, string subKey, JsonNode data)
    {
        var jsonResponse = data["resp"]?.AsObject() ?? new JsonObject();
        var subarr = jsonResponse[subKey] as JsonArray ?? new JsonArray();

        var root = new JsonObject();
        var dataArray = new JsonArray();

        foreach (var entryNode in subarr)
        {
            if (entryNode is not JsonObject entry)
                continue;

            var node = entry["node"]?.GetValue<string>() ?? "?";
            var mod = entry["mod"]?.GetValue<string>() ?? "?";
            var point = entry["point"]?.GetValue<string>() ?? "?";

            var normalized = NormalizerService.Normalize(entry);

            var obj = new JsonObject
            {
                ["device_key"] = $"nt{nodeType}:n{node}:m{mod}:p{point}",
                ["data"] = normalized.DeepClone()
            };

            dataArray.Add(obj);
        }

        root["data"] = dataArray;
        return root;
    }

    private void ConsolePrettyPrint(JsonNode obj)
    {
        try
        {
            var pretty = obj.ToJsonString(new JsonSerializerOptions
            {
                WriteIndented = true
            });

            Console.WriteLine("=== JSON ===");
            Console.WriteLine(pretty);
            Console.WriteLine("============");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed to pretty print JSON: {ex.Message}");
        }
    }

    // private Task<JsonNode?> ReadHvacUnitAsync(string ahindex, CancellationToken ct)
    // {
    //     return _protocol.SendCommandAsync("read_hvac_unit", new Dictionary<string, string>() { ["ahindex"] = ahindex }, ct);
    // }

}
