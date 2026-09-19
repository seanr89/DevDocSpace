using DevDocSpace.Api.Auth;
using DevDocSpace.Api.Content;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocSpace.Api.Endpoints;

public record EnvironmentInput(ApiEnvironment Name, string BaseUrl, string? CredentialKey);
public record UpsertSpecRequest(Role RequiredRole, List<EnvironmentInput> Environments);
public record UpsertNamespaceRequest(string? Title, Role RequiredRole);
public record UpdateUserRoleRequest(Role Role);

public static class AdminEndpoints
{
    public static IEndpointRouteBuilder MapAdmin(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/admin").RequireAuthorization(Policies.Admin);

        group.MapGet("/specs", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.ApiSpecs.Include(s => s.Environments)
                .OrderBy(s => s.Service).ThenBy(s => s.Version)
                .Select(s => new
                {
                    s.Id, s.Service, s.Version, s.Path, RequiredRole = s.RequiredRole.ToString(),
                    Environments = s.Environments.Select(e => new { Name = e.Name.ToString(), e.BaseUrl, e.CredentialKey }),
                })
                .ToListAsync(ct)));

        group.MapPost("/specs/sync", async (AppDbContext db, IContentStore store, CancellationToken ct) =>
        {
            var onDisk = await store.ListSpecsAsync(ct);
            var existing = await db.ApiSpecs.ToDictionaryAsync(s => (s.Service, s.Version), ct);
            var added = 0;
            foreach (var entry in onDisk)
            {
                if (existing.TryGetValue((entry.Service, entry.Version), out var spec))
                {
                    spec.Path = entry.Path;
                    continue;
                }
                db.ApiSpecs.Add(new ApiSpec { Service = entry.Service, Version = entry.Version, Path = entry.Path });
                added++;
            }
            await db.SaveChangesAsync(ct);
            return Results.Ok(new { discovered = onDisk.Count, added });
        });

        group.MapPut("/specs/{service}/{version}", async (string service, string version, UpsertSpecRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var spec = await db.ApiSpecs.Include(s => s.Environments)
                .SingleOrDefaultAsync(s => s.Service == service && s.Version == version, ct);
            if (spec is null) return Results.NotFound();

            spec.RequiredRole = req.RequiredRole;
            db.ServiceEnvironments.RemoveRange(spec.Environments);
            spec.Environments = req.Environments
                .Select(e => new ServiceEnvironment { Name = e.Name, BaseUrl = e.BaseUrl, CredentialKey = e.CredentialKey })
                .ToList();
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapDelete("/specs/{service}/{version}", async (string service, string version, AppDbContext db, CancellationToken ct) =>
        {
            var deleted = await db.ApiSpecs.Where(s => s.Service == service && s.Version == version).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        });

        group.MapGet("/namespaces", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.DocNamespaces.OrderBy(n => n.Slug)
                .Select(n => new { n.Id, n.Slug, n.Title, RequiredRole = n.RequiredRole.ToString() })
                .ToListAsync(ct)));

        group.MapPut("/namespaces/{slug}", async (string slug, UpsertNamespaceRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var ns = await db.DocNamespaces.SingleOrDefaultAsync(n => n.Slug == slug, ct);
            if (ns is null)
            {
                ns = new DocNamespace { Slug = slug };
                db.DocNamespaces.Add(ns);
            }
            ns.Title = req.Title;
            ns.RequiredRole = req.RequiredRole;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapDelete("/namespaces/{slug}", async (string slug, AppDbContext db, CancellationToken ct) =>
        {
            var deleted = await db.DocNamespaces.Where(n => n.Slug == slug).ExecuteDeleteAsync(ct);
            return deleted == 0 ? Results.NotFound() : Results.NoContent();
        });

        group.MapGet("/users", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Users.OrderBy(u => u.Email)
                .Select(u => new { u.Id, u.Email, Role = u.Role.ToString(), u.CreatedAt })
                .ToListAsync(ct)));

        group.MapPut("/users/{id:guid}/role", async (Guid id, UpdateUserRoleRequest req, AppDbContext db, CancellationToken ct) =>
        {
            var user = await db.Users.FindAsync([id], ct);
            if (user is null) return Results.NotFound();
            user.Role = req.Role;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return app;
    }
}
