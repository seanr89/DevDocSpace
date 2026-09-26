using System.Net;
using System.Net.Http.Json;
using DevDocSpace.Data.Entities;

namespace DevDocSpace.Api.Tests;

public class AdminContentTests : IDisposable
{
    private const string PetstoreFile = "{\"openapi\":\"3.0.0\",\"info\":{\"title\":\"Petstore from file\",\"version\":\"1\"}}";
    private const string PetstoreEdited = "{\"openapi\":\"3.0.0\",\"info\":{\"title\":\"Petstore edited\",\"version\":\"1\"}}";

    private readonly TestAppFactory _app = new();

    public void Dispose() => _app.Dispose();

    private HttpClient Admin() => _app.CreateClientAs("uid-admin", "admin@example.com");

    private async Task RegisterIngestedPetstore()
    {
        _app.WriteContent("specs/petstore/v1/openapi.json", PetstoreFile);
        var res = await Admin().PostAsync("/api/v1/admin/specs/sync", null);
        res.EnsureSuccessStatusCode();
    }

    // ---- Specs ----

    [Fact]
    public async Task Created_spec_is_listed_and_served()
    {
        var admin = Admin();
        var res = await admin.PostAsJsonAsync("/api/v1/admin/specs",
            new { service = "demo", version = "v1", requiredRole = "ExternalClient", content = PetstoreEdited });
        Assert.Equal(HttpStatusCode.Created, res.StatusCode);

        var external = _app.CreateClientAs("ext", "ext@example.com");
        var specs = await external.GetFromJsonAsync<List<SpecResponse>>("/api/v1/specs");
        Assert.Equal(["demo"], specs!.Select(s => s.Service));
        Assert.Contains("Petstore edited", await external.GetStringAsync("/api/v1/specs/demo/v1"));

        var adminSpecs = await admin.GetFromJsonAsync<List<AdminSpecResponse>>("/api/v1/admin/specs");
        var demo = Assert.Single(adminSpecs!);
        Assert.Equal("managed", demo.Source);
        Assert.Null(demo.Path);
        Assert.NotNull(demo.ContentUpdatedAt);
    }

