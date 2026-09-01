using RestaurantDelivery.Core.Common;
using RestaurantDelivery.Core.DTOs.Common;
using RestaurantDelivery.Core.DTOs.Orders;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly IOrderRepository _repository;
    private readonly IPushNotificationService _pushNotificationService;

    public OrderService(IOrderRepository repository, IPushNotificationService pushNotificationService)
    {
        _repository = repository;
        _pushNotificationService = pushNotificationService;
    }

    public async Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, string? userId)
    {
        var buildResult = await BuildOrderItemsAsync(request.Items);
        if (!buildResult.Succeeded)
        {
            return ServiceResult<OrderResponse>.Failure(buildResult.Errors.ToArray());
        }

        var orderItems = buildResult.Data!;

        var order = new Order
        {
            UserId = userId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            DeliveryAddress = request.DeliveryAddress,
            Status = OrderStatus.Pending,
            TotalAmount = CalculateTotal(orderItems),
            OrderItems = orderItems
        };

        await _repository.AddAsync(order);
        await _repository.SaveChangesAsync();

        // Notification failures must never surface as an order-creation failure —
        // NotifyCaptainsOfNewOrderAsync swallows and logs its own errors internally.
        await _pushNotificationService.NotifyCaptainsOfNewOrderAsync(order);

        return ServiceResult<OrderResponse>.Success(MapResponse(order));
    }

    public async Task<ServiceResult<PagedResult<OrderResponse>>> GetAllAsync(OrderStatus? status, int page, int pageSize)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize is < 1 or > 100 ? 20 : pageSize;

        var (orders, totalCount) = await _repository.GetPagedWithItemsAsync(status, page, pageSize);

        var result = new PagedResult<OrderResponse>
        {
            Items = orders.Select(MapResponse).ToList(),
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };

        return ServiceResult<PagedResult<OrderResponse>>.Success(result);
    }

    public async Task<ServiceResult<OrderResponse>> GetByIdAsync(int id)
    {
        var order = await _repository.GetByIdWithItemsAsync(id);
        if (order is null)
        {
            return ServiceResult<OrderResponse>.Failure("Order not found.");
        }

        return ServiceResult<OrderResponse>.Success(MapResponse(order));
    }

    public async Task<ServiceResult<List<OrderResponse>>> GetMyOrdersAsync(string userId)
    {
        var orders = await _repository.GetByUserIdAsync(userId);
        return ServiceResult<List<OrderResponse>>.Success(orders.Select(MapResponse).ToList());
    }

    public async Task<ServiceResult<OrderResponse>> UpdateAsync(int id, UpdateOrderRequest request)
    {
        var order = await _repository.GetByIdWithItemsAsync(id);
        if (order is null)
        {
            return ServiceResult<OrderResponse>.Failure("Order not found.");
        }

        if (order.Status is OrderStatus.Delivered or OrderStatus.Cancelled)
        {
            return ServiceResult<OrderResponse>.Failure($"Cannot edit an order that is already {order.Status}.");
        }

        var buildResult = await BuildOrderItemsAsync(request.Items);
        if (!buildResult.Succeeded)
        {
            return ServiceResult<OrderResponse>.Failure(buildResult.Errors.ToArray());
        }

        var newItems = buildResult.Data!;

        order.CustomerName = request.CustomerName;
        order.CustomerPhone = request.CustomerPhone;
        order.DeliveryAddress = request.DeliveryAddress;

        order.OrderItems.Clear();
        foreach (var item in newItems)
        {
            order.OrderItems.Add(item);
        }

        order.TotalAmount = CalculateTotal(newItems);
        order.UpdatedAt = DateTime.UtcNow;

        await _repository.SaveChangesAsync();

        return ServiceResult<OrderResponse>.Success(MapResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> UpdateStatusAsync(int id, OrderStatus status)
    {
        var order = await _repository.GetByIdWithItemsAsync(id);
        if (order is null)
        {
            return ServiceResult<OrderResponse>.Failure("Order not found.");
        }

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        await _repository.SaveChangesAsync();

        return ServiceResult<OrderResponse>.Success(MapResponse(order));
    }

    public async Task<ServiceResult<bool>> DeleteAsync(int id)
    {
        var order = await _repository.GetByIdAsync(id);
        if (order is null)
        {
            return ServiceResult<bool>.Failure("Order not found.");
        }

        _repository.Remove(order);
        await _repository.SaveChangesAsync();

        return ServiceResult<bool>.Success(true);
    }

    private async Task<ServiceResult<List<OrderItem>>> BuildOrderItemsAsync(List<OrderItemRequest> lines)
    {
        var menuItemIds = lines.Select(i => i.MenuItemId).Distinct().ToList();
        var menuItems = await _repository.GetMenuItemsByIdsAsync(menuItemIds);
        var menuItemsById = menuItems.ToDictionary(m => m.Id);

        var orderItems = new List<OrderItem>();
        foreach (var line in lines)
        {
            if (!menuItemsById.TryGetValue(line.MenuItemId, out var menuItem))
            {
                return ServiceResult<List<OrderItem>>.Failure($"Menu item {line.MenuItemId} was not found.");
            }

            if (!menuItem.IsAvailable)
            {
                return ServiceResult<List<OrderItem>>.Failure($"'{menuItem.Name}' is currently unavailable.");
            }

            var availableAddOns = menuItem.MenuItemAddOns.ToDictionary(ma => ma.AddOnId, ma => ma.AddOn);
            var addOns = new List<OrderItemAddOn>();

            foreach (var addOnId in line.AddOnIds.Distinct())
            {
                if (!availableAddOns.TryGetValue(addOnId, out var addOn))
                {
                    return ServiceResult<List<OrderItem>>.Failure(
                        $"The selected add-on is not available for '{menuItem.Name}'.");
                }

                addOns.Add(new OrderItemAddOn { AddOnId = addOn.Id, Name = addOn.Name, Price = addOn.Price });
            }

            orderItems.Add(new OrderItem
            {
                MenuItemId = menuItem.Id,
                MenuItem = menuItem,
                Quantity = line.Quantity,
                UnitPrice = menuItem.Price,
                AddOns = addOns
            });
        }

        return ServiceResult<List<OrderItem>>.Success(orderItems);
    }

    private static decimal CalculateTotal(List<OrderItem> items) =>
        items.Sum(i => i.Quantity * (i.UnitPrice + i.AddOns.Sum(a => a.Price)));

    private static OrderResponse MapResponse(Order order) => new()
    {
        Id = order.Id,
        UserId = order.UserId,
        CustomerName = order.CustomerName,
        CustomerPhone = order.CustomerPhone,
        DeliveryAddress = order.DeliveryAddress,
        TotalAmount = order.TotalAmount,
        Status = order.Status,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        Items = order.OrderItems.Select(oi => new OrderItemResponse
        {
            Id = oi.Id,
            MenuItemId = oi.MenuItemId,
            MenuItemName = oi.MenuItem.Name,
            Quantity = oi.Quantity,
            UnitPrice = oi.UnitPrice,
            AddOns = oi.AddOns.Select(a => new OrderItemAddOnResponse { Name = a.Name, Price = a.Price }).ToList()
        }).ToList()
    };
}
