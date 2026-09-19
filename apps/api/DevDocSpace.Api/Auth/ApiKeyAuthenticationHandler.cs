using System.Security.Claims;
using System.Text.Encodings.Web;
using DevDocSpace.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevDocSpace.Api.Auth;

public class ApiKeyAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    AppDbContext db)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string SchemeName = "ApiKey";
    public const string HeaderName = "X-Api-Key";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(HeaderName, out var values) || values.Count == 0)
            return AuthenticateResult.NoResult();

        var plain = values[0]!;
        if (!plain.StartsWith(ApiKeyHasher.KeyPrefix, StringComparison.Ordinal))
            return AuthenticateResult.Fail("Malformed API key");

        var prefix = ApiKeyHasher.ExtractPrefix(plain);
        var candidates = await db.ApiKeys.Include(k => k.User)
            .Where(k => k.Prefix == prefix && k.RevokedAt == null)
            .ToListAsync(Context.RequestAborted);

        var match = candidates.FirstOrDefault(k => ApiKeyHasher.Verify(plain, k.KeyHash));
        if (match is null) return AuthenticateResult.Fail("Invalid API key");

        var identity = new ClaimsIdentity(
        [
            new Claim(AppClaims.UserId, match.UserId.ToString()),
            new Claim(ClaimTypes.Email, match.User.Email),
            new Claim(AppClaims.AuthMethod, "apikey"),
        ], SchemeName);

        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), SchemeName));
    }
}

public static class AppClaims
{
    public const string UserId = "dds:user_id";
    public const string AuthMethod = "dds:auth_method";
}
