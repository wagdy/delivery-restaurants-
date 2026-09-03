using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace RestaurantDelivery.Api.Hubs;

// No overrides needed: ASP.NET Core SignalR's default IUserIdProvider already keys off
// ClaimTypes.NameIdentifier, the exact claim this app's JWT already carries as the
// AppUser id - so Clients.User(customerId) (see LoyaltyRealtimeNotifier) already fans
// out to every one of that customer's connections (every open tab, every device) with
// no custom group-management logic required.
[Authorize]
public class LoyaltyHub : Hub
{
}
