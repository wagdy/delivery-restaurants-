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
    private readonly ILoyaltyService _loyaltyService;
    private readonly IWhatsAppNotificationService _whatsAppNotificationService;
    private readonly IOrderRealtimeNotifier _orderRealtimeNotifier;
    private readonly IPromoCodeRepository _promoCodeRepository;
    private readonly ICategoryRepository _categoryRepository;
    private readonly ISettingsService _settingsService;

    public OrderService(
        IOrderRepository repository,
        IPushNotificationService pushNotificationService,
        ILoyaltyService loyaltyService,
        IWhatsAppNotificationService whatsAppNotificationService,
        IOrderRealtimeNotifier orderRealtimeNotifier,
        IPromoCodeRepository promoCodeRepository,
        ICategoryRepository categoryRepository,
        ISettingsService settingsService)
    {
        _repository = repository;
        _pushNotificationService = pushNotificationService;
        _loyaltyService = loyaltyService;
        _whatsAppNotificationService = whatsAppNotificationService;
        _orderRealtimeNotifier = orderRealtimeNotifier;
        _promoCodeRepository = promoCodeRepository;
        _categoryRepository = categoryRepository;
        _settingsService = settingsService;
    }

    public async Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, string? userId, bool isStaffCreated = false)
    {
        // Covers both a normal customer's own (already-trustworthy) JWT-derived id and
        // an admin-supplied CreateOrderRequest.CustomerId from the "Create Order" POS
        // screen's phone search - the latter is exactly the case that needs this check,
        // since it's a client-supplied value; validating both paths uniformly is one
        // extra indexed lookup on the common path and closes the gap on the new one.
        if (userId is not null && await _repository.GetCustomerByIdAsync(userId) is null)
        {
            return ServiceResult<OrderResponse>.Failure("The selected customer account could not be found.");
        }

        var buildResult = await BuildOrderItemsAsync(request.Items);
        if (!buildResult.Succeeded)
        {
            return ServiceResult<OrderResponse>.Failure(buildResult.Errors.ToArray());
        }

        var orderItems = buildResult.Data!;
        var subtotal = CalculateTotal(orderItems);

        // Re-validated and recomputed from scratch here, exactly like
        // CheckoutService.ValidatePromoAsync - never trust a client-supplied discount
        // amount. A code that was valid moments ago at preview time but has since
        // expired/been deactivated/deleted fails the whole order rather than silently
        // charging full price for a discount the customer expected.
        string? promoCodeText = null;
        var discountAmount = 0m;
        var deliveryDiscountAmount = 0m;

        if (!string.IsNullOrWhiteSpace(request.PromoCodeText))
        {
            var promoResult = await ResolveAndApplyPromoAsync(request.PromoCodeText, orderItems, request.DeliveryFee);
            if (!promoResult.Succeeded)
            {
                return ServiceResult<OrderResponse>.Failure(promoResult.Errors.ToArray());
            }

            (promoCodeText, discountAmount, deliveryDiscountAmount) = promoResult.Data;
        }

        var settings = await _settingsService.GetAsync();
        var taxableAmount = subtotal - discountAmount;
        var taxAmount = Math.Round(taxableAmount * (settings.TaxPercentage / 100m), 2);
        var deliveryFeeAfterDiscount = request.DeliveryFee - deliveryDiscountAmount;

        var order = new Order
        {
            UserId = userId,
            CustomerName = request.CustomerName,
            CustomerPhone = request.CustomerPhone,
            DeliveryAddress = request.DeliveryAddress,
            Status = OrderStatus.Pending,
            TotalAmount = taxableAmount + taxAmount + deliveryFeeAfterDiscount,
            PromoCodeText = promoCodeText,
            DiscountAmount = discountAmount,
            TaxAmount = taxAmount,
            DeliveryFee = request.DeliveryFee,
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = request.PaymentMethod == PaymentMethod.Visa ? PaymentStatus.Pending : PaymentStatus.Confirmed,
            OrderItems = orderItems
        };

        await _repository.AddAsync(order);
        await _repository.SaveChangesAsync();

        // Notification failures must never surface as an order-creation failure —
        // NotifyCaptainsOfNewOrderAsync swallows and logs its own errors internally.
        await _pushNotificationService.NotifyCaptainsOfNewOrderAsync(order);

        // Live-updates the admin/cashier Orders tab the instant this order lands, instead
        // of staff having to manually refresh to see it. Same "never throw" contract as
        // the push notification above - order.Id is already populated by SaveChangesAsync.
        await _orderRealtimeNotifier.NotifyNewOrderAsync(new NewOrderNotification
        {
            OrderId = order.Id,
            CustomerName = order.CustomerName,
            TotalAmount = order.TotalAmount,
            ItemCount = orderItems.Sum(i => i.Quantity),
            CreatedAt = order.CreatedAt,
            IsStaffCreated = isStaffCreated
        });

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

        // DiscountAmount and DeliveryFee are left as originally applied at checkout - an
        // admin correcting line items isn't re-running promo validation or re-quoting
        // delivery. Only TaxAmount is recomputed (against the new subtotal), so the
        // persisted breakdown (Subtotal - DiscountAmount + TaxAmount + DeliveryFee) still
        // adds up to TotalAmount after the edit.
        var newSubtotal = CalculateTotal(newItems);
        var settings = await _settingsService.GetAsync();
        order.TaxAmount = Math.Round((newSubtotal - order.DiscountAmount) * (settings.TaxPercentage / 100m), 2);
        order.TotalAmount = newSubtotal - order.DiscountAmount + order.TaxAmount + order.DeliveryFee;
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

        // Captured before anything below runs. Deliberately keyed on the persistent
        // PointsAwarded flag rather than "was the previous status already Delivered" -
        // that weaker check doesn't survive a Delivered -> Cancelled -> Delivered round
        // trip (order.Status would read Cancelled just before the second Delivered PATCH,
        // so a same-status comparison alone would re-fire both the loyalty award and the
        // WhatsApp confirmation for a second time).
        var alreadyProcessedForLoyalty = order.PointsAwarded;

        order.Status = status;
        order.UpdatedAt = DateTime.UtcNow;

        await _repository.SaveChangesAsync();

        if (status == OrderStatus.Delivered && !alreadyProcessedForLoyalty)
        {
            // Both of these must never fail this method - ProcessOrderDeliveredAsync
            // catches its own DbUpdateException race, and the WhatsApp service swallows
            // and logs every failure internally.
            var loyaltyResult = await _loyaltyService.ProcessOrderDeliveredAsync(order);
            await _whatsAppNotificationService.SendOrderConfirmationAsync(
                order.CustomerPhone, order.CustomerName, order,
                loyaltyResult.PointsEarned, loyaltyResult.NewTotalPoints, loyaltyResult.TierUpgraded, loyaltyResult.PunchUpdates);
        }

        return ServiceResult<OrderResponse>.Success(MapResponse(order));
    }

    public async Task<ServiceResult<OrderResponse>> AcknowledgeAsync(int id)
    {
        var order = await _repository.GetByIdWithItemsAsync(id);
        if (order is null)
        {
            return ServiceResult<OrderResponse>.Failure("Order not found.");
        }

        // Idempotent - re-acknowledging an already-acknowledged order (e.g. a duplicate
        // click, or two cashiers on different tabs) just returns success without
        // clobbering the original AcknowledgedAt timestamp.
        if (!order.IsAcknowledged)
        {
            order.IsAcknowledged = true;
            order.AcknowledgedAt = DateTime.UtcNow;
            await _repository.SaveChangesAsync();
        }

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

    public async Task<ServiceResult<CustomerLookupResponse>> LookupCustomerByPhoneAsync(string phoneNumber)
    {
        var customer = await _repository.GetCustomerByPhoneAsync(phoneNumber);
        if (customer is null)
        {
            return ServiceResult<CustomerLookupResponse>.Failure("No registered customer found with that phone number.");
        }

        return ServiceResult<CustomerLookupResponse>.Success(new CustomerLookupResponse
        {
            Id = customer.Id,
            FullName = customer.FullName,
            PhoneNumber = customer.PhoneNumber ?? string.Empty,
            Address = customer.Address
        });
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

    // Mirrors CheckoutService.ValidatePromoAsync's own resolve-then-calculate flow, but
    // against the already-built, already-priced OrderItem list instead of a fresh
    // MenuItem lookup - both ultimately feed the same PromoDiscountCalculator so the two
    // can never disagree about what a given code is worth.
    private async Task<ServiceResult<(string CodeText, decimal DiscountAmount, decimal DeliveryDiscountAmount)>> ResolveAndApplyPromoAsync(
        string rawCodeText, List<OrderItem> orderItems, decimal deliveryFee)
    {
        var codeText = rawCodeText.Trim().ToUpperInvariant();
        var promo = await _promoCodeRepository.GetByCodeAsync(codeText);

        if (promo is null)
        {
            return ServiceResult<(string, decimal, decimal)>.Failure("This promo code was not found.");
        }

        if (!promo.IsActive)
        {
            return ServiceResult<(string, decimal, decimal)>.Failure("This promo code is no longer active.");
        }

        if (promo.ExpiryDate <= DateTime.UtcNow)
        {
            return ServiceResult<(string, decimal, decimal)>.Failure("This promo code has expired.");
        }

        var lines = orderItems
            .Select(i => new PromoDiscountCalculator.CartLine(
                i.MenuItemId, i.MenuItem.Category, i.Quantity, i.Quantity * (i.UnitPrice + i.AddOns.Sum(a => a.Price))))
            .ToList();

        var categoryNamesById = new Dictionary<int, string>();
        if (promo.DiscountType == PromoDiscountType.SpecificCategory)
        {
            var targetIds = PromoDiscountCalculator.ParseTargetIds(promo.TargetIds);
            var categories = await _categoryRepository.GetByIdsAsync(targetIds);
            categoryNamesById = categories.ToDictionary(c => c.Id, c => c.Name);
        }

        var discount = PromoDiscountCalculator.Calculate(promo, lines, deliveryFee, categoryNamesById);

        return ServiceResult<(string, decimal, decimal)>.Success(
            (promo.CodeText, discount.ItemDiscountAmount, discount.DeliveryDiscountAmount));
    }

    private static OrderResponse MapResponse(Order order) => new()
    {
        Id = order.Id,
        UserId = order.UserId,
        CustomerStatus = order.UserId is null ? "Guest" : "Registered",
        CustomerName = order.CustomerName,
        CustomerPhone = order.CustomerPhone,
        DeliveryAddress = order.DeliveryAddress,
        TotalAmount = order.TotalAmount,
        Status = order.Status,
        Notes = order.Notes,
        CreatedAt = order.CreatedAt,
        UpdatedAt = order.UpdatedAt,
        PromoCodeText = order.PromoCodeText,
        DiscountAmount = order.DiscountAmount,
        TaxAmount = order.TaxAmount,
        DeliveryFee = order.DeliveryFee,
        PaymentMethod = order.PaymentMethod,
        PaymentStatus = order.PaymentStatus,
        IsAcknowledged = order.IsAcknowledged,
        AcknowledgedAt = order.AcknowledgedAt,
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
