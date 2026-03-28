public record GeneralSettings
{
    public int http_request_delay_seconds { get; init; } = 5;
    public int http_timeout_delay_seconds { get; init; } = 15;
    public int http_retry_count { get; init; } = 3;
    public int soft_reset_interval_hours { get; init; } = 12;
    public bool keep_alive { get; init; }
    public bool use_cloud { get; init; } = true;
}
