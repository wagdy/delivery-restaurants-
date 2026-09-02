using Microsoft.AspNetCore.Authorization;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Api.Authorization;

public record PermissionRequirement(AdminModules Module) : IAuthorizationRequirement;