    [Theory]
    [InlineData("demo", "v1", "not json")]
    [InlineData("demo", "v1", "{\"info\":{\"title\":\"x\"}}")]
    [InlineData("demo", "v1", "{\"openapi\":\"3.0.0\"}")]
    [InlineData("demo", "v1", "[]")]
    [InlineData("../evil", "v1", PetstoreFile)]
    [InlineData("Demo", "v1", PetstoreFile)]
    [InlineData("demo", "", PetstoreFile)]
    public async Task Invalid_spec_is_rejected(string service, string version, string content)
    {
        var res = await Admin().PostAsJsonAsync("/api/v1/admin/specs",
            new { service, version, requiredRole = "InternalDeveloper", content });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Duplicate_spec_is_a_conflict()
    {
        await RegisterIngestedPetstore();
        var res = await Admin().PostAsJsonAsync("/api/v1/admin/specs",
            new { service = "petstore", version = "v1", requiredRole = "InternalDeveloper", content = PetstoreEdited });
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Override_of_ingested_spec_is_served_and_can_be_reverted()
    {
        await RegisterIngestedPetstore();
        var admin = Admin();

        Assert.Contains("Petstore from file", await admin.GetStringAsync("/api/v1/admin/specs/petstore/v1/content"));

        var put = await admin.PutAsJsonAsync("/api/v1/admin/specs/petstore/v1/content", new { content = PetstoreEdited });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);
        Assert.Contains("Petstore edited", await admin.GetStringAsync("/api/v1/specs/petstore/v1"));
        Assert.Equal("overridden", (await admin.GetFromJsonAsync<List<AdminSpecResponse>>("/api/v1/admin/specs"))!.Single().Source);

        var revert = await admin.DeleteAsync("/api/v1/admin/specs/petstore/v1/content");
        Assert.Equal(HttpStatusCode.NoContent, revert.StatusCode);
        Assert.Contains("Petstore from file", await admin.GetStringAsync("/api/v1/specs/petstore/v1"));
        Assert.Equal("ingested", (await admin.GetFromJsonAsync<List<AdminSpecResponse>>("/api/v1/admin/specs"))!.Single().Source);
    }

    [Fact]
    public async Task Invalid_spec_edit_is_rejected()
    {
        await RegisterIngestedPetstore();
        var res = await Admin().PutAsJsonAsync("/api/v1/admin/specs/petstore/v1/content", new { content = "{" });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Editing_unknown_spec_is_not_found()
    {
        var res = await Admin().PutAsJsonAsync("/api/v1/admin/specs/nope/v1/content", new { content = PetstoreEdited });
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Reverting_managed_spec_is_a_conflict()
    {
        var admin = Admin();
        await admin.PostAsJsonAsync("/api/v1/admin/specs",
            new { service = "demo", version = "v1", requiredRole = "ExternalClient", content = PetstoreEdited });
        var res = await admin.DeleteAsync("/api/v1/admin/specs/demo/v1/content");
        Assert.Equal(HttpStatusCode.Conflict, res.StatusCode);
    }

    [Fact]
    public async Task Sync_keeps_managed_spec_content()
    {
        var admin = Admin();
        await admin.PostAsJsonAsync("/api/v1/admin/specs",
            new { service = "petstore", version = "v1", requiredRole = "ExternalClient", content = PetstoreEdited });
        _app.WriteContent("specs/petstore/v1/openapi.json", PetstoreFile);
        (await admin.PostAsync("/api/v1/admin/specs/sync", null)).EnsureSuccessStatusCode();

        var spec = (await admin.GetFromJsonAsync<List<AdminSpecResponse>>("/api/v1/admin/specs"))!.Single();
        Assert.Equal("overridden", spec.Source);
        Assert.Contains("Petstore edited", await admin.GetStringAsync("/api/v1/specs/petstore/v1"));
    }

    // ---- Docs ----

    [Fact]
    public async Task New_page_in_new_namespace_is_visible()
    {
        var admin = Admin();
        var put = await admin.PutAsJsonAsync("/api/v1/admin/docs/guides/pages/auth/tokens", new { markdown = "# Tokens" });
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var external = _app.CreateClientAs("ext", "ext@example.com");
        var namespaces = await external.GetFromJsonAsync<List<NamespaceResponse>>("/api/v1/docs/namespaces");
        Assert.Contains("guides", namespaces!.Select(n => n.Slug));

        var tree = await external.GetFromJsonAsync<List<DocEntryResponse>>("/api/v1/docs/guides/tree");
        var dir = Assert.Single(tree!);
        Assert.True(dir.IsDirectory);
        Assert.Equal("auth", dir.Path);
        Assert.Equal("auth/tokens", Assert.Single(dir.Children).Path);

        Assert.Equal("# Tokens", await external.GetStringAsync("/api/v1/docs/guides/auth/tokens"));

        var pages = await admin.GetFromJsonAsync<List<AdminDocPageResponse>>("/api/v1/admin/docs/guides/pages");
        var page = Assert.Single(pages!);
        Assert.Equal(("auth/tokens", "managed"), (page.Path, page.Source));
    }

    [Fact]
    public async Task Override_of_file_doc_is_served_and_delete_reverts()
    {
        _app.WriteContent("docs/public/index.md", "# From file");
        _app.WriteContent("docs/public/other.md", "# Other");
        var admin = Admin();

        await admin.PutAsJsonAsync("/api/v1/admin/docs/public/pages/index", new { markdown = "# Edited" });
        Assert.Equal("# Edited", await admin.GetStringAsync("/api/v1/docs/public/index"));

        var tree = await admin.GetFromJsonAsync<List<DocEntryResponse>>("/api/v1/docs/public/tree");
        Assert.Equal(["index", "other"], tree!.Select(e => e.Path).Order());

        var pages = await admin.GetFromJsonAsync<List<AdminDocPageResponse>>("/api/v1/admin/docs/public/pages");
        Assert.Equal([("index", "overridden"), ("other", "ingested")], pages!.Select(p => (p.Path, p.Source)).Order());

        var del = await admin.DeleteAsync("/api/v1/admin/docs/public/pages/index");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        Assert.Equal("# From file", await admin.GetStringAsync("/api/v1/docs/public/index"));
    }

    [Fact]
    public async Task Deleting_ingested_only_page_is_not_found()
    {
        _app.WriteContent("docs/public/index.md", "# From file");
        var res = await Admin().DeleteAsync("/api/v1/admin/docs/public/pages/index");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Theory]
    [InlineData("/api/v1/admin/docs/public/pages/..%2F..%2Fsecret", "# x")]
    [InlineData("/api/v1/admin/docs/public/pages/Bad%20Name", "# x")]
    [InlineData("/api/v1/admin/docs/public/pages/page.md", "# x")]
    [InlineData("/api/v1/admin/docs/Public/pages/index", "# x")]
    [InlineData("/api/v1/admin/docs/public/pages/index", "   ")]
    public async Task Invalid_doc_page_is_rejected(string url, string markdown)
    {
        var res = await Admin().PutAsJsonAsync(url, new { markdown });
        Assert.Equal(HttpStatusCode.BadRequest, res.StatusCode);
    }

    [Fact]
    public async Task Namespace_policy_hides_db_pages()
    {
        var admin = Admin();
        await admin.PutAsJsonAsync("/api/v1/admin/docs/internal/pages/index", new { markdown = "# Internal" });
        await _app.WithDb(async db =>
        {
            db.DocNamespaces.Add(new DocNamespace { Slug = "internal", RequiredRole = Role.InternalDeveloper });
            await db.SaveChangesAsync();
        });

        var external = _app.CreateClientAs("ext", "ext@example.com");
        Assert.DoesNotContain("internal", (await external.GetFromJsonAsync<List<NamespaceResponse>>("/api/v1/docs/namespaces"))!.Select(n => n.Slug));
        Assert.Equal(HttpStatusCode.NotFound, (await external.GetAsync("/api/v1/docs/internal/index")).StatusCode);
    }

    // ---- Authorization ----

    [Theory]
    [InlineData("GET", "/api/v1/admin/specs/petstore/v1/content")]
    [InlineData("POST", "/api/v1/admin/specs")]
    [InlineData("PUT", "/api/v1/admin/specs/petstore/v1/content")]
    [InlineData("DELETE", "/api/v1/admin/specs/petstore/v1/content")]
    [InlineData("GET", "/api/v1/admin/docs/public/pages")]
    [InlineData("PUT", "/api/v1/admin/docs/public/pages/index")]
    [InlineData("DELETE", "/api/v1/admin/docs/public/pages/index")]
    public async Task Content_endpoints_reject_non_admins(string method, string url)
    {
        await _app.SeedUser("dev", "dev@example.com", Role.InternalDeveloper);
        var client = _app.CreateClientAs("dev", "dev@example.com");
        var req = new HttpRequestMessage(new HttpMethod(method), url);
        if (method is "POST" or "PUT") req.Content = JsonContent.Create(new { content = PetstoreEdited, markdown = "# x" });
        var res = await client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Forbidden, res.StatusCode);
    }

    private record SpecResponse(Guid Id, string Service, string Version, string RequiredRole);
    private record AdminSpecResponse(Guid Id, string Service, string Version, string? Path, string RequiredRole, string Source, DateTimeOffset? ContentUpdatedAt);
    private record NamespaceResponse(string Slug, string Title);
    private record DocEntryResponse(string Path, string Title, bool IsDirectory, List<DocEntryResponse> Children);
    private record AdminDocPageResponse(string Path, string Source, DateTimeOffset? UpdatedAt);
}
