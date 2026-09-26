namespace DevDocSpace.Data.Entities;

// A portal-authored Markdown page. Served instead of a content-store file at the same namespace/path.
public class DocPage
{
    public Guid Id { get; set; }
    public required string Namespace { get; set; }
    public required string Path { get; set; }
    public required string Markdown { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid? UpdatedById { get; set; }
    public User? UpdatedBy { get; set; }
}
