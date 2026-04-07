using System.Text.Json.Nodes;

public sealed class TelemetryWorker : BackgroundService
{
    private readonly IIotDevice _iotDevice;
    private readonly DbReader _dbReader;
    private readonly ILogger<TelemetryWorker> _logger;

    private DateTime _lastFullFrameTime = DateTime.MinValue;
    private DateTime _lastDbCleanTime = DateTime.UtcNow;

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
            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            _logger.LogInformation("Sending telemetry...");

            JsonArray dataToSend;

            if (DateTime.UtcNow - _lastFullFrameTime >= TimeSpan.FromMinutes(60))
            {
                _logger.LogInformation("Sending full frame of data");
                dataToSend = await _dbReader.GetHotRowsAsJsonAsync(stoppingToken);

                await _dbReader.ReplaceSnapshotAsync(stoppingToken);

                _lastFullFrameTime = DateTime.UtcNow;
            }
            else
            {
                _logger.LogInformation("Sending delta frame of data");
                dataToSend = await _dbReader.GetDeltaAsync(stoppingToken);
            }

            await _iotDevice.SendMessageAsync(dataToSend, stoppingToken);

            if (DateTime.UtcNow - _lastFullFrameTime >= TimeSpan.FromHours(24))
            {
                await _dbReader.CleanDbAsync(stoppingToken);
            }
        }
    }
}
