using Microsoft.AspNetCore.Authorization;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Api.Authorization;

// Generic granular-permission policy infrastructure. NOT wired to any endpoint today: the
// 8 concrete example sub-permissions from the Manage Roles UI (Orders.Create/AllOrders/
// ActiveStatus/Reports, Settings.Branding/Contact/Checkout/Payment) don't map one-to-one
// onto distinct backend endpoints - e.g. GET /api/orders backs both the "All Orders" and
// "Active Status" tabs, and POST /api/orders is intentionally unauthenticated (public
// guest checkout shares it with the admin "Create Order" tab), so gating either with a
// single-permission policy would break something that must stay reachable. Enforcement
// for those 8 tabs is done in the Angular layer instead (@if visibility + defensive
// tab-switch guards). This handler exists so a *future* endpoint that genuinely serves
// exactly one sub-permission can add a "Permission.X" policy with zero new plumbing.
//
// Same "absence = full access" rule as PermissionAuthorizationHandler, applied one level
// deeper: a token with no granular-permission claims *for this permission's module* is
// treated as unrestricted within that module (legacy role, or a role that simply never
// recorded sub-permission restrictions).
public class GranularPermissionAuthorizationHandler : AuthorizationHandler<GranularPermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        GranularPermissionRequirement requirement)
    {
        if (!context.User.IsInRole("Admin"))
        {
            return Task.CompletedTask;
        }

        var granted = context.User.FindAll(GranularPermissionClaims.ClaimType).Select(c => c.Value).ToList();
        var modulePrefix = requirement.Permission.Split('.')[0] + '.';
        var hasAnyForModule = granted.Any(p => p.StartsWith(modulePrefix, StringComparison.Ordinal));

        if (!hasAnyForModule || granted.Contains(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
