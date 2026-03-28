public interface IBmsClient
{
    IAsyncEnumerable<ClientCommand> GetPollingSequenceAsync(CancellationToken ct);

    // Task ExecuteUserCommandAsync(UserCommand cmd, CancellationToken ct);
}
