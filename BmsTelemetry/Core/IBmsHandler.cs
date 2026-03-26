public interface IBmsHandler
{
    string DeviceIP { get; init; }
    BmsType DeviceType { get; init; }
    ConnectionStatus Connection { get; set; }
    BmsHandlerStatus Status { get; set; }

    Task StartAsync(CancellationToken ct);

    Task StopAsync();
}
