using Microsoft.Extensions.Logging;
using RestaurantDelivery.Core.DTOs.Categories;
using RestaurantDelivery.Core.DTOs.Dgtera;
using RestaurantDelivery.Core.Entities;
using RestaurantDelivery.Core.Enums;
using RestaurantDelivery.Core.Interfaces;

namespace RestaurantDelivery.Infrastructure.Services;

public class DgteraSyncService : IDgteraSyncService
{
    private const string SourceName = "Dgtera";

    private readonly IDgteraClient _dgteraClient;
    private readonly IOrderRepository _orderRepository;
    private readonly IMenuItemRepository _menuItemRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ICategoryService _categoryService;
    private readonly ILogger<DgteraSyncService> _logger;

    public DgteraSyncService(
        IDgteraClient dgteraClient,
        IOrderRepository orderRepository,
        IMenuItemRepository menuItemRepository,
        ICategoryRepository categoryRepository,
        ICategoryService categoryService,
        ILogger<DgteraSyncService> logger)
    {
        _dgteraClient = dgteraClient;
        _orderRepository = orderRepository;
        _menuItemRepository = menuItemRepository;
        _categoryRepository = categoryRepository;
        _categoryService = categoryService;
        _logger = logger;
    }

    public async Task<SyncOrdersResult> SyncOrdersAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncOrdersResult();

        List<DgteraOrderDto> externalOrders;
        try
        {
            externalOrders = await _dgteraClient.GetRecentOrdersAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch orders from Dgtera.");
            // ex.Message may be a real connectivity failure or an error Odoo itself
            // returned (e.g. wrong database name, bad credentials) - both surface here.
            result.Errors.Add($"Dgtera sync failed: {ex.Message}");
            return result;
        }

        result.OrdersFetched = externalOrders.Count;

        foreach (var externalOrder in externalOrders)
        {
            try
            {
                var isNew = await UpsertOrderAsync(externalOrder);

                // Saved per-order rather than once at the end: one malformed record
                // (e.g. an unresolvable product) shouldn't roll back every other order
                // this run already processed successfully.
                await _orderRepository.SaveChangesAsync();

                if (isNew)
                {
                    result.OrdersCreated++;
                }
                else
                {
                    result.OrdersUpdated++;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to sync Dgtera order {ExternalId}", externalOrder.ExternalId);
                result.OrdersSkipped++;
                result.Errors.Add($"Order {externalOrder.ReferenceNumber ?? externalOrder.ExternalId}: {ex.Message}");
            }
        }

        return result;
    }

    // Returns true if a new Order was created, false if an existing one was updated.
    private async Task<bool> UpsertOrderAsync(DgteraOrderDto externalOrder)
    {
        var existing = await _orderRepository.GetByExternalIdAsync(SourceName, externalOrder.ExternalId);

        var orderItems = new List<OrderItem>();
        var total = 0m;

        foreach (var line in externalOrder.Lines)
        {
            var menuItem = await ResolveMenuItemAsync(line);
            var quantity = Math.Max(line.Quantity, 1);
            var unitPrice = line.UnitPrice > 0 ? line.UnitPrice : menuItem.Price;

            orderItems.Add(new OrderItem
            {
                MenuItemId = menuItem.Id,
                MenuItem = menuItem,
                Quantity = quantity,
                UnitPrice = unitPrice
            });
            total += unitPrice * quantity;
        }

        var status = MapStatus(externalOrder.RawStatus);

        if (existing is not null)
        {
            existing.CustomerName = externalOrder.CustomerName;
            existing.CustomerPhone = externalOrder.CustomerPhone ?? existing.CustomerPhone;
            existing.DeliveryAddress = externalOrder.DeliveryAddress ?? existing.DeliveryAddress;
            existing.TotalAmount = total;
            existing.Status = status;
            existing.UpdatedAt = DateTime.UtcNow;

            existing.OrderItems.Clear();
            foreach (var item in orderItems)
            {
                existing.OrderItems.Add(item);
            }

            _orderRepository.Update(existing);
            return false;
        }

        var order = new Order
        {
            ExternalSource = SourceName,
            ExternalOrderId = externalOrder.ExternalId,
            CustomerName = externalOrder.CustomerName,
            CustomerPhone = externalOrder.CustomerPhone ?? "N/A",
            DeliveryAddress = externalOrder.DeliveryAddress ?? "Picked up in-store (synced from Dgtera)",
            TotalAmount = total,
            Status = status,
            CreatedAt = externalOrder.OrderDateUtc,
            UpdatedAt = DateTime.UtcNow,
            OrderItems = orderItems
        };

        await _orderRepository.AddAsync(order);
        return true;
    }

    // Matches an incoming line to a local MenuItem by name, creating one (and its
    // category, if needed) when nothing matches - a POS product sold in Dgtera won't
    // necessarily have been added to the delivery menu yet.
    private async Task<MenuItem> ResolveMenuItemAsync(DgteraOrderLineDto line)
    {
        var existing = await _menuItemRepository.GetByNameAsync(line.ProductName);
        if (existing is not null)
        {
            return existing;
        }

        var categoryName = await ResolveCategoryNameAsync(line.CategoryName);

        var menuItem = new MenuItem
        {
            Name = line.ProductName,
            Category = categoryName,
            Price = line.UnitPrice,
            IsAvailable = true
        };

        await _menuItemRepository.AddAsync(menuItem);
        return menuItem;
    }

    private async Task<string> ResolveCategoryNameAsync(string? categoryName)
    {
        var name = string.IsNullOrWhiteSpace(categoryName) ? "Uncategorized" : categoryName.Trim();

        var existing = await _categoryRepository.GetByNameAsync(name);
        if (existing is not null)
        {
            return existing.Name;
        }

        var created = await _categoryService.CreateAsync(new CategoryRequest { Name = name });
        if (!created.Succeeded)
        {
            // Falls through to the per-order catch in SyncOrdersAsync, so this only
            // skips the one order/line at fault rather than the whole sync run.
            throw new InvalidOperationException(created.Errors.FirstOrDefault() ?? "Failed to create category.");
        }

        return created.Data!.Name;
    }

    // Dgtera/Odoo's own order lifecycle doesn't line up with ours (Pending -> Preparing
    // -> OutForDelivery -> Delivered): a completed POS sale has no further "preparing"
    // step to go through here, so it lands as fulfilled; anything not yet finalized
    // lands as Pending for a human to triage. Adjust to match your actual workflow.
    private static OrderStatus MapStatus(string rawStatus) => rawStatus.ToLowerInvariant() switch
    {
        "cancel" => OrderStatus.Cancelled,
        "done" or "invoiced" or "paid" => OrderStatus.Delivered,
        _ => OrderStatus.Pending
    };
}
