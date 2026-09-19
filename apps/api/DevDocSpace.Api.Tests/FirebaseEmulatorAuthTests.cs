using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using DevDocSpace.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace DevDocSpace.Api.Tests;

public class FirebaseEmulatorAuthTests
{
    private const string ProjectId = "demo-project";

    private class EmulatorFactory : WebApplicationFactory<Program>
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.UseSetting("Auth:FirebaseProjectId", ProjectId);
            builder.UseSetting("Auth:UseFirebaseEmulator", "true");
            builder.UseSetting("Content:RootPath", Path.GetTempPath());
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<AppDbContext>>();
                foreach (var d in services.Where(d => d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true).ToList())
                    services.Remove(d);
                services.RemoveAll<AppDbContext>();
                services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(Guid.NewGuid().ToString()));
            });
        }
    }

    private static string UnsignedToken(string issuer, string audience, long exp)
    {
        static string B64(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s)).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var header = B64("""{"alg":"none","typ":"JWT"}""");
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var payload = B64(JsonSerializer.Serialize(new
        {
            iss = issuer, aud = audience, sub = "emu-user-1", user_id = "emu-user-1",
            email = "emu@example.com", iat = now, auth_time = now, exp,
        }));
        return $"{header}.{payload}.";
    }

    [Fact]
    public async Task Accepts_unsigned_emulator_token_with_correct_issuer_and_audience()
    {
        using var factory = new EmulatorFactory();
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer",
            UnsignedToken($"https://securetoken.google.com/{ProjectId}", ProjectId, DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds()));

        var res = await client.GetAsync("/api/v1/me");
        Assert.Equal(HttpStatusCode.OK, res.StatusCode);
        var me = await res.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("emu@example.com", me.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Rejects_wrong_audience_and_expired_tokens()
    {
        using var factory = new EmulatorFactory();
        var issuer = $"https://securetoken.google.com/{ProjectId}";
        var future = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds();
        var past = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();

        foreach (var token in new[] { UnsignedToken(issuer, "other-project", future), UnsignedToken(issuer, ProjectId, past) })
        {
            var client = factory.CreateClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/me")).StatusCode);
        }
    }
}
