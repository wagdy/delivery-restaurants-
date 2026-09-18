import { PaymentMethod, PaymentStatus } from './checkout.model';

// ReadyForCollection/Collected mirror OutForDelivery/Delivered for orders the customer
// picks up (see Order.isPickup). Kept in step with the backend OrderStatus enum, which is
// persisted as a string, so these names are the stored values.
export type OrderStatus =
  | 'Pending'
  | 'Preparing'
  | 'OutForDelivery'
  | 'Delivered'
  | 'ReadyForCollection'
  | 'Collected'
  | 'Cancelled';

export const ORDER_STATUSES: OrderStatus[] = [
  'Pending',
  'Preparing',
  'OutForDelivery',
  'Delivered',
  'ReadyForCollection',
  'Collected',
  'Cancelled'
];

// The statuses that only make sense for one fulfilment mode - used to keep the admin's
// status dropdown honest per order rather than offering "Collected" on a delivery.
export const DELIVERY_ONLY_STATUSES: OrderStatus[] = ['OutForDelivery', 'Delivered'];
export const COLLECTION_ONLY_STATUSES: OrderStatus[] = ['ReadyForCollection', 'Collected'];

// What a human should read. The raw enum names leak straight into the customer's My
// Orders list otherwise, which is how "OutForDelivery" ended up on screen - and adding
// "ReadyForCollection" without this would have made that worse, not better.
export const ORDER_STATUS_LABELS: Record<OrderStatus, string> = {
  Pending: 'Pending',
  Preparing: 'Preparing',
  OutForDelivery: 'Out for delivery',
  Delivered: 'Delivered',
  ReadyForCollection: 'Ready for collection',
  Collected: 'Collected',
  Cancelled: 'Cancelled'
};

export interface OrderItemAddOn {
  name: string;
  price: number;
}

export interface OrderItem {
  id: number;
  menuItemId: number;
  menuItemName: string;
  // The size/weight that was ordered, snapshotted server-side at order time. Null for
  // items with no variants. The kitchen needs it as much as the customer does - the dish
  // name alone does not say whether to cook 500g or a kilo.
  variantName: string | null;
  quantity: number;
  unitPrice: number;
  addOns: OrderItemAddOn[];
  lineTotal: number;
}

export type CustomerStatus = 'Guest' | 'Registered';

export interface Order {
  id: number;
  userId?: string | null;
  // "Guest" or "Registered" - computed server-side from userId (see
  // OrderService.MapResponse), kept as a real field rather than re-derived from userId
  // at every call site so the cashier dashboard badge is a one-line binding.
  customerStatus: CustomerStatus;
  customerName: string;
  customerPhone: string;
  deliveryAddress: string;
  totalAmount: number;
  status: OrderStatus;
  notes?: string | null;
  createdAt: string;
  updatedAt: string;
  items: OrderItem[];
  promoCodeText?: string | null;
  discountAmount: number;
  // Points spent on this order and what they were worth, snapshotted server-side.
  pointsRedeemed: number;
  pointsDiscountAmount: number;
  taxAmount: number;
  deliveryFee: number;
  // Store pickup rather than delivery. Always accompanied by deliveryFee: 0 - the server
  // zeroes it (see OrderService.CreateAsync) rather than trusting the client.
  isPickup: boolean;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  subtotal: number;
  // Whether a cashier has dismissed the live new-order alarm for this order -
  // independent of `status`, see the backend Order.IsAcknowledged doc comment.
  isAcknowledged: boolean;
  acknowledgedAt?: string | null;
}

export interface BulkOrderImportResult {
  rowsProcessed: number;
  ordersCreated: number;
  rowsSkipped: number;
  errors: string[];
}

export interface OrderItemRequest {
  menuItemId: number;
  quantity: number;
  addOnIds: number[];
}

export interface CreateOrderRequest {
  // Loyalty points the customer asks to spend. The server revalidates against their
  // real balance and recalculates the discount from its own settings.
  pointsToRedeem?: number;
  customerName: string;
  customerPhone: string;
  deliveryAddress: string;
  // The customer's "Special request" from the review step. Lands in Order.notes, which
  // the admin order-details dialog already displays - see CreateOrderRequest.cs for why
  // this reuses that column rather than adding a second free-text field.
  notes?: string | null;
  items: OrderItemRequest[];
  paymentMethod: PaymentMethod;
  // Re-validated server-side against the live PromoCodes table - never trusted at face
  // value for the discount amount.
  promoCodeText?: string | null;
  deliveryFee: number;
  // Store pickup instead of delivery. Re-checked server-side against the live
  // isPickupEnabled setting, which is what actually enforces it.
  isPickup?: boolean;
  // Set only by the admin "Create Order" screen when a registered customer was picked
  // via phone search - re-validated server-side, never trusted at face value either.
  customerId?: string | null;
  // Set only by the admin "Create Order" screen's New Customer mode - tells the backend
  // to find-or-create a real customer account from customerName/customerPhone/
  // deliveryAddress instead of leaving the order a guest order (see OrderService.CreateAsync,
  // which only honors this for staff-created orders).
  isNewCustomer?: boolean;
}

// For the admin "Create Order" screen's registered-customer phone search.
export interface CustomerLookup {
  id: string;
  fullName: string;
  phoneNumber: string;
  address: string | null;
}

export type UpdateOrderRequest = CreateOrderRequest;

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}

// Lightweight OrderHub "NewOrderReceived" push payload - mirrors the backend's
// NewOrderNotification DTO. Deliberately not the full Order shape; the admin dashboard
// fetches the complete order by orderId once notified.
export interface NewOrderNotification {
  orderId: number;
  customerName: string;
  totalAmount: number;
  itemCount: number;
  createdAt: string;
  // True when an admin entered this order themselves via the Create Order screen - the
  // cashier dashboard still unshifts it into the list on every connected tab, but must
  // not ring the alarm or show "needs acknowledgment" for it anywhere.
  isStaffCreated: boolean;
}
