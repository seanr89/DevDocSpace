using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevDocSpace.Api.Auth;
using DevDocSpace.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevDocSpace.Api.Tests;

public class DevAuthTests
{
    private class DevAuthFactory(string environment = "Development", bool enabled = true) : WebApplicationFactory<Program>
    {
        private readonly string _dbName = Guid.NewGuid().ToString();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Auth:UseDevAuth", enabled ? "true" : "false");
            builder.UseSetting("Auth:AdminEmails:0", "admin@devdocspace.local");
            builder.UseSetting("Auth:DevUsers:dev@devdocspace.local", "InternalDeveloper");
            builder.UseSetting("Content:RootPath", Path.GetTempPath());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                foreach (var d in services.Where(d => d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true).ToList())
                    services.Remove(d);
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));
            });
        }
    }

    private static HttpClient ClientAs(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add(DevAuthHandler.HeaderName, email);
        return client;
    }

    private static async Task<JsonElement> Me(HttpClient client)
    {
        var res = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        return await res.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Header_signs_in_a_user_with_that_email()
    {
        using var factory = new DevAuthFactory();
        var me = await Me(ClientAs(factory, "client@devdocspace.local"));
        Assert.Equal("client@devdocspace.local", me.GetProperty("email").GetString());
        Assert.Equal("ExternalClient", me.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Admin_emails_still_seed_the_admin_role()
    {
        using var factory = new DevAuthFactory();
        var me = await Me(ClientAs(factory, "admin@devdocspace.local"));
        Assert.Equal("Admin", me.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Dev_users_config_seeds_the_configured_role_on_first_sign_in()
    {
        using var factory = new DevAuthFactory();
        var me = await Me(ClientAs(factory, "dev@devdocspace.local"));
        Assert.Equal("InternalDeveloper", me.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Dev_users_config_does_not_overwrite_an_existing_role()
    {
        using var factory = new DevAuthFactory();
        var admin = ClientAs(factory, "admin@devdocspace.local");
        var dev = ClientAs(factory, "dev@devdocspace.local");
        var id = (await Me(dev)).GetProperty("id").GetString();

        var res = await admin.PutAsJsonAsync($"/api/v1/admin/users/{id}/role", new { role = "ExternalClient" });
        Assert.Equal(HttpStatusCode.NoContent, res.StatusCode);

        Assert.Equal("ExternalClient", (await Me(dev)).GetProperty("role").GetString());
    }

    [Fact]
    public async Task Requests_without_the_header_are_unauthorized()
    {
        using var factory = new DevAuthFactory();
        var res = await factory.CreateClient().GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public async Task Header_is_ignored_when_dev_auth_is_disabled()
    {
        using var factory = new DevAuthFactory(enabled: false);
        var res = await ClientAs(factory, "admin@devdocspace.local").GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.Unauthorized, res.StatusCode);
    }

    [Fact]
    public void Refuses_to_start_outside_development()
    {
        using var factory = new DevAuthFactory(environment: "Production");
        var ex = Assert.ThrowsAny<Exception>(() => factory.CreateClient());
        Assert.Contains("Auth:UseDevAuth", ex.ToString());
    }
}
