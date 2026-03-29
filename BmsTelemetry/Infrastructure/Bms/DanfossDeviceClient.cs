using System.Text.Json.Nodes;
using System.Text.Json;
using System.Runtime.CompilerServices;

public sealed class DanfossDeviceClient : IBmsClient
{
    private readonly DanfossProtocol _protocol;
    private readonly ILogger<DanfossDeviceClient> _logger;
    private readonly IBmsTransport _transport;
    private readonly DbReader _dbReader;
    private readonly string _ip;
    private DateTime _lastInitTime = DateTime.MinValue;

    public DanfossDeviceClient(IBmsTransport transport, string deviceIP, DbReader dbReader, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<DanfossDeviceClient>();
        _protocol = new DanfossProtocol(transport, loggerFactory);
        _transport = transport;
        _dbReader = dbReader;
        _ip = deviceIP;
    }

    public async IAsyncEnumerable<ClientCommand> GetPollingSequenceAsync(
        [EnumeratorCancellation] CancellationToken ct)
    {
        // if (DateTime.UtcNow - _lastInitTime >= TimeSpan.FromMinutes(60))
        // {
        //     foreach (var cmd in GetInitCommands())
        //     {
        //         yield return cmd;
        //     }
        //
        //     _lastInitTime = DateTime.UtcNow;
        // }
        //
        // foreach (var cmd in GetContinuousCommands())
        // {
        //     yield return cmd;
        // }

        yield return new ClientCommand(
            "ReadHvacUnitAsync",
            ct => ReadHvacUnitAsync(ct)
        );
    }

    private IEnumerable<ClientCommand> GetInitCommands()
    {
        yield return new ClientCommand(
            "ReadInputsAsync",
            ct => ReadInputsAsync(ct)
        );

        yield return new ClientCommand(
            "ReadRelaysAsync",
            ct => ReadRelaysAsync(ct)
        );

        yield return new ClientCommand(
            "ReadSensorsAsync",
            ct => ReadSensorsAsync(ct)
        );

        yield return new ClientCommand(
            "ReadVarOutsAsync",
            ct => ReadVarOutsAsync(ct)
        );

        yield return new ClientCommand(
            "ReadUnitsAsync",
            ct => ReadUnitsAsync(ct)
        );
    }

    private IEnumerable<ClientCommand> GetContinuousCommands()
    {
        yield return new ClientCommand(
            "AlarmSummaryAsync",
            ct => AlarmSummaryAsync(ct)
        );

        yield return new ClientCommand(
            "ReadDevicesAsync",
            ct => ReadDevicesAsync(ct)
        );

        yield return new ClientCommand(
            "ReadLightingAsync",
            ct => ReadLightingAsync(ct)
        );

        yield return new ClientCommand(
            "ReadHvacAsync",
            ct => ReadHvacAsync(ct)
        );

        yield return new ClientCommand(
            "ReadHvacUnitAsync",
            ct => ReadHvacUnitAsync(ct)
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

    private async Task<JsonNode?> AlarmSummaryAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("alarm_summary", null, ct);
        if (response is null)
            return null;

        return BareParse("alarm_summary", response);
    }

    private async Task<JsonNode?> ReadDevicesAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_devices", null, ct);
        if (response is null)
            return null;

        return AtParse("device", response);
    }

    private async Task<JsonNode?> ReadLightingAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_lighting", null, ct);
        if (response is null)
            return null;

        return AtParse("device", response);
    }

    private async Task<JsonNode?> ReadUnitsAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_units", null, ct);
        if (response is null)
            return null;

        return BareParse("controller", response);
    }

    private async Task<JsonNode?> ReadHvacAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_hvac", null, ct);
        if (response is null)
            return null;

        return AtParse("device", response);
    }

    private async Task<JsonNode?> ReadHvacUnitAsync(CancellationToken ct)
    {
        var atContainer = new JsonObject();
        var arrContainer = new JsonObject();
        var jsonArr = new JsonArray();

        string methodName = "ReadHvacUnitAsync";

        var jsonRows = await _dbReader.GetHotRowsAsJsonAsync(ip: _ip, source: "ReadHvacAsync", ct);

        for (var i = 1; i < jsonRows.Count + 1; i++)
        {
            _logger.LogInformation("Executing {methodName} step {i} of {jsonRows.Count}", methodName, i, jsonRows.Count);

            var response = await _protocol.SendCommandAsync(
                "read_hvac_unit",
                new Dictionary<string, string>() { ["ahindex"] = i.ToString() },
                ct
            );

            if (response is null)
                continue;

            var jsonData = response["resp"]?.AsObject() ?? new JsonObject();
            jsonArr.Add(jsonData.DeepClone());
        }

        arrContainer["arrkey"] = jsonArr;
        atContainer["resp"] = arrContainer;

        var final = AtParse("arrkey", atContainer);

        return final;
    }

    // private Task<JsonNode?> ReadHvacUnitAsync(string ahindex, CancellationToken ct)
    // {
    //     return _protocol.SendCommandAsync("read_hvac_unit", new Dictionary<string, string>() { ["ahindex"] = ahindex }, ct);
    // }

    // Helper methods
    private JsonObject BareParse(string deviceKey, JsonNode data)
    {
        var jsonResponse = data["resp"]?.AsObject() ?? new JsonObject();
        var normalized = NormalizerService.Normalize(jsonResponse);

        var obj = new JsonObject
        {
            ["device_key"] = deviceKey,
            ["data"] = normalized.DeepClone()
        };

        var root = new JsonObject();
        root["data"] = new JsonArray(obj);
        return root;
    }

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

    private JsonObject AtParse(string subKey, JsonNode data)
    {
        var jsonResponse = data["resp"]?.AsObject() ?? new JsonObject();
        var subarr = jsonResponse[subKey] as JsonArray ?? new JsonArray();

        var root = new JsonObject();
        var dataArray = new JsonArray();

        foreach (var entryNode in subarr)
        {
            if (entryNode is not JsonObject entry)
                continue;

            var nodeType = entry["@nodetype"]?.GetValue<string>() ?? "?";
            var node = entry["@node"]?.GetValue<string>() ?? "?";
            var mod = entry["@mod"]?.GetValue<string>() ?? "?";
            var point = entry["@point"]?.GetValue<string>() ?? "?";

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

}
