using Microsoft.AspNetCore.Authorization;

namespace RestaurantDelivery.Api.Authorization;

// Backs every Module.* policy: only Admin accounts can ever pass (Captains/Customers are
// never granted admin-module access at all), and within Admin, a token with no module
// claims (legacy token, or an Admin with no CustomRole assigned) is treated as full
// access - see AuthService.ResolveAdminModuleNamesAsync for where that default is set.
public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (!context.User.IsInRole("Admin"))
        {
            return Task.CompletedTask;
        }

        if (AdminModuleClaimsHelper.HasModule(context.User, requirement.Module))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
