using System.Security.Claims;
using System.Text.Encodings.Web;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevDocSpace.Api.Auth;

// Local-development stand-in for Firebase: trusts an `X-Dev-User: <email>` header outright.
// Only registered when Auth:UseDevAuth is set, which Program.cs restricts to the Development environment.
public class DevAuthHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db,
    IOptions<AuthOptions> authOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "DevAuth";
    public const string HeaderName = "X-Dev-User";
    public const string UidPrefix = "dev:";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || string.IsNullOrWhiteSpace(values[0]))
            return AuthenticateResult.NoResult();

        var email = values[0]!.Trim();
        var uid = UidPrefix + email;
        await SeedConfiguredRoleAsync(uid, email);

        var identity = new ClaimsIdentity(
        [
            new Claim("sub", uid),
            new Claim("email", email),
            new Claim(AppClaims.AuthMethod, "dev"),
        ], SchemeName);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }

    // Auth:DevUsers maps email -> role for preset local users. Applied only when the row is first
    // created so roles still live in the database and can be changed via the admin UI afterwards.
    private async Task SeedConfiguredRoleAsync(string uid, string email)
    {
        var configured = authOptions.Value.DevUsers
            .FirstOrDefault(kv => string.Equals(kv.Key, email, StringComparison.OrdinalIgnoreCase));
        if (configured.Key is null) return;
        if (await db.Users.AnyAsync(u => u.FirebaseUid == uid, Context.RequestAborted)) return;

        db.Users.Add(new User { FirebaseUid = uid, Email = email, Role = configured.Value });
        await db.SaveChangesAsync(Context.RequestAborted);
    }
}
