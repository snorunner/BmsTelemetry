using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

public sealed class E2DeviceClient : IBmsClient
{
    private readonly E2Protocol _protocol;
    private readonly ILogger<E2DeviceClient> _logger;
    private readonly IBmsTransport _transport;
    private readonly DbReader _dbReader;
    private readonly string _ip;
    private string _controllerName = "HVAC/LTS"; // set to reasonable default for testing
    private DateTime _lastInitTime = DateTime.MinValue;

    public E2DeviceClient(IBmsTransport transport, string deviceIP, DbReader dbReader, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<E2DeviceClient>();
        _protocol = new E2Protocol(transport, loggerFactory);
        _transport = transport;
        _dbReader = dbReader;
        _ip = deviceIP;
    }

    public async IAsyncEnumerable<ClientCommand> GetPollingSequenceAsync([EnumeratorCancellation] CancellationToken ct)
    {
        if (DateTime.UtcNow - _lastInitTime >= TimeSpan.FromMinutes(60))
        {
            foreach (var cmd in GetInitCommands())
            {
                yield return cmd;
            }

            _lastInitTime = DateTime.UtcNow;
        }

        foreach (var cmd in GetContinuousCommands())
        {
            yield return cmd;
        }

        // Test new commands
        // yield return new ClientCommand(
        //     "E2.GetMultiExpandedStatusAsync",
        //     ct => GetMultiExpandedStatusAsync(ct)
        // );
    }

    private IEnumerable<ClientCommand> GetInitCommands()
    {
        yield return new ClientCommand(
            "E2.GetControllerListAsync",
            ct => GetControllerListAsync(ct)
        );

        yield return new ClientCommand(
            "E2.GetCellListAsync",
            ct => GetCellListAsync(ct)
        );
    }

    private IEnumerable<ClientCommand> GetContinuousCommands()
    {
        yield return new ClientCommand(
            "E2.GetAlarmListAsync",
            ct => GetAlarmListAsync(ct)
        );

        yield return new ClientCommand(
            "E2.GetMultiExpandedStatusAsync",
            ct => GetMultiExpandedStatusAsync(ct)
        );
    }

    private async Task<JsonNode?> GetControllerListAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("E2.GetControllerList", null, ct);
        if (response is null)
            return null;

        var controllerArr = response["result"]?.AsArray() ?? new JsonArray();

        var outArr = new JsonArray();

        int i = 0;
        foreach (var controller in controllerArr)
        {
            var name = controller?["name"]?.GetValue<string>();

            if (string.IsNullOrEmpty(name))
                continue;

            outArr.Add(new JsonObject
            {
                ["device_key"] = $"controller{name}",
                ["data"] = controller?.DeepClone() ?? new JsonObject()
            });

            // Set primary controller
            if (i == 0)
                _controllerName = name;
            i++;
        }

        var result = new JsonObject { ["data"] = outArr };

        return result;
    }

    private async Task<JsonNode?> GetCellListAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("E2.GetCellList", new JsonArray { _controllerName }, ct);
        if (response is null)
            return null;

        var cellArr = response["result"]?["data"]?.AsArray() ?? new JsonArray();
        var outArr = new JsonArray();

        foreach (var cell in cellArr)
        {
            var name = cell?["cellname"]?.GetValue<string>();

            if (string.IsNullOrEmpty(name))
                continue;

            outArr.Add(new JsonObject
            {
                ["device_key"] = $"controller{_controllerName}:cell{name}",
                ["data"] = cell?.DeepClone() ?? new JsonObject()
            });
        }

        var result = new JsonObject { ["data"] = outArr };

        return result;
    }

    private async Task<JsonNode?> GetAlarmListAsync(CancellationToken ct)
    {
        var response = await _protocol.SendCommandAsync("E2.GetAlarmList", new JsonArray { _controllerName }, ct);
        if (response is null)
            return null;

        var alarmArr = response["result"]?["data"]?.AsArray() ?? new JsonArray();

        var outArr = new JsonArray();

        foreach (var alarm in alarmArr)
        {
            int? advid = null;

            try
            {
                advid = alarm?["advid"]?.GetValue<int>();
            }
            catch
            {
                continue;
            }

            if (advid is null)
                continue;

            outArr.Add(new JsonObject
            {
                ["device_key"] = $"controller{_controllerName}:alarm{advid}",
                ["data"] = alarm?.DeepClone() ?? new JsonObject()
            });
        }

        var result = new JsonObject { ["data"] = outArr };

        return result;
    }

    private async Task<JsonNode?> GetMultiExpandedStatusAsync(CancellationToken ct)
    {
        var jsonRows = await _dbReader.GetHotRowsAsJsonAsync(ip: _ip, source: "E2.GetCellListAsync", ct);

        var outArr = new JsonArray();

        int i = 1;
        string methodName = "GetMultiExpandedStatusAsync";

        foreach (var entry in jsonRows)
        {
            _logger.LogInformation("Executing {methodName} step {i} of {jsonRows.Count}", methodName, i, jsonRows.Count);
            var cellString = entry!["Data"]!["celltype"]!.GetValue<string>();
            var cellName = entry!["Data"]!["cellname"]!.GetValue<string>();

            if (string.IsNullOrEmpty(cellString) || string.IsNullOrEmpty(cellName))
                continue;

            if (!int.TryParse(cellString, out var cellInt))
                continue;

            if (!E2PointCatalog.ByCellType.TryGetValue(cellInt, out var points))
                continue;

            if (points.Count == 0)
                continue;

            var paramArray = new JsonArray();

            foreach (var p in points)
            {
                var key = $"{_controllerName}:{cellName}:{p.Index}";
                paramArray.Add(key);
            }

            var parameters = new JsonArray { paramArray };

            var response = await _protocol.SendCommandAsync("E2.GetMultiExpandedStatus", parameters, ct);

            if (response is null)
                continue;

            // NormalizerService.ConsolePrettyPrint(response);

            var data = response?["result"]?["data"] as JsonArray;

            if (data is null)
                continue;

            var pointLookup = points.ToDictionary(p => p.Index, p => p.Name);

            // replace idx with name
            foreach (var item in data)
            {
                var prop = item?["prop"]?.GetValue<string>();
                if (string.IsNullOrEmpty(prop))
                    continue;

                var lastColon = prop.LastIndexOf(":");
                if (lastColon < 0 || lastColon == prop.Length - 1)
                    continue;

                var indexPart = prop[(lastColon + 1)..];

                if (!int.TryParse(indexPart, out var index))
                    continue;

                if (!pointLookup.TryGetValue(index, out var name))
                    continue;

                item!["prop"] = prop[..(lastColon + 1)] + name;
            }

            var flattened = new JsonObject();

            foreach (var item in data)
            {
                var prop = item?["prop"]?.GetValue<string>();
                if (string.IsNullOrEmpty(prop))
                    continue;

                var lastColon = prop.LastIndexOf(':');
                if (lastColon < 0 || lastColon == prop.Length - 1)
                    continue;

                var pointName = prop[(lastColon + 1)..];

                // Iterate ALL properties on the item
                foreach (var kvp in item!.AsObject())
                {
                    if (kvp.Key == "prop")
                        continue;

                    var key = $"{pointName}__{kvp.Key}";
                    var value = kvp.Value?.ToString() ?? "?";

                    flattened[key] = value;
                }
            }

            var outObj = new JsonObject
            {
                ["device_key"] = $"controller{_controllerName}:cell{cellName}",
                ["data"] = flattened
            };

            outArr.Add(outObj.DeepClone());

            i++;
        }

        var result = new JsonObject { ["data"] = outArr };

        return result;
    }
}
