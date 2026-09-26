using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using DevDocSpace.Api.Auth;
using DevDocSpace.Api.Content;
using DevDocSpace.Api.Endpoints;
using DevDocSpace.Api.Proxy;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

builder.Services.Configure<AuthOptions>(config.GetSection(AuthOptions.Section));
builder.Services.Configure<ContentOptions>(config.GetSection(ContentOptions.Section));
builder.Services.Configure<ProxyOptions>(config.GetSection(ProxyOptions.Section));
var authOptions = config.GetSection(AuthOptions.Section).Get<AuthOptions>() ?? new AuthOptions();
var proxyOptions = config.GetSection(ProxyOptions.Section).Get<ProxyOptions>() ?? new ProxyOptions();

builder.Services.ConfigureHttpJsonOptions(o => o.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(config.GetConnectionString("Default")));

builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<CurrentUser>();
builder.Services.AddSingleton<FileSystemContentStore>();
builder.Services.AddScoped<IContentStore, OverlayContentStore>();
builder.Services.AddScoped<ProxyForwarder>();
builder.Services.AddHttpClient(ProxyOptions.HttpClientName, c => c.Timeout = TimeSpan.FromSeconds(proxyOptions.TimeoutSeconds))
    .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        AutomaticDecompression = System.Net.DecompressionMethods.None,
    });

builder.Services.AddAuthentication(o =>
    {
        o.DefaultScheme = "Smart";
        o.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddPolicyScheme("Smart", "Firebase JWT, API key or dev header", o =>
        o.ForwardDefaultSelector = ctx =>
            ctx.Request.Headers.ContainsKey(ApiKeyAuthenticationHandler.HeaderName)
                ? ApiKeyAuthenticationHandler.SchemeName
                : authOptions.UseDevAuth && ctx.Request.Headers.ContainsKey(DevAuthHandler.HeaderName)
                    ? DevAuthHandler.SchemeName
                    : JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        var issuer = $"https://securetoken.google.com/{authOptions.FirebaseProjectId}";
        o.MapInboundClaims = false;
        o.TokenValidationParameters = new()
        {
            ValidIssuer = issuer,
            ValidAudience = authOptions.FirebaseProjectId,
            ValidateIssuerSigningKey = true,
            ValidateLifetime = true,
        };

        if (authOptions.UseFirebaseEmulator)
        {
            if (!builder.Environment.IsDevelopment())
                throw new InvalidOperationException("Auth:UseFirebaseEmulator is only allowed in the Development environment.");
            // The Auth emulator issues unsigned (alg=none) tokens.
            o.TokenValidationParameters.ValidateIssuerSigningKey = false;
            o.TokenValidationParameters.RequireSignedTokens = false;
            o.TokenValidationParameters.SignatureValidator = (token, _) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
        }
        else
        {
            o.Authority = issuer;
        }
    })
    .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, ApiKeyAuthenticationHandler>(
        ApiKeyAuthenticationHandler.SchemeName, null);

if (authOptions.UseDevAuth)
{
    if (!builder.Environment.IsDevelopment())
        throw new InvalidOperationException("Auth:UseDevAuth is only allowed in the Development environment.");
    builder.Services.AddAuthentication()
        .AddScheme<Microsoft.AspNetCore.Authentication.AuthenticationSchemeOptions, DevAuthHandler>(DevAuthHandler.SchemeName, null);
}

builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, RoleRequirementHandler>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Policies.Authenticated, p => p.RequireAuthenticatedUser().AddRequirements(new RoleRequirement(Role.ExternalClient)))
    .AddPolicy(Policies.Internal, p => p.RequireAuthenticatedUser().AddRequirements(new RoleRequirement(Role.InternalDeveloper)))
    .AddPolicy(Policies.Admin, p => p.RequireAuthenticatedUser().AddRequirements(new RoleRequirement(Role.Admin)));

builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(ProxyEndpoints.RateLimitPolicy, ctx =>
    {
        var key = ctx.User.FindFirst(AppClaims.UserId)?.Value
                  ?? ctx.User.FindFirst("sub")?.Value
                  ?? ctx.Connection.RemoteIpAddress?.ToString()
                  ?? "anonymous";
        return RateLimitPartition.GetFixedWindowLimiter(key, _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = proxyOptions.RateLimitPermitsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        });
    });
});

var allowedOrigins = config.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? ["http://localhost:3000"];
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod().WithExposedHeaders("*")));

builder.Services.AddOpenApi();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsRelational()) db.Database.Migrate();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api/v1");
api.MapMe();
api.MapDocs();
api.MapSpecs();
api.MapProxy(proxyOptions);
api.MapApiKeys();
api.MapAdmin();

app.Run();

public partial class Program;
