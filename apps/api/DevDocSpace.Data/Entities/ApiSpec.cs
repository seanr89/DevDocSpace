namespace DevDocSpace.Data.Entities;

public class ApiSpec
{
    public Guid Id { get; set; }
    public required string Service { get; set; }
    public required string Version { get; set; }
    // Relative path in the content store; null when the spec exists only in the database.
    public string? Path { get; set; }
    public Role RequiredRole { get; set; } = Role.InternalDeveloper;

    // Portal-authored spec JSON; when set it is served instead of the file at Path.
    public string? Content { get; set; }
    public DateTimeOffset? ContentUpdatedAt { get; set; }
    public Guid? ContentUpdatedById { get; set; }
    public User? ContentUpdatedBy { get; set; }

    public ICollection<ServiceEnvironment> Environments { get; set; } = [];
}

public enum ApiEnvironment
{
    Sandbox = 0,
    Staging = 1,
    Production = 2,
}

public class ServiceEnvironment
{
    public Guid Id { get; set; }
    public Guid SpecId { get; set; }
    public ApiSpec Spec { get; set; } = null!;
    public ApiEnvironment Name { get; set; }
    public required string BaseUrl { get; set; }
    public string? CredentialKey { get; set; }
}
