public sealed class TelemetryWorker : BackgroundService
{
    private readonly IIotDevice _iotDevice;
    private readonly DbReader _dbReader;
    private readonly ILogger<TelemetryWorker> _logger;

    private DateTime _lastFullFrameTime = DateTime.MinValue;

    public TelemetryWorker(IIotDevice iotDevice, DbReader dbReader, ILogger<TelemetryWorker> logger)
    {
        _iotDevice = iotDevice;
        _dbReader = dbReader;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(8), stoppingToken);
            _logger.LogInformation("Simulating sending telemetry...");
            var x = await _dbReader.GetHotRowsAsJson("10.158.71.180");

            Console.WriteLine(x);


            // declare data;

            // if (DateTime.UtcNow - _lastFullFrameTime >= TimeSpan.FromMinutes(60))
            // {
            //     _logger.LogInformation("Sending full frame of data");
            //     // data = get full data;
            //     _lastFullFrameTime = DateTime.UtcNow;
            // }
            // else
            // {
            //     _logger.LogInformation("Sending CoV frame of data");
            //     // data = get CoV data;
            // }

            // var dataToSend = JsonArrayConvert(data); <- Do NOT implement this, just a placeholder assume defined elsewhere.

            // await _iotDevice.SendMessageAsync(new JsonArray(), stoppingToken); // replace dummy array with dataToSend
        }
    }
}

// SAMPLE: 
// using System.Text.Json.Nodes;
// using Microsoft.EntityFrameworkCore;
//
// public sealed class TelemetryWorker : BackgroundService
// {
//     private readonly IIotDevice _iotDevice;
//     private readonly ILogger<TelemetryWorker> _logger;
//     private readonly IDbContextFactory<AppDbContext> _dbFactory;
//
//     private DateTime _lastFullFrameTime = DateTime.MinValue;
//
//     public TelemetryWorker(
//         IIotDevice iotDevice,
//         ILogger<TelemetryWorker> logger,
//         IDbContextFactory<AppDbContext> dbFactory)
//     {
//         _iotDevice = iotDevice;
//         _logger = logger;
//         _dbFactory = dbFactory;
//     }
//
//     protected override async Task ExecuteAsync(CancellationToken stoppingToken)
//     {
//         while (!stoppingToken.IsCancellationRequested)
//         {
//             await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
//             _logger.LogInformation("Preparing telemetry frame...");
//
//             using var db = await _dbFactory.CreateDbContextAsync(stoppingToken);
//
//             JsonArray dataToSend;
//
//             bool sendFull =
//                 DateTime.UtcNow - _lastFullFrameTime >= TimeSpan.FromHours(1);
//
//             if (sendFull)
//             {
//                 _logger.LogInformation("Sending FULL frame");
//
//                 // 1. Read full frame
//                 var full = await db.TelemetryCurrent
//                     .AsNoTracking()
//                     .ToListAsync(stoppingToken);
//
//                 dataToSend = ConvertToJson(full); // your placeholder
//
//                 // 2. Replace snapshot table
//                 await ReplaceSnapshotAsync(db, stoppingToken);
//
//                 _lastFullFrameTime = DateTime.UtcNow;
//             }
//             else
//             {
//                 _logger.LogInformation("Sending CoV (delta) frame");
//
//                 // 1. Read delta frame
//                 var delta = await GetDeltaAsync(db, stoppingToken);
//
//                 dataToSend = ConvertToJson(delta); // your placeholder
//
//                 // 2. Update snapshot with only the changed rows
//                 await UpsertSnapshotAsync(db, delta, stoppingToken);
//             }
//
//             // 3. Send telemetry
//             await _iotDevice.SendMessageAsync(dataToSend, stoppingToken);
//         }
//     }
//
//     // ---------------------------
//     // DATABASE LOGIC
//     // ---------------------------
//
//     private static async Task<List<TelemetryRecord>> GetDeltaAsync(
//         AppDbContext db, CancellationToken ct)
//     {
//         return await db.TelemetryCurrent
//             .AsNoTracking()
//             .GroupJoin(
//                 db.TelemetryLastSent,
//                 c => new { c.Ip, c.DeviceKey, c.DataKey },
//                 s => new { s.Ip, s.DeviceKey, s.DataKey },
//                 (c, sGroup) => new { c, sGroup }
//             )
//             .SelectMany(
//                 x => x.sGroup.DefaultIfEmpty(),
//                 (x, s) => new { x.c, s }
//             )
//             .Where(x =>
//                 x.s == null ||
//                 x.s.DataValue != x.c.DataValue
//             )
//             .Select(x => x.c)
//             .ToListAsync(ct);
//     }
//
//     private static async Task ReplaceSnapshotAsync(
//         AppDbContext db, CancellationToken ct)
//     {
//         await db.Database.ExecuteSqlRawAsync("DELETE FROM TelemetryLastSent;", ct);
//
//         await db.Database.ExecuteSqlRawAsync(@"
//             INSERT INTO TelemetryLastSent (Ip, DeviceKey, DataKey, DataValue)
//             SELECT Ip, DeviceKey, DataKey, DataValue
//             FROM TelemetryCurrent;
//         ", ct);
//     }
//
//     private static async Task UpsertSnapshotAsync(
//         AppDbContext db,
//         List<TelemetryRecord> changed,
//         CancellationToken ct)
//     {
//         if (changed.Count == 0)
//             return;
//
//         // Bulk upsert into snapshot
//         await db.BulkInsertOrUpdateAsync(changed, cancellationToken: ct);
//     }
//
//     // ---------------------------
//     // JSON CONVERSION PLACEHOLDER
//     // ---------------------------
//
//     private JsonArray ConvertToJson(List<TelemetryRecord> records)
//     {
//         // You said not to implement this — so here’s a stub.
//         // Assume you have your own converter elsewhere.
//         return new JsonArray();
//     }
// }
