namespace DevDocSpace.Data.Entities;

public class ApiCallLog
{
    public long Id { get; set; }
    public Guid UserId { get; set; }
    public Guid SpecId { get; set; }
    public ApiEnvironment Environment { get; set; }
    public required string Method { get; set; }
    public required string Path { get; set; }
    public int StatusCode { get; set; }
    public int DurationMs { get; set; }
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}
