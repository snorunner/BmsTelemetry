public class UptimeService
{
    private readonly DateTime _startTime = DateTime.UtcNow;

    public TimeSpan GetUptime()
    {
        return DateTime.UtcNow - _startTime;
    }
}
