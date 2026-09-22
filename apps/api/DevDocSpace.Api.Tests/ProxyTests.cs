using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using DevDocSpace.Api.Auth;
using DevDocSpace.Data.Entities;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace DevDocSpace.Api.Tests;

public class ProxyTests : IAsyncLifetime
{
    private readonly TestAppFactory _app = new();
    private WebApplication _upstream = null!;
    private string _upstreamUrl = null!;

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _upstream = builder.Build();
        _upstream.Map("/{**path}", async (HttpContext ctx, string? path) =>
        {
            var body = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
            ctx.Response.Headers["X-Upstream"] = "yes";
            ctx.Response.Headers["Set-Cookie"] = "leak=1";
            await ctx.Response.WriteAsJsonAsync(new
            {
                method = ctx.Request.Method,
                path = "/" + (path ?? ""),
                query = ctx.Request.QueryString.Value,
                headers = ctx.Request.Headers.ToDictionary(h => h.Key, h => h.Value.ToString()),
                body,
            });
        });
        await _upstream.StartAsync();
        _upstreamUrl = _upstream.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();

        await _app.SeedUser("dev", "dev@example.com", Role.InternalDeveloper);
        await _app.WithDb(async db =>
        {
            db.ApiSpecs.Add(new ApiSpec
            {
                Service = "echo", Version = "v1", Path = "specs/echo/v1/openapi.json",
                RequiredRole = Role.InternalDeveloper,
                Environments =
                [
                    new ServiceEnvironment { Name = ApiEnvironment.Sandbox, BaseUrl = _upstreamUrl + "/sandbox", CredentialKey = "echo-sandbox" },
                ],
            });
            await db.SaveChangesAsync();
        });
    }

    public async Task DisposeAsync()
    {
        await _upstream.StopAsync();
        await _upstream.DisposeAsync();
        _app.Dispose();
    }

    private HttpClient DevClient() => _app.CreateClientAs("dev", "dev@example.com");

    [Fact]
    public async Task Forwards_method_path_query_and_body()
    {
        var res = await DevClient().PostAsJsonAsync("/api/v1/proxy/echo/v1/sandbox/pets/42?x=1", new { name = "rex" });
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var echo = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("POST", echo.GetProperty("method").GetString());
        Assert.Equal("/sandbox/pets/42", echo.GetProperty("path").GetString());
        Assert.Equal("?x=1", echo.GetProperty("query").GetString());
        Assert.Contains("rex", echo.GetProperty("body").GetString());
    }

    [Fact]
    public async Task Strips_caller_auth_headers_and_injects_upstream_credential()
    {
        using var factory = new CredentialedFactory();
        await factory.SeedUser("dev", "dev@example.com", Role.InternalDeveloper);
        await factory.WithDb(async db =>
        {
            db.ApiSpecs.Add(new ApiSpec
            {
                Service = "echo", Version = "v1", Path = "x", RequiredRole = Role.InternalDeveloper,
                Environments = [new ServiceEnvironment { Name = ApiEnvironment.Sandbox, BaseUrl = _upstreamUrl, CredentialKey = "echo-sandbox" }],
            });
            await db.SaveChangesAsync();
        });

        var client = factory.CreateClientAs("dev", "dev@example.com");
        client.DefaultRequestHeaders.Add("Cookie", "session=abc");
        client.DefaultRequestHeaders.Add(DevAuthHandler.HeaderName, "dev@example.com");
        var res = await client.GetAsync("/api/v1/proxy/echo/v1/sandbox/whoami");
        var echo = await res.Content.ReadFromJsonAsync<JsonElement>();
        var headers = echo.GetProperty("headers");

        Assert.Equal("Bearer upstream-secret", headers.GetProperty("Authorization").GetString());
        Assert.False(headers.TryGetProperty("Cookie", out _));
        Assert.False(headers.TryGetProperty(DevAuthHandler.HeaderName, out _));
        Assert.False(res.Headers.Contains("Set-Cookie"));
        Assert.Equal("yes", res.Headers.GetValues("X-Upstream").Single());
    }

    [Fact]
    public async Task Unknown_environment_or_service_returns_404()
    {
        var client = DevClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/proxy/echo/v1/production/pets")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/v1/proxy/nope/v1/sandbox/pets")).StatusCode);
    }

    [Fact]
    public async Task Insufficient_role_returns_404()
    {
        var res = await _app.CreateClientAs("ext", "ext@example.com").GetAsync("/api/v1/proxy/echo/v1/sandbox/pets");
        Assert.Equal(HttpStatusCode.NotFound, res.StatusCode);
    }

    [Fact]
    public async Task Calls_are_logged()
    {
        await DevClient().GetAsync("/api/v1/proxy/echo/v1/sandbox/pets");
        var log = await _app.WithDb(db => db.ApiCallLogs.SingleAsync());
        Assert.Equal("GET", log.Method);
        Assert.Equal("/pets", log.Path);
        Assert.Equal(200, log.StatusCode);
        Assert.Equal(ApiEnvironment.Sandbox, log.Environment);

        var calls = await DevClient().GetFromJsonAsync<JsonElement>("/api/v1/apikeys/calls");
        Assert.Equal(1, calls.GetArrayLength());
    }

    [Fact]
    public async Task Api_key_can_authenticate_proxy_calls()
    {
        var created = await DevClient().PostAsJsonAsync("/api/v1/apikeys", new { name = "ci" });
        var key = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("key").GetString()!;

        var anon = _app.CreateClient();
        anon.DefaultRequestHeaders.Add("X-Api-Key", key);
        var res = await anon.GetAsync("/api/v1/proxy/echo/v1/sandbox/pets");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);

        anon.DefaultRequestHeaders.Remove("X-Api-Key");
        anon.DefaultRequestHeaders.Add("X-Api-Key", "dds_invalid");
        Assert.Equal(HttpStatusCode.Unauthorized, (await anon.GetAsync("/api/v1/proxy/echo/v1/sandbox/pets")).StatusCode);
    }

    private class CredentialedFactory : TestAppFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Proxy:Credentials:echo-sandbox:Header", "Authorization");
            builder.UseSetting("Proxy:Credentials:echo-sandbox:Value", "Bearer upstream-secret");
        }
    }
}
