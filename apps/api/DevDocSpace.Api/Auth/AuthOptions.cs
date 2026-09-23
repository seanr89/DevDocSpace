using DevDocSpace.Data.Entities;

namespace DevDocSpace.Api.Auth;

public class AuthOptions
{
    public const string Section = "Auth";

    public string FirebaseProjectId { get; set; } = "";
    public bool UseFirebaseEmulator { get; set; }
    public List<string> AdminEmails { get; set; } = [];

    // Development only: accept `X-Dev-User: <email>` in place of a Firebase token (see DevAuthHandler).
    public bool UseDevAuth { get; set; }
    // Email -> role seeded on first dev sign-in, so preset local users land with the right role.
    public Dictionary<string, Role> DevUsers { get; set; } = [];
}
