export type PromoDiscountType = 'Percentage' | 'SpecificCategory' | 'SpecificItem' | 'DeliveryDiscount';

export interface PromoCode {
  id: number;
  codeText: string;
  discountType: PromoDiscountType;
  // Always a percentage (0-100) - see PromoDiscountType's backend doc comment for what
  // it's a percentage OF, which depends on discountType.
  discountValue: number;
  // Category.id values for SpecificCategory, MenuItem.id values for SpecificItem, null
  // for Percentage/DeliveryDiscount.
  targetIds: number[] | null;
  expiryDate: string;
  isActive: boolean;
  isExpired: boolean;
}

export interface PromoCodeRequest {
  codeText: string;
  discountType: PromoDiscountType;
  discountValue: number;
  targetIds: number[] | null;
  expiryDate: string;
  isActive: boolean;
  // Fires a WhatsApp broadcast to every active customer announcing this code - see
  // WhatsAppBroadcastBackgroundService. Opt-in per save (create or update), never implied.
  notifyCustomersViaWhatsApp: boolean;
}
