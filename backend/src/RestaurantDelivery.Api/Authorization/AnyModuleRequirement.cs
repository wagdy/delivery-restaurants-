using Microsoft.AspNetCore.Authorization;
using RestaurantDelivery.Core.Enums;

namespace RestaurantDelivery.Api.Authorization;

// Unlike PermissionRequirement (a single required module), this passes if the caller has
// ANY one of the listed modules - needed because the merged Customer Insights dashboard
// must stay reachable by whichever of the two pre-existing, independently-grantable
// permissions (Crm, Customers) a given role already had, rather than silently narrowing
// everyone down to the intersection of both.
public record AnyModuleRequirement(params AdminModules[] Modules) : IAuthorizationRequirement;
