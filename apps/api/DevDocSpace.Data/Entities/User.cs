namespace DevDocSpace.Data.Entities;

public class User
{
    public Guid Id { get; set; }
    public required string FirebaseUid { get; set; }
    public required string Email { get; set; }
    public Role Role { get; set; } = Role.ExternalClient;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<ApiKey> ApiKeys { get; set; } = [];
}
