public interface IBmsClient
{
    Task StartAsync(CancellationToken ct);
    Task StopAsync();
}
