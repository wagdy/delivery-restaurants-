namespace RestaurantDelivery.Core.DTOs.MenuItems;

// Which side of the soft-delete line a menu item query should return.
//
// Active is the default precisely because GET /api/menuitems is public - it backs the
// storefront - so the safe value is the one you get by omitting the parameter. Asking
// for anything else requires Module.MenuItems; see MenuItemsController.GetAll.
public enum DeletedFilter
{
    Active = 0,
    Deleted = 1,
    All = 2
}
