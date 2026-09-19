using DevDocSpace.Api.Auth;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocSpace.Api.Endpoints;

public record CreateApiKeyRequest(string Name);

public static class ApiKeyEndpoints
{
    public static IEndpointRouteBuilder MapApiKeys(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/apikeys").RequireAuthorization(Policies.Authenticated);

        group.MapGet("/", async (AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            var user = await current.RequireAsync(ct);
            var keys = await db.ApiKeys.Where(k => k.UserId == user.Id)
                .OrderByDescending(k => k.CreatedAt)
                .Select(k => new { k.Id, k.Name, k.Prefix, k.CreatedAt, k.RevokedAt })
                .ToListAsync(ct);
            return Results.Ok(keys);
        });

        group.MapPost("/", async (CreateApiKeyRequest req, AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(req.Name)) return Results.BadRequest(new { error = "Name is required" });
            var user = await current.RequireAsync(ct);
            var (plain, prefix, hash) = ApiKeyHasher.Generate();
            var key = new ApiKey { UserId = user.Id, Name = req.Name.Trim(), Prefix = prefix, KeyHash = hash };
            db.ApiKeys.Add(key);
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/apikeys/{key.Id}", new { key.Id, key.Name, key.Prefix, key.CreatedAt, Key = plain });
        });

        group.MapDelete("/{id:guid}", async (Guid id, AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            var user = await current.RequireAsync(ct);
            var key = await db.ApiKeys.SingleOrDefaultAsync(k => k.Id == id && k.UserId == user.Id, ct);
            if (key is null) return Results.NotFound();
            key.RevokedAt ??= DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/calls", async (AppDbContext db, CurrentUser current, int? take, CancellationToken ct) =>
        {
            var user = await current.RequireAsync(ct);
            var limit = Math.Clamp(take ?? 50, 1, 500);
            var calls = await db.ApiCallLogs.Where(l => l.UserId == user.Id)
                .OrderByDescending(l => l.At).Take(limit)
                .Join(db.ApiSpecs, l => l.SpecId, s => s.Id, (l, s) => new
                {
                    l.Id, s.Service, s.Version, Environment = l.Environment.ToString(),
                    l.Method, l.Path, l.StatusCode, l.DurationMs, l.At,
                })
                .ToListAsync(ct);
            return Results.Ok(calls);
        });

        return app;
    }
}
