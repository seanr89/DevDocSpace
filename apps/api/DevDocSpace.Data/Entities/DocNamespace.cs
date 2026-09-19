namespace DevDocSpace.Data.Entities;

public class DocNamespace
{
    public Guid Id { get; set; }
    public required string Slug { get; set; }
    public string? Title { get; set; }
    public Role RequiredRole { get; set; } = Role.ExternalClient;
}
