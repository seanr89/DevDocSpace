using DevDocSpace.Api.Auth;

namespace DevDocSpace.Api.Endpoints;

public static class MeEndpoints
{
    public static IEndpointRouteBuilder MapMe(this IEndpointRouteBuilder app)
    {
        app.MapGet("/me", async (CurrentUser current, CancellationToken ct) =>
        {
            var user = await current.RequireAsync(ct);
            return Results.Ok(new { user.Id, user.Email, Role = user.Role.ToString() });
        }).RequireAuthorization(Policies.Authenticated);
        return app;
    }
}
