using Microsoft.EntityFrameworkCore;
using System.Text.Json.Nodes;

public class DbReader
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DbReader(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task<JsonArray> GetHotRowsAsJson(string ip, CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.Telemetry
            .Where(x => x.Ip == ip)
            .AsNoTracking()
            .ToListAsync(ct);

        return GroupRows(rows);
    }

    public async Task<JsonArray> GetHotRowsAsJson(string ip, string source, CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.Telemetry
            .Where(x => x.Ip == ip && x.Source == source)
            .AsNoTracking()
            .ToListAsync(ct);

        return GroupRows(rows);
    }

    private JsonArray GroupRows(List<TelemetryRecord> rows)
    {
        var grouped = rows
            .GroupBy(r => new { r.Ip, r.DeviceKey })
            .ToList();

        var result = new JsonArray();

        foreach (var deviceGroup in grouped)
        {
            var obj = new JsonObject
            {
                ["Ip"] = deviceGroup.Key.Ip,
                ["DeviceKey"] = deviceGroup.Key.DeviceKey
            };

            var dataObj = new JsonObject();

            foreach (var row in deviceGroup)
            {
                dataObj[row.DataKey] = row.DataValue;
            }

            obj["Data"] = dataObj;
            result.Add(obj);
        }

        return result;
    }
}
