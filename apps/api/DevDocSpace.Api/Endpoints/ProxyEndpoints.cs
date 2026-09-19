using System.Diagnostics;
using DevDocSpace.Api.Auth;
using DevDocSpace.Api.Proxy;
using DevDocSpace.Data;
using DevDocSpace.Data.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace DevDocSpace.Api.Endpoints;

public static class ProxyEndpoints
{
    public const string RateLimitPolicy = "proxy-per-user";

    public static IEndpointRouteBuilder MapProxy(this IEndpointRouteBuilder app, ProxyOptions proxyOptions)
    {
        app.MapMethods("/proxy/{service}/{version}/{env}/{**path}",
                ["GET", "POST", "PUT", "PATCH", "DELETE", "HEAD", "OPTIONS"],
                Handle)
            .RequireAuthorization(Policies.Authenticated)
            .RequireRateLimiting(RateLimitPolicy)
            .WithMetadata(new RequestSizeLimitAttribute(proxyOptions.MaxRequestBodyBytes));
        return app;
    }

    private static async Task Handle(
        HttpContext context, string service, string version, string env, string? path,
        AppDbContext db, CurrentUser current, ProxyForwarder forwarder, CancellationToken ct)
    {
        var user = await current.RequireAsync(ct);

        if (!Enum.TryParse<ApiEnvironment>(env, ignoreCase: true, out var environment))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var spec = await db.ApiSpecs.Include(s => s.Environments)
            .SingleOrDefaultAsync(s => s.Service == service && s.Version == version, ct);
        if (spec is null || !user.Role.Satisfies(spec.RequiredRole))
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var target = spec.Environments.SingleOrDefault(e => e.Name == environment);
        if (target is null)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
            return;
        }

        var upstreamUri = ProxyForwarder.BuildUpstreamUri(target.BaseUrl, path ?? "", context.Request.QueryString);
        var sw = Stopwatch.StartNew();
        var status = await forwarder.ForwardAsync(context, upstreamUri, target.CredentialKey);
        sw.Stop();

        db.ApiCallLogs.Add(new ApiCallLog
        {
            UserId = user.Id,
            SpecId = spec.Id,
            Environment = environment,
            Method = context.Request.Method,
            Path = "/" + (path ?? ""),
            StatusCode = status,
            DurationMs = (int)sw.ElapsedMilliseconds,
        });
        await db.SaveChangesAsync(CancellationToken.None);
    }
}
