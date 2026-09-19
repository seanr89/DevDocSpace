using System.Security.Claims;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace DevDocSpace.Api.Auth;

public class CurrentUser(AppDbContext db, IHttpContextAccessor accessor, IOptions<AuthOptions> authOptions)
{
    private User? _user;
    private bool _resolved;

    public async Task<User?> GetAsync(CancellationToken ct = default)
    {
        if (_resolved) return _user;
        _resolved = true;

        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated != true) return null;

        var userIdClaim = principal.FindFirstValue(AppClaims.UserId);
        if (userIdClaim is not null && Guid.TryParse(userIdClaim, out var id))
        {
            _user = await db.Users.FindAsync([id], ct);
            return _user;
        }

        var uid = principal.FindFirstValue("sub") ?? principal.FindFirstValue("user_id");
        if (uid is null) return null;
        var email = principal.FindFirstValue("email") ?? principal.FindFirstValue(ClaimTypes.Email) ?? "";

        _user = await db.Users.SingleOrDefaultAsync(u => u.FirebaseUid == uid, ct);
        if (_user is null)
        {
            var isSeedAdmin = authOptions.Value.AdminEmails.Contains(email, StringComparer.OrdinalIgnoreCase);
            _user = new User { FirebaseUid = uid, Email = email, Role = isSeedAdmin ? Role.Admin : Role.ExternalClient };
            db.Users.Add(_user);
            await db.SaveChangesAsync(ct);
        }
        else if (_user.Email != email && email != "")
        {
            _user.Email = email;
            await db.SaveChangesAsync(ct);
        }
        return _user;
    }

    public async Task<User> RequireAsync(CancellationToken ct = default) =>
        await GetAsync(ct) ?? throw new UnauthorizedAccessException();
}
