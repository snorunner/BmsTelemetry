public interface IBmsHandler
{
    string DeviceIP { get; init; }
    BmsType DeviceType { get; init; }
    ConnectionStatus Connection { get; }
    BmsHandlerStatus Status { get; }

    int ConsecutiveFailures { get; }
    DateTime LastSuccess { get; }
    DateTime LastFailure { get; }

    event Action? OnStatusChanged;

    Task StartAsync(CancellationToken ct);

    Task StopAsync();

    Task EvaluateAsync(CancellationToken ct);
}
