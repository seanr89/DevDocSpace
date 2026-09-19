using DevDocSpace.Data.Entities;
using Microsoft.AspNetCore.Authorization;

namespace DevDocSpace.Api.Auth;

public record RoleRequirement(Role Minimum) : IAuthorizationRequirement;

public class RoleRequirementHandler(CurrentUser currentUser) : AuthorizationHandler<RoleRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, RoleRequirement requirement)
    {
        var user = await currentUser.GetAsync();
        if (user is not null && user.Role.Satisfies(requirement.Minimum))
            context.Succeed(requirement);
    }
}

public static class Policies
{
    public const string Authenticated = "Authenticated";
    public const string Internal = "Internal";
    public const string Admin = "Admin";
}
