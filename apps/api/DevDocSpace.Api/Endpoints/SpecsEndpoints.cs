using DevDocSpace.Api.Auth;
using DevDocSpace.Api.Content;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocSpace.Api.Endpoints;

public static class SpecsEndpoints
{
    public static IEndpointRouteBuilder MapSpecs(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/specs").RequireAuthorization(Policies.Authenticated);

        group.MapGet("/", async (AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            var user = await current.RequireAsync(ct);
            var specs = await db.ApiSpecs.Include(s => s.Environments)
                .OrderBy(s => s.Service).ThenBy(s => s.Version)
                .ToListAsync(ct);
            var visible = specs
                .Where(s => user.Role.Satisfies(s.RequiredRole))
                .Select(s => new
                {
                    s.Id, s.Service, s.Version,
                    RequiredRole = s.RequiredRole.ToString(),
                    Environments = s.Environments.OrderBy(e => e.Name).Select(e => e.Name.ToString()),
                });
            return Results.Ok(visible);
        });

        group.MapGet("/{service}/{version}", async (string service, string version, AppDbContext db, IContentStore store, CurrentUser current, CancellationToken ct) =>
        {
            var user = await current.RequireAsync(ct);
            var spec = await db.ApiSpecs.SingleOrDefaultAsync(s => s.Service == service && s.Version == version, ct);
            if (spec is null || !user.Role.Satisfies(spec.RequiredRole)) return Results.NotFound();

            var json = await store.GetSpecAsync(service, version, ct);
            return json is null ? Results.NotFound() : Results.Text(json, "application/json");
        });

        return app;
    }
}
