using Microsoft.AspNetCore.Authorization;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Api.Authorization;

// Backs the existing "OrdersAccess" policy (used by OrdersController.GetAll/UpdateStatus,
// attributes left untouched). CaptainOrder always passes - captains need these endpoints
// to do deliveries regardless of any Admin-side custom Role - while Admin additionally
// needs the Orders module, so a restricted custom Role can be denied the same endpoints
// an Admin-only Module.Orders policy would deny.
public class OrdersAccessAuthorizationHandler : AuthorizationHandler<OrdersAccessRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        OrdersAccessRequirement requirement)
    {
        if (context.User.IsInRole("CaptainOrder"))
        {
            context.Succeed(requirement);
            return Task.CompletedTask;
        }

        if (context.User.IsInRole("Admin") && AdminModuleClaimsHelper.HasModule(context.User, AdminModules.Orders))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
