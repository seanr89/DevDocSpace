namespace DevDocSpace.Api.Auth;

public class AuthOptions
{
    public const string Section = "Auth";

    public string FirebaseProjectId { get; set; } = "";
    public bool UseFirebaseEmulator { get; set; }
    public List<string> AdminEmails { get; set; } = [];
}
