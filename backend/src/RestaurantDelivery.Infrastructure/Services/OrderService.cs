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
    private readonly IAuthService _authService;

    public OrderService(
        IOrderRepository repository,
        IPushNotificationService pushNotificationService,
        ILoyaltyService loyaltyService,
        IWhatsAppNotificationService whatsAppNotificationService,
        IOrderRealtimeNotifier orderRealtimeNotifier,
        IPromoCodeRepository promoCodeRepository,
        ICategoryRepository categoryRepository,
        ISettingsService settingsService,
        IAuthService authService)
    {
        _repository = repository;
        _pushNotificationService = pushNotificationService;
        _loyaltyService = loyaltyService;
        _whatsAppNotificationService = whatsAppNotificationService;
        _orderRealtimeNotifier = orderRealtimeNotifier;
        _promoCodeRepository = promoCodeRepository;
        _categoryRepository = categoryRepository;
        _settingsService = settingsService;
        _authService = authService;
    }

    public async Task<ServiceResult<OrderResponse>> CreateAsync(CreateOrderRequest request, string? userId, bool isStaffCreated = false)
    {
        // Only a staff-created order can auto-register a customer - IsNewCustomer is
        // otherwise ignored entirely. Without this gate, a public/guest checkout request
        // could attach itself to any stranger's account just by sending their phone
        // number, since phone numbers (unlike CreateOrderRequest.CustomerId's opaque GUID)
        // aren't a secret. Resolves to an existing account if this phone is already
        // registered (self-healing a cashier picking "New" for a repeat customer) rather
        // than erroring or creating a duplicate.
        if (request.IsNewCustomer && isStaffCreated)
        {
            var customerResult = await _authService.FindOrCreateCustomerByPhoneAsync(
                request.CustomerName, request.CustomerPhone, request.DeliveryAddress);
            if (!customerResult.Succeeded)
            {
                return ServiceResult<OrderResponse>.Failure(customerResult.Errors.ToArray());
            }

            userId = customerResult.Data!.CustomerId;
        }
        // Covers both a normal customer's own (already-trustworthy) JWT-derived id and
        // an admin-supplied CreateOrderRequest.CustomerId from the "Create Order" POS
        // screen's phone search - the latter is exactly the case that needs this check,
        // since it's a client-supplied value; validating both paths uniformly is one
        // extra indexed lookup on the common path and closes the gap on the new one.
        else if (userId is not null && await _repository.GetCustomerByIdAsync(userId) is null)
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

        // Fetched before the promo block below because the delivery fee feeds into
        // free-delivery promo resolution as well as the final total.
        var settings = await _settingsService.GetAsync();

        // A customer's own delivery fee comes from settings, never from their request.
        // This endpoint accepts guest orders without a token, so honoring the posted value
        // let anyone send 0 and skip the charge - every other money value here was already
        // recomputed server-side (item prices, add-on prices, promo discounts, tax) and
        // this was the one that wasn't.
        //
        // Staff keep the override, gated the same way IsNewCustomer and CustomerId already
        // are: the admin "Create Order" screen legitimately sets a per-order fee (0 for a
        // pickup, a higher one for an address outside the normal range), and isStaffCreated
        // is derived from the caller's own JWT role in OrdersController, not from anything
        // in the request body.
        var deliveryFee = isStaffCreated ? request.DeliveryFee : settings.BaseDeliveryFee;

        // Pickup is re-checked against the live setting rather than taken on trust. A
        // client that posts IsPickup while the branch has pickup switched off would
        // otherwise pay no delivery fee for an order a driver still has to deliver, and
        // this endpoint accepts guest orders with no token at all. Refusing outright (as
        // opposed to silently downgrading to delivery) is deliberate: the customer chose
        // pickup, and quietly charging them for delivery instead would be worse than an
        // error they can act on.
        if (request.IsPickup && !settings.IsPickupEnabled)
        {
            return ServiceResult<OrderResponse>.Failure("Store pickup is currently unavailable. Please choose delivery.");
        }

        // Nothing is being delivered, so nothing is charged for delivery - applied after
        // the staff override above so an admin cannot accidentally leave a fee on a
        // pickup order either.
        if (request.IsPickup)
        {
            deliveryFee = 0m;
        }

        if (!string.IsNullOrWhiteSpace(request.PromoCodeText))
        {
            var promoResult = await ResolveAndApplyPromoAsync(request.PromoCodeText, orderItems, deliveryFee);
            if (!promoResult.Succeeded)
            {
                return ServiceResult<OrderResponse>.Failure(promoResult.Errors.ToArray());
            }

            (promoCodeText, discountAmount, deliveryDiscountAmount) = promoResult.Data;
        }

        var taxableAmount = subtotal - discountAmount;
        var taxAmount = Math.Round(taxableAmount * (settings.TaxPercentage / 100m), 2);
        var deliveryFeeAfterDiscount = deliveryFee - deliveryDiscountAmount;

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
            DeliveryFee = deliveryFee,
            IsPickup = request.IsPickup,
            Notes = OptionalText.NullIfBlank(request.Notes),
            PaymentMethod = request.PaymentMethod,
            PaymentStatus = request.PaymentMethod == PaymentMethod.Visa ? PaymentStatus.Pending : PaymentStatus.Confirmed,
            OrderItems = orderItems
        };

        await _repository.AddAsync(order);
        await _repository.SaveChangesAsync();

        // Notification failures must never surface as an order-creation failure —
        // both methods swallow and log their own errors internally. Captains need to know
        // about a new delivery regardless of who entered it, but the cashier push is
        // suppressed for staff-created orders for the same reason the SignalR alarm is
        // (see NewOrderNotification.IsStaffCreated) - a cashier shouldn't get paged for an
        // order they (or a co-worker) just typed into the POS themselves.
        //
        // Pickup orders skip the captain push entirely: there is nothing to drive, and the
        // captain's own queue filters them out (see CaptainOrdersComponent.filteredOrders),
        // so paging a driver here would send them to a list the order is not in. The
        // cashier still hears about it - somebody in the branch has to make the food.
        if (!order.IsPickup)
        {
            await _pushNotificationService.NotifyCaptainsOfNewOrderAsync(order);
        }
        if (!isStaffCreated)
        {
            await _pushNotificationService.NotifyCashiersOfNewOrderAsync(order);
        }

        // Deliberately NOT awaited: SendOrderNotificationsAsync never throws (see
        // IWhatsAppNotificationService's contract, enforced by the implementation's own
        // internal try/catch) and depends on nothing request-scoped - just an HttpClient,
        // IOptions, and ILogger, all safe to keep running after this method returns. Not
        // awaiting it here is what actually keeps a slow Green API response from adding
        // its own latency to the checkout API's HTTP response.
        _ = _whatsAppNotificationService.SendOrderNotificationsAsync(
            order,
            [settings.ManagerWhatsApp1, settings.ManagerWhatsApp2, settings.ManagerWhatsApp3]);

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

        // IsFinal rather than naming statuses: a collected pickup order is just as
        // finished as a delivered one, and a direct comparison here would have left it
        // editable after the fact.
        if (OrderStatuses.IsFinal(order.Status))
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

        // IsFulfilled, not == Delivered: a customer who collects their order has earned
        // the same points as one who had it delivered, and gating on Delivered alone
        // would have quietly stopped awarding points the moment pickup went live.
        if (OrderStatuses.IsFulfilled(status) && !alreadyProcessedForLoyalty)
        {
            // Awards points/punches and returns the customer's post-award balance
            // (NewTotalPoints) - must never fail this method, ProcessOrderDeliveredAsync
            // catches its own DbUpdateException race internally.
            var loyaltyResult = await _loyaltyService.ProcessOrderDeliveredAsync(order);

            // The only WhatsApp touchpoint on delivery - the old order-summary "تم تسليم
            // طلبك... ملخص الطلب" message (SendOrderConfirmationAsync) has been removed
            // entirely, not just for this branch; it had no other caller. Registered
            // customers get the points/balance message; a guest order (order.UserId is
            // null, so there's no LoyaltyProfile to report a balance from) gets a
            // separate, points-free thank-you instead of nothing. Deliberately NOT
            // awaited: this is purely a customer-engagement side effect, so it must
            // never add its own latency to the "mark delivered" response an
            // admin/captain is waiting on - same reasoning, and same
            // safe-to-fire-and-forget shape (no request-scoped dependencies, never
            // throws), as SendOrderNotificationsAsync in OrderService.CreateAsync.
            if (order.UserId is not null)
            {
                _ = _whatsAppNotificationService.SendPostDeliveryPointsNotificationAsync(
                    order.CustomerPhone, order.CustomerName, loyaltyResult.PointsEarned, loyaltyResult.NewTotalPoints);
            }
            else
            {
                _ = _whatsAppNotificationService.SendGuestDeliveryThankYouAsync(
                    order.CustomerPhone, order.CustomerName, order.Id);
            }
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

            // Variant resolution, before anything is priced.
            //
            // Both directions are refused rather than guessed at. An item WITH variants
            // has no meaningful base price - falling back to MenuItem.Price would charge
            // a customer who picked "1 Kilo" whatever the placeholder base happens to be.
            // An item WITHOUT variants that arrives carrying one means the client is
            // confused about what it is ordering, and silently dropping the id would hide
            // that. The lookup is scoped to THIS item's own variants, so naming a cheap
            // variant belonging to something else fails rather than underpricing.
            MenuItemVariant? variant = null;
            if (menuItem.Variants.Count > 0)
            {
                if (line.VariantId is null)
                {
                    return ServiceResult<List<OrderItem>>.Failure(
                        $"Please choose a size for '{menuItem.Name}'.");
                }

                variant = menuItem.Variants.FirstOrDefault(v => v.Id == line.VariantId.Value);
                if (variant is null)
                {
                    return ServiceResult<List<OrderItem>>.Failure(
                        $"The selected size is not available for '{menuItem.Name}'.");
                }

                if (!variant.IsAvailable)
                {
                    return ServiceResult<List<OrderItem>>.Failure(
                        $"'{variant.Name}' of '{menuItem.Name}' is currently unavailable.");
                }
            }
            else if (line.VariantId is not null)
            {
                return ServiceResult<List<OrderItem>>.Failure(
                    $"'{menuItem.Name}' does not come in different sizes.");
            }

            var resolvedUnitPrice = variant?.Price ?? menuItem.Price;

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

            // Zero-price rules, applied AFTER add-ons are resolved because for a
            // dynamically priced item the add-ons ARE the price.
            //
            // Two different zero states, and collapsing them would break one or the
            // other: an item priced from its add-ons is perfectly orderable as long as
            // the customer actually picked some, while an item priced on the day has no
            // figure at all and is not orderable online.
            if (menuItem.IsPriceBasedOnAddons)
            {
                // Having add-ons available is guaranteed by MenuItemService; having any
                // SELECTED is not, and none selected still adds up to nothing. A
                // selection of only free add-ons lands here too, which is why the test
                // is on the money rather than on the count.
                if (addOns.Sum(a => a.Price) <= 0)
                {
                    return ServiceResult<List<OrderItem>>.Failure(
                        $"Please choose at least one paid option for '{menuItem.Name}'.");
                }
            }
            else if (resolvedUnitPrice <= 0)
            {
                // Priced on the day (Price 0, explained by PriceNote). Without this a
                // crafted request orders a kilo of meat for L.E 0.00 - and with add-ons
                // attached the customer would be charged for the tahini and nothing for
                // the meat.
                return ServiceResult<List<OrderItem>>.Failure(
                    $"'{menuItem.Name}' is priced on the day and cannot be ordered online. " +
                    "Please call the branch to order it.");
            }

            orderItems.Add(new OrderItem
            {
                MenuItemId = menuItem.Id,
                MenuItem = menuItem,
                // Snapshotted here, not read back through the navigation later - that is
                // what lets a menu item be soft-deleted without taking every past order
                // containing it down with it.
                MenuItemName = menuItem.Name,
                VariantId = variant?.Id,
                // Snapshotted for the same reason as MenuItemName: a variant cascades
                // away with its menu item, so the receipt cannot depend on it existing.
                VariantName = variant?.Name,
                Quantity = line.Quantity,
                // The variant's price REPLACES the base price - it is absolute, not a
                // delta. Add-ons are what add.
                UnitPrice = resolvedUnitPrice,
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
        IsPickup = order.IsPickup,
        PaymentMethod = order.PaymentMethod,
        PaymentStatus = order.PaymentStatus,
        IsAcknowledged = order.IsAcknowledged,
        AcknowledgedAt = order.AcknowledgedAt,
        Items = order.OrderItems.Select(oi => new OrderItemResponse
        {
            Id = oi.Id,
            MenuItemId = oi.MenuItemId,
            // The snapshot, not oi.MenuItem.Name. The navigation is filtered out once
            // the item is soft-deleted, so reading through it would return null here and
            // throw. Falls back to the live row only for orders placed before the
            // snapshot column existed and somehow missed the migration's backfill.
            MenuItemName = string.IsNullOrEmpty(oi.MenuItemName) ? oi.MenuItem?.Name ?? string.Empty : oi.MenuItemName,
            VariantName = oi.VariantName,
            Quantity = oi.Quantity,
            UnitPrice = oi.UnitPrice,
            AddOns = oi.AddOns.Select(a => new OrderItemAddOnResponse { Name = a.Name, Price = a.Price }).ToList()
        }).ToList()
    };
}
