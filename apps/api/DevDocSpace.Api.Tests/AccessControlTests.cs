using System.Net;
using System.Net.Http.Json;
using DevDocSpace.Data.Entities;

namespace DevDocSpace.Api.Tests;

public class AccessControlTests : IDisposable
{
    private readonly TestAppFactory _app = new();

    public void Dispose() => _app.Dispose();

    [Fact]
    public async Task Unauthenticated_request_is_rejected()
    {
        var res = await _app.CreateClient().GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task First_login_creates_external_client_user()
    {
        var client = _app.CreateClientAs("uid-1", "someone@example.com");
        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/me");
        Assert.Equal("ExternalClient", me!.Role);
        Assert.Equal("someone@example.com", me.Email);
    }

    [Fact]
    public async Task Seeded_admin_email_gets_admin_role()
    {
        var client = _app.CreateClientAs("uid-admin", "admin@example.com");
        var me = await client.GetFromJsonAsync<MeResponse>("/api/v1/me");
        Assert.Equal("Admin", me!.Role);
    }

    [Fact]
    public async Task Admin_endpoints_reject_non_admins()
    {
        var client = _app.CreateClientAs("uid-2", "dev@example.com");
        var res = await client.GetAsync("/api/v1/admin/users");
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    [Fact]
    public async Task Docs_namespace_is_hidden_when_role_insufficient()
    {
        _app.WriteContent("docs/public/index.md", "# Public");
        _app.WriteContent("docs/internal/index.md", "# Internal");
        await _app.WithDb(async db =>
        {
            db.DocNamespaces.Add(new DocNamespace { Slug = "internal", RequiredRole = Role.InternalDeveloper });
            await db.SaveChangesAsync();
        });

        var external = _app.CreateClientAs("ext", "ext@example.com");
        var namespaces = await external.GetFromJsonAsync<List<NamespaceResponse>>("/api/v1/docs/namespaces");
        Assert.Equal(["public"], namespaces!.Select(n => n.Slug));

        var hidden = await external.GetAsync("/api/v1/docs/internal/index");
        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        var visible = await external.GetStringAsync("/api/v1/docs/public/index");
        Assert.Equal("# Public", visible);
    }

    [Fact]
    public async Task Specs_are_filtered_by_required_role()
    {
        _app.WriteContent("specs/petstore/v1/openapi.json", "{\"openapi\":\"3.0.0\"}");
        await _app.SeedUser("dev", "dev@example.com", Role.InternalDeveloper);
        await _app.WithDb(async db =>
        {
            db.ApiSpecs.Add(new ApiSpec { Service = "petstore", Version = "v1", Path = "specs/petstore/v1/openapi.json", RequiredRole = Role.InternalDeveloper });
            await db.SaveChangesAsync();
        });

        var external = _app.CreateClientAs("ext", "ext@example.com");
        var extSpecs = await external.GetFromJsonAsync<List<SpecResponse>>("/api/v1/specs");
        Assert.Empty(extSpecs!);
        Assert.Equal(HttpStatusCode.NotFound, (await external.GetAsync("/api/v1/specs/petstore/v1")).StatusCode);

        var dev = _app.CreateClientAs("dev", "dev@example.com");
        var devSpecs = await dev.GetFromJsonAsync<List<SpecResponse>>("/api/v1/specs");
        Assert.Single(devSpecs!);
        var json = await dev.GetStringAsync("/api/v1/specs/petstore/v1");
        Assert.Contains("3.0.0", json);
    }

    [Fact]
    public async Task Doc_paths_cannot_escape_namespace()
    {
        _app.WriteContent("docs/public/index.md", "# Public");
        _app.WriteContent("secret.md", "# Secret");
        var client = _app.CreateClientAs("ext", "ext@example.com");
        var res = await client.GetAsync("/api/v1/docs/public/..%2F..%2Fsecret");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    private record MeResponse(Guid Id, string Email, string Role);
    private record NamespaceResponse(string Slug, string Title);
    private record SpecResponse(Guid Id, string Service, string Version, string RequiredRole);
}
