using System.Text.Json.Nodes;

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
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            _logger.LogInformation("Simulating sending telemetry...");

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
        }
    }
}
