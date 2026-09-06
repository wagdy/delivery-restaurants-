namespace RestaurantDelivery.Core.Enums;

// DiscountValue is always a percentage (0-100) regardless of type - interpreted as: %
// off the whole subtotal (Percentage), % off items in the targeted categories
// (SpecificCategory) or menu items (SpecificItem), or % off the delivery fee
// (DeliveryDiscount, where 100 means free delivery). See PromoDiscountCalculator.
public enum PromoDiscountType
{
    Percentage,
    SpecificCategory,
    SpecificItem,
    DeliveryDiscount
}
