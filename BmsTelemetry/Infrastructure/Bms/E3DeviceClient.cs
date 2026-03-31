using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;

public sealed class E3DeviceClient : IBmsClient
{
    private readonly E3Protocol _protocol;
    private readonly ILogger<E3DeviceClient> _logger;
    private readonly IBmsTransport _transport;
    private readonly DbReader _dbReader;
    private readonly string _ip;
    private string _sessionId = string.Empty;
    private DateTime _lastInitTime = DateTime.MinValue;

    public E3DeviceClient(IBmsTransport transport, string deviceIP, DbReader dbReader, ILoggerFactory loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<E3DeviceClient>();
        _protocol = new E3Protocol(transport, loggerFactory);
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
        //     "GetAlarmsAsync",
        //     ct => GetAlarmsAsync(ct)
        // );
    }

    private IEnumerable<ClientCommand> GetInitCommands()
    {
        yield return new ClientCommand(
            "GetSystemInventoryAsync",
            ct => GetSystemInventoryAsync(ct)
        );
    }

    private IEnumerable<ClientCommand> GetContinuousCommands()
    {
        yield return new ClientCommand(
            "GetAppDescriptionAsync",
            ct => GetAppDescriptionAsync(ct)
        );

        yield return new ClientCommand(
            "GetAlarmsAsync",
            ct => GetAlarmsAsync(ct)
        );
    }

    private async Task<JsonNode?> GetSystemInventoryAsync(CancellationToken ct)
    {
        var sidOk = await EnsureSessionIdAsync(ct);
        if (!sidOk)
            return null;

        var response = await _protocol.SendCommandAsync(
            "GetSystemInventory",
            new JsonObject { ["sid"] = _sessionId },
            ct,
            HttpMethod.Post
        );

        if (response is null)
        {
            _sessionId = string.Empty;
            return null;
        }

        // Validate JSON structure
        var result = response["result"] as JsonObject;
        if (result is null)
        {
            _sessionId = string.Empty;
            return null;
        }

        var aps = result["aps"] as JsonArray;
        if (aps is null)
        {
            _sessionId = string.Empty;
            return null;
        }

        // Build output
        var outArr = new JsonArray();

        foreach (var entry in aps)
        {
            if (entry is not JsonObject obj)
                continue;

            var appName = obj["appname"]?.GetValue<string>();
            if (string.IsNullOrEmpty(appName))
                continue;

            outArr.Add(new JsonObject
            {
                ["device_key"] = $"app{appName}",
                ["data"] = obj.DeepClone()
            });
        }

        var outObj = new JsonObject
        {
            ["data"] = outArr
        };

        return outObj;
    }


    private async Task<JsonNode?> GetAppDescriptionAsync(CancellationToken ct)
    {
        var sidOk = await EnsureSessionIdAsync(ct);
        if (!sidOk)
            return null;

        // Load cached system inventory rows
        var jsonRows = await _dbReader.GetHotRowsAsJsonAsync(
            ip: _ip,
            source: "GetSystemInventoryAsync",
            ct
        );

        var dataArr = new JsonArray();

        int i = 1;
        string methodName = "GetAppDescriptionAsync";
        foreach (var entry in jsonRows)
        {
            _logger.LogInformation("Executing {methodName} step {i} of {Count}", methodName, i, jsonRows.Count);

            var iid = entry?["Data"]?["iid"]?.GetValue<string>() ?? string.Empty;
            var appname = entry?["Data"]?["appname"]?.GetValue<string>() ?? string.Empty;

            if (string.IsNullOrEmpty(iid) || string.IsNullOrEmpty(appname))
                continue;

            // Call GetAppDescription
            var response = await _protocol.SendCommandAsync(
                "GetAppDescription",
                new JsonObject { ["sid"] = _sessionId, ["iid"] = iid },
                ct,
                HttpMethod.Post
            );

            if (response is null)
            {
                _sessionId = string.Empty;
                return null;
            }

            // Extract points array
            var points = response["result"]?["points"] as JsonArray;
            if (points is null)
            {
                _sessionId = string.Empty;
                return null;
            }

            var thisEntry = new JsonObject { ["device_key"] = $"app{appname}", ["data"] = new JsonObject() };

            foreach (var point in points)
            {
                if (point is not JsonObject pointObj)
                    continue;

                // Extract name + val
                var name = pointObj["name"]?.GetValue<string>();
                string val;

                try
                {
                    val = pointObj["val"]?.GetValue<string>() ?? "nullString";
                }
                catch
                {
                    val = "invalidData";
                }

                if (name is null || val is null)
                    continue;

                thisEntry["data"]![name] = val;
            }

            dataArr.Add(thisEntry.DeepClone());

            i++;
        }

        var outObj = new JsonObject { ["data"] = dataArr.DeepClone() };

        return outObj;
    }

    private async Task<JsonNode?> GetAlarmsAsync(CancellationToken ct)
    {
        var sidOk = await EnsureSessionIdAsync(ct);
        if (!sidOk)
            return null;

        var response = await _protocol.SendCommandAsync(
            "GetAlarms",
            new JsonObject { ["sid"] = _sessionId },
            ct,
            HttpMethod.Post
        );

        if (response is null)
        {
            _sessionId = string.Empty;
            return null;
        }

        // Validate JSON structure
        var result = response["result"] as JsonObject;
        if (result is null)
        {
            _sessionId = string.Empty;
            return null;
        }

        var alarms = result["alarms"] as JsonArray;
        if (alarms is null)
        {
            _sessionId = string.Empty;
            return null;
        }

        var outArr = new JsonArray();
        foreach (var entry in alarms)
        {
            var advId = entry?["advinstanceid"]?.GetValue<string>();

            if (string.IsNullOrEmpty(advId))
                continue;

            var data = NormalizerService.Normalize(entry?.AsObject());

            if (data is null)
                continue;

            var thisData = new JsonObject
            {
                ["device_key"] = $"alarm{advId}",
                ["data"] = data.DeepClone()
            };

            outArr.Add(thisData);
        }

        var outObj = new JsonObject
        {
            ["data"] = outArr.DeepClone()
        };

        return outObj;
    }

    private async Task<bool> EnsureSessionIdAsync(CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_sessionId))
        {
            await GetSessionIdAsync(ct);

            if (string.IsNullOrEmpty(_sessionId))
                return false;
        }
        return true;
    }

    private async Task GetSessionIdAsync(CancellationToken ct)
    {
        _logger.LogInformation("Fetching new SID for {_endpoint}", _transport._endpoint);
        var response = await _protocol.SendCommandAsync("GetSessionID", null, ct, HttpMethod.Get);
        if (response is null)
        {
            _sessionId = string.Empty;
            return;
        }

        _sessionId = response["result"]?["sid"]?.GetValue<string>() ?? string.Empty;

        return;
    }
}
