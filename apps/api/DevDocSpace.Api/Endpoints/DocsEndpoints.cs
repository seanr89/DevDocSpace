using DevDocSpace.Api.Auth;
using DevDocSpace.Api.Content;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocSpace.Api.Endpoints;

public static class DocsEndpoints
{
    public static IEndpointRouteBuilder MapDocs(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/docs").RequireAuthorization(Policies.Authenticated);

        group.MapGet("/namespaces", async (IContentStore store, AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            var user = await current.RequireAsync(ct);
            var slugs = await store.ListNamespacesAsync(ct);
            var policies = await db.DocNamespaces.ToDictionaryAsync(n => n.Slug, ct);
            var visible = slugs
                .Where(s => user.Role.Satisfies(RequiredRole(policies, s)))
                .Select(s => new { Slug = s, Title = policies.GetValueOrDefault(s)?.Title ?? s });
            return Results.Ok(visible);
        });

        group.MapGet("/{ns}/tree", async (string ns, IContentStore store, AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            if (!await CanAccess(ns, db, current, ct)) return Results.NotFound();
            return Results.Ok(await store.GetDocTreeAsync(ns, ct));
        });

        group.MapGet("/{ns}/{*path}", async (string ns, string path, IContentStore store, AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            if (!await CanAccess(ns, db, current, ct)) return Results.NotFound();
            var content = await store.GetDocAsync(ns, path, ct);
            return content is null ? Results.NotFound() : Results.Text(content, "text/markdown");
        });

        return app;
    }

    private static Role RequiredRole(IDictionary<string, DocNamespace> policies, string slug) =>
        policies.TryGetValue(slug, out var n) ? n.RequiredRole : Role.ExternalClient;

    private static async Task<bool> CanAccess(string ns, AppDbContext db, CurrentUser current, CancellationToken ct)
    {
        var user = await current.RequireAsync(ct);
        var policy = await db.DocNamespaces.SingleOrDefaultAsync(n => n.Slug == ns, ct);
        return user.Role.Satisfies(policy?.RequiredRole ?? Role.ExternalClient);
    }
}
