using Microsoft.AspNetCore.Authorization;

namespace RestaurantDelivery.Api.Authorization;

// Backs AnyModuleRequirement - same "Admin role required, no-modules-claim fails open"
// rule as PermissionAuthorizationHandler, but succeeds if the token has any one of the
// requirement's listed modules instead of requiring one specific module.
public class AnyModuleAuthorizationHandler : AuthorizationHandler<AnyModuleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AnyModuleRequirement requirement)
    {
        if (!context.User.IsInRole("Admin"))
        {
            return Task.CompletedTask;
        }

        if (requirement.Modules.Any(module => AdminModuleClaimsHelper.HasModule(context.User, module)))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
