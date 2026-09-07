using Microsoft.AspNetCore.Authorization;

namespace RestaurantDelivery.Api.Authorization;

// Requires one specific granular sub-permission string (e.g. "Orders.Create"), on top of
// the ordinary module check already done by PermissionRequirement. Not currently applied
// to any endpoint - see GranularPermissionAuthorizationHandler's doc comment for why - but
// kept ready for any future endpoint with a genuine one-to-one mapping to a single
// sub-permission.
public record GranularPermissionRequirement(string Permission) : IAuthorizationRequirement;
