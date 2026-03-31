using Microsoft.EntityFrameworkCore;
using EFCore.BulkExtensions;
using System.Text.Json.Nodes;

public class DbReader
{
    private readonly IServiceScopeFactory _serviceScopeFactory;

    public DbReader(IServiceScopeFactory serviceScopeFactory)
    {
        _serviceScopeFactory = serviceScopeFactory;
    }

    public async Task ReplaceSnapshotAsync(CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync("DELETE FROM TelemetrySnapshot;", ct);

        await db.Database.ExecuteSqlRawAsync(@"
            INSERT INTO TelemetrySnapshot (Ip, DeviceKey, DataKey, Source, DataValue, Timestamp)
            SELECT Ip, DeviceKey, DataKey, Source, DataValue, Timestamp
            FROM Telemetry;
        ", ct);
    }

    public async Task CleanDbAsync(CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        await db.Database.ExecuteSqlRawAsync("DELETE FROM TelemetrySnapshot;", ct);
        await db.Database.ExecuteSqlRawAsync("DELETE FROM Telemetry;", ct);
    }

    public async Task<JsonArray> GetDeltaAsync(CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.Telemetry
            .AsNoTracking()
            .GroupJoin(
                db.TelemetrySnapshot,
                c => new { c.Ip, c.DeviceKey, c.DataKey },
                s => new { s.Ip, s.DeviceKey, s.DataKey },
                (c, sGroup) => new { c, sGroup }
            )
            .SelectMany(
                x => x.sGroup.DefaultIfEmpty(),
                (x, s) => new { x.c, s }
            )
            .Where(x =>
                x.s == null ||
                x.s.DataValue != x.c.DataValue
            )
            .Select(x => x.c)
            .ToListAsync(ct);

        await UpsertSnapshotAsync(rows, ct);

        return GroupRows(rows);
    }

    private async Task UpsertSnapshotAsync(List<TelemetryRecord> changed, CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (changed.Count == 0)
            return;

        // Convert TelemetryRecord -> TelemetrySnapshotRecord
        var snapshotRows = changed.Select(x => new TelemetrySnapshotRecord
        {
            Ip = x.Ip,
            DeviceKey = x.DeviceKey,
            DataKey = x.DataKey,
            DataValue = x.DataValue,
            Source = x.Source,
            Timestamp = x.Timestamp
        }).ToList();

        // Bulk upsert into snapshot table
        await db.BulkInsertOrUpdateAsync(snapshotRows, cancellationToken: ct);
    }

    public async Task<JsonArray> GetSnapRowsAsJsonAsync(CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.TelemetrySnapshot
            .AsNoTracking()
            .ToListAsync(ct);

        return GroupRows(rows);
    }

    public async Task<JsonArray> GetHotRowsAsJsonAsync(CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.Telemetry
            .AsNoTracking()
            .ToListAsync(ct);

        return GroupRows(rows);
    }

    public async Task<JsonArray> GetHotRowsAsJsonAsync(string ip, CancellationToken ct = default)
    {
        using var scope = _serviceScopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var rows = await db.Telemetry
            .Where(x => x.Ip == ip)
            .AsNoTracking()
            .ToListAsync(ct);

        return GroupRows(rows);
    }

    public async Task<JsonArray> GetHotRowsAsJsonAsync(string ip, string source, CancellationToken ct = default)
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

    private JsonArray GroupRows(List<TelemetrySnapshotRecord> rows)
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
