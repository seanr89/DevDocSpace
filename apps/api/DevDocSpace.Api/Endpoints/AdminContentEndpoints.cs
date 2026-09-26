using DevDocSpace.Api.Auth;
using DevDocSpace.Api.Content;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace DevDocSpace.Api.Endpoints;

public record CreateSpecRequest(string Service, string Version, Role RequiredRole, string Content);
public record SpecContentRequest(string Content);
public record DocPageRequest(string Markdown);

// Admin authoring of spec JSON and Markdown pages. Content is stored in the database and overlays the file store
// (see OverlayContentStore); "source" tells the UI whether an item is ingested, managed (DB only) or overridden.
public static class AdminContentEndpoints
{
    public const string Ingested = "ingested";
    public const string Managed = "managed";
    public const string Overridden = "overridden";

    public static string SpecSource(bool hasContent, string? path) =>
        !hasContent ? Ingested : path is null ? Managed : Overridden;

    public static RouteGroupBuilder MapAdminContent(this RouteGroupBuilder group)
    {
        group.MapPost("/specs", async (CreateSpecRequest req, AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            if (!ContentValidation.IsSlug(req.Service) || !ContentValidation.IsSlug(req.Version))
                return Results.BadRequest("Service and version must be lowercase slugs (a-z, 0-9, '.', '_', '-').");
            if (ContentValidation.ValidateSpec(req.Content) is { } error) return Results.BadRequest(error);
            if (await db.ApiSpecs.AnyAsync(s => s.Service == req.Service && s.Version == req.Version, ct))
                return Results.Conflict($"Spec {req.Service}/{req.Version} already exists.");

            var user = await current.RequireAsync(ct);
            db.ApiSpecs.Add(new ApiSpec
            {
                Service = req.Service,
                Version = req.Version,
                RequiredRole = req.RequiredRole,
                Content = req.Content,
                ContentUpdatedAt = DateTimeOffset.UtcNow,
                ContentUpdatedById = user.Id,
            });
            await db.SaveChangesAsync(ct);
            return Results.Created($"/api/v1/specs/{req.Service}/{req.Version}", null);
        });

        group.MapGet("/specs/{service}/{version}/content", async (string service, string version, AppDbContext db, IContentStore store, CancellationToken ct) =>
        {
            if (!await db.ApiSpecs.AnyAsync(s => s.Service == service && s.Version == version, ct)) return Results.NotFound();
            var json = await store.GetSpecAsync(service, version, ct);
            return json is null ? Results.NotFound() : Results.Text(json, "application/json");
        });

        group.MapPut("/specs/{service}/{version}/content", async (string service, string version, SpecContentRequest req, AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            var spec = await db.ApiSpecs.SingleOrDefaultAsync(s => s.Service == service && s.Version == version, ct);
            if (spec is null) return Results.NotFound();
            if (ContentValidation.ValidateSpec(req.Content) is { } error) return Results.BadRequest(error);

            var user = await current.RequireAsync(ct);
            spec.Content = req.Content;
            spec.ContentUpdatedAt = DateTimeOffset.UtcNow;
            spec.ContentUpdatedById = user.Id;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapDelete("/specs/{service}/{version}/content", async (string service, string version, AppDbContext db, CancellationToken ct) =>
        {
            var spec = await db.ApiSpecs.SingleOrDefaultAsync(s => s.Service == service && s.Version == version, ct);
            if (spec is null || spec.Content is null) return Results.NotFound();
            if (spec.Path is null)
                return Results.Conflict("This spec exists only in the portal; delete the spec instead of reverting it.");

            spec.Content = null;
            spec.ContentUpdatedAt = null;
            spec.ContentUpdatedById = null;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapGet("/docs/{ns}/pages", async (string ns, AppDbContext db, FileSystemContentStore files, CancellationToken ct) =>
        {
            var filePaths = Flatten(await files.GetDocTreeAsync(ns, ct)).ToHashSet();
            var dbPages = await db.DocPages.Where(p => p.Namespace == ns)
                .Select(p => new { p.Path, p.UpdatedAt })
                .ToDictionaryAsync(p => p.Path, p => p.UpdatedAt, ct);

            var pages = filePaths.Union(dbPages.Keys).Order(StringComparer.Ordinal).Select(path => new
            {
                Path = path,
                Source = !dbPages.ContainsKey(path) ? Ingested : filePaths.Contains(path) ? Overridden : Managed,
                UpdatedAt = dbPages.TryGetValue(path, out var at) ? at : (DateTimeOffset?)null,
            });
            return Results.Ok(pages);
        });

        group.MapPut("/docs/{ns}/pages/{*path}", async (string ns, string path, DocPageRequest req, AppDbContext db, CurrentUser current, CancellationToken ct) =>
        {
            if (!ContentValidation.IsSlug(ns) || !ContentValidation.IsDocPath(path))
                return Results.BadRequest("Namespace and page path must be lowercase slugs separated by '/', without a file extension.");
            if (ContentValidation.ValidateMarkdown(req.Markdown) is { } error) return Results.BadRequest(error);

            var user = await current.RequireAsync(ct);
            var page = await db.DocPages.SingleOrDefaultAsync(p => p.Namespace == ns && p.Path == path, ct);
            if (page is null)
            {
                page = new DocPage { Namespace = ns, Path = path, Markdown = req.Markdown };
                db.DocPages.Add(page);
            }
            page.Markdown = req.Markdown;
            page.UpdatedAt = DateTimeOffset.UtcNow;
            page.UpdatedById = user.Id;
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        group.MapDelete("/docs/{ns}/pages/{*path}", async (string ns, string path, AppDbContext db, CancellationToken ct) =>
        {
            var page = await db.DocPages.SingleOrDefaultAsync(p => p.Namespace == ns && p.Path == path, ct);
            if (page is null) return Results.NotFound();
            db.DocPages.Remove(page);
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });

        return group;
    }

    private static IEnumerable<string> Flatten(IEnumerable<DocEntry> entries) =>
        entries.SelectMany(e => e.IsDirectory ? Flatten(e.Children) : [e.Path]);
}
