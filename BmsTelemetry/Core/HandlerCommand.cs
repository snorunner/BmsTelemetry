public enum HandlerCommandType
{
    PollStep,
    UserCommand,
    Start,
    Stop
}

public record HandlerCommand(
    HandlerCommandType Type,
    ClientCommand? clientCommand = null
)
{
    public static HandlerCommand Start() => new(HandlerCommandType.Start);
    public static HandlerCommand Stop() => new(HandlerCommandType.Stop);
    public static HandlerCommand Poll(ClientCommand cmd) => new(HandlerCommandType.PollStep, cmd);
}
