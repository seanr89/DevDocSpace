using System.Security.Claims;
using System.Text.Encodings.Web;
using DevDocSpace.Api.Content;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevDocSpace.Api.Tests;

public class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = Guid.NewGuid().ToString();
    public string ContentRoot { get; } = Directory.CreateTempSubdirectory("dds-content").FullName;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Content:RootPath", ContentRoot);
        builder.UseSetting("Auth:AdminEmails:0", "admin@example.com");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            foreach (var d in services.Where(d => d.ServiceType.FullName?.Contains("IDbContextOptionsConfiguration") == true).ToList())
                services.Remove(d);
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(o => o.UseInMemoryDatabase(_dbName));

            services.AddTransient<FakeFirebaseHandler>();
            services.PostConfigure<AuthenticationOptions>(o =>
                o.SchemeMap[JwtBearerDefaults.AuthenticationScheme].HandlerType = typeof(FakeFirebaseHandler));
        });
    }

    public HttpClient CreateClientAs(string uid, string email)
    {
        var client = CreateClient();
        client.DefaultRequestHeaders.Add(FakeFirebaseHandler.UidHeader, uid);
        client.DefaultRequestHeaders.Add(FakeFirebaseHandler.EmailHeader, email);
        return client;
    }

    public async Task<T> WithDb<T>(Func<AppDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task WithDb(Func<AppDbContext, Task> action) => WithDb(async db => { await action(db); return 0; });

    public async Task<User> SeedUser(string uid, string email, Role role) =>
        await WithDb(async db =>
        {
            var u = new User { FirebaseUid = uid, Email = email, Role = role };
            db.Users.Add(u);
            await db.SaveChangesAsync();
            return u;
        });

    public void WriteContent(string relativePath, string content)
    {
        var full = Path.Combine(ContentRoot, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(ContentRoot)) Directory.Delete(ContentRoot, true);
    }
}

public class FakeFirebaseHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string UidHeader = "X-Test-Uid";
    public const string EmailHeader = "X-Test-Email";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(UidHeader, out var uid)) return Task.FromResult(AuthenticateResult.NoResult());
        var identity = new ClaimsIdentity(
        [
            new Claim("sub", uid!),
            new Claim("email", Request.Headers[EmailHeader].ToString()),
        ], Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
