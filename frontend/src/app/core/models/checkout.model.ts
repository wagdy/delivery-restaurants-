export type PaymentMethod = 'Cash' | 'Visa' | 'Instapay';
export type PaymentStatus = 'Confirmed' | 'Pending';

export interface PromoCartItemRequest {
  menuItemId: number;
  quantity: number;
  addOnIds: number[];
}

export interface ValidatePromoRequest {
  codeText: string;
  items: PromoCartItemRequest[];
  deliveryFee: number;
}

export interface ValidatePromoResponse {
  subtotal: number;
  discountAmount: number;
  deliveryFee: number;
  deliveryDiscountAmount: number;
  taxAmount: number;
  // Fully computed "Final Total" - already includes tax and nets out both discounts.
  total: number;
  affectedMenuItemIds: number[];
  description: string;
}
