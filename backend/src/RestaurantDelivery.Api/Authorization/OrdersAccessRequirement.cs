using Microsoft.AspNetCore.Authorization;

namespace RestaurantDelivery.Api.Authorization;

// Marker requirement backing the "OrdersAccess" policy - see OrdersAccessAuthorizationHandler.
public record OrdersAccessRequirement : IAuthorizationRequirement;
