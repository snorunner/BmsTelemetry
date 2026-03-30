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

        // Test new commands
        yield return new ClientCommand(
            "ReadMetersAsync",
            ct => ReadMetersAsync(ct)
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

        yield return new ClientCommand(
            "ReadDevicesAsync",
            ct => ReadDevicesAsync(ct)
        );

        yield return new ClientCommand(
            "ReadSuctionGroupAsync",
            ct => ReadSuctionGroupAsync(ct)
        );

        yield return new ClientCommand(
            "ReadCondenserAsync",
            ct => ReadCondenserAsync(ct)
        );

        yield return new ClientCommand(
            "ReadCircuitAsync",
            ct => ReadCircuitAsync(ct)
        );
    }

    private IEnumerable<ClientCommand> GetContinuousCommands()
    {
        yield return new ClientCommand(
            "AlarmSummaryAsync",
            ct => AlarmSummaryAsync(ct)
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

        yield return new ClientCommand(
            "ReadHvacServiceAsync",
            ct => ReadHvacServiceAsync(ct)
        );

        yield return new ClientCommand(
            "ReadLightingZoneAsync",
            ct => ReadLightingZoneAsync(ct)
        );

        yield return new ClientCommand(
            "ReadMetersAsync",
            ct => ReadMetersAsync(ct)
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

    private async Task<JsonNode?> ReadHvacServiceAsync(CancellationToken ct)
    {
        var atContainer = new JsonObject();
        var arrContainer = new JsonObject();
        var jsonArr = new JsonArray();

        string methodName = "ReadHvacServiceAsync";

        var jsonRows = await _dbReader.GetHotRowsAsJsonAsync(ip: _ip, source: "ReadHvacAsync", ct);

        for (var i = 1; i < jsonRows.Count + 1; i++)
        {
            _logger.LogInformation("Executing {methodName} step {i} of {jsonRows.Count}", methodName, i, jsonRows.Count);

            var response = await _protocol.SendCommandAsync(
                "read_hvac_service",
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

        var final = SingleAtParse("arrkey", "@ahindex", atContainer);

        return final;
    }

    private async Task<JsonNode?> ReadLightingZoneAsync(CancellationToken ct)
    {
        var jsonRows = await _dbReader.GetHotRowsAsJsonAsync(ip: _ip, source: "ReadLightingAsync", ct);

        string methodName = "ReadLightingZoneAsync";
        //
        var outArr = new JsonArray();

        int i = 0;
        foreach (var entry in jsonRows)
        {
            i++;
            _logger.LogInformation("Executing {methodName} step {i} of {jsonRows.Count}", methodName, i, jsonRows.Count);

            var index = entry?["Data"]?["index"]?.GetValue<string>() ?? null;
            var nodeType = entry?["Data"]?["@nodetype"]?.GetValue<string>() ?? "?";
            var node = entry?["Data"]?["@node"]?.GetValue<string>() ?? "?";
            var mod = entry?["Data"]?["@mod"]?.GetValue<string>() ?? "?";
            var point = entry?["Data"]?["@point"]?.GetValue<string>() ?? "?";

            if (string.IsNullOrEmpty(index))
                continue;

            var response = await _protocol.SendCommandAsync(
                "read_lighting_zone",
                new Dictionary<string, string>() { ["index"] = index },
                ct
            );

            if (response is null)
                continue;

            var result = BareParse($"nt{nodeType}:n{node}:m{mod}:p{point}", response);
            outArr.Add(result["data"]![0]!.DeepClone());
        }

        var jsonWrap = new JsonObject();
        jsonWrap["data"] = outArr.DeepClone();
        return jsonWrap;
    }

    private async Task<JsonNode?> ReadSuctionGroupAsync(CancellationToken ct)
    {
        var jsonRows = await _dbReader.GetHotRowsAsJsonAsync(ip: _ip, source: "ReadDevicesAsync", ct);

        var outArr = new JsonArray();

        var seen = new HashSet<(string rId, string sId)>();
        foreach (var entry in jsonRows)
        {
            var sId = entry?["Data"]?["@suction_id"]?.GetValue<string>() ?? null;
            var rId = entry?["Data"]?["@rack_id"]?.GetValue<string>() ?? null;

            if (string.IsNullOrEmpty(sId) || string.IsNullOrEmpty(rId))
                continue;

            var addr = (sId, rId);

            if (seen.Contains(addr))
                continue;

            seen.Add(addr);
        }

        string methodName = "ReadSuctionGroupAsync";
        int i = 0;
        foreach (var idAddr in seen)
        {
            i++;
            _logger.LogInformation("Executing {methodName} step {i} of {seen.Count}", methodName, i, seen.Count);

            var response = await _protocol.SendCommandAsync(
                "read_suction_group",
                new Dictionary<string, string>() { ["rack_id"] = idAddr.rId, ["suction_id"] = idAddr.sId },
                ct
            );

            if (response is null)
                continue;

            var result = BareParse($"SuctionGroup:suctId{idAddr.sId}:rackId{idAddr.rId}", response);
            outArr.Add(result["data"]![0]!.DeepClone());
        }
        //
        var jsonWrap = new JsonObject();
        jsonWrap["data"] = outArr.DeepClone();
        return jsonWrap;
    }

    private async Task<JsonNode?> ReadCondenserAsync(CancellationToken ct)
    {
        var jsonRows = await _dbReader.GetHotRowsAsJsonAsync(ip: _ip, source: "ReadDevicesAsync", ct);

        var outArr = new JsonArray();

        var seen = new HashSet<string>();
        foreach (var entry in jsonRows)
        {
            var rId = entry?["Data"]?["@rack_id"]?.GetValue<string>() ?? null;

            if (string.IsNullOrEmpty(rId))
                continue;

            if (seen.Contains(rId))
                continue;

            seen.Add(rId);
        }

        string methodName = "ReadCondenserAsync";
        int i = 0;
        foreach (var idAddr in seen)
        {
            i++;
            _logger.LogInformation("Executing {methodName} step {i} of {seen.Count}", methodName, i, seen.Count);

            var response = await _protocol.SendCommandAsync(
                "read_condenser",
                new Dictionary<string, string>() { ["rack_id"] = idAddr },
                ct
            );

            if (response is null)
                continue;

            var result = BareParse($"condenser:rackId{idAddr}", response);
            outArr.Add(result["data"]![0]!.DeepClone());
        }
        //
        var jsonWrap = new JsonObject();
        jsonWrap["data"] = outArr.DeepClone();
        return jsonWrap;
    }

    private async Task<JsonNode?> ReadCircuitAsync(CancellationToken ct)
    {
        var jsonRows = await _dbReader.GetHotRowsAsJsonAsync(ip: _ip, source: "ReadSuctionGroupAsync", ct);

        var outArr = new JsonArray();

        string methodName = "ReadCircuitAsync";
        int i = 0;
        foreach (var entry in jsonRows)
        {
            var sId = entry?["Data"]?["@suction_id"]?.GetValue<string>() ?? null;
            var rId = entry?["Data"]?["@rack_id"]?.GetValue<string>() ?? null;
            var num = entry?["Data"]?["num_circuits"]?.GetValue<string>() ?? "0";

            if (!int.TryParse(num, out var numInt))
            {
                continue;
            }

            if (string.IsNullOrEmpty(rId) || string.IsNullOrEmpty(sId))
                continue;

            i++;
            _logger.LogInformation("Executing {methodName} step {i} of {jsonRows.Count}", methodName, i, jsonRows.Count);

            for (var j = 1; j < numInt + 1; j++)
            {
                _logger.LogInformation("Executing {methodName} step {i} substep {j} of {numInt}", methodName, i, j, numInt);

                var response = await _protocol.SendCommandAsync(
                    "read_condenser",
                    new Dictionary<string, string>() { ["rack_id"] = rId, ["suction_id"] = sId, ["circuit_id"] = j.ToString() },
                    ct
                );

                if (response is null)
                    continue;

                var result = BareParse($"circuit:rackId{rId}:suctId{sId}:circId{j}", response);
                outArr.Add(result["data"]![0]!.DeepClone());
            }

        }

        var jsonWrap = new JsonObject();
        jsonWrap["data"] = outArr.DeepClone();
        return jsonWrap;
    }

    private async Task<JsonNode?> ReadMetersAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("read_meters", null, ct);
        if (response is null)
            return null;

        return BareParse("meters", response);
    }

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

    private JsonObject SingleAtParse(string subKey, string atKey, JsonNode data)
    {
        var jsonResponse = data["resp"]?.AsObject() ?? new JsonObject();
        var subarr = jsonResponse[subKey] as JsonArray ?? new JsonArray();

        var root = new JsonObject();
        var dataArray = new JsonArray();

        foreach (var entryNode in subarr)
        {
            if (entryNode is not JsonObject entry)
                continue;

            var atVal = entry[atKey]?.GetValue<string>() ?? "?";

            var normalized = NormalizerService.Normalize(entry);

            var obj = new JsonObject
            {
                ["device_key"] = $"{atKey}{atVal}",
                ["data"] = normalized.DeepClone()
            };

            dataArray.Add(obj);
        }

        root["data"] = dataArray;
        return root;
    }
}
