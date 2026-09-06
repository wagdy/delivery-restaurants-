import { PaymentMethod, PaymentStatus } from './checkout.model';

export type OrderStatus = 'Pending' | 'Preparing' | 'OutForDelivery' | 'Delivered' | 'Cancelled';

export const ORDER_STATUSES: OrderStatus[] = [
  'Pending',
  'Preparing',
  'OutForDelivery',
  'Delivered',
  'Cancelled'
];

export interface OrderItemAddOn {
  name: string;
  price: number;
}

export interface OrderItem {
  id: number;
  menuItemId: number;
  menuItemName: string;
  quantity: number;
  unitPrice: number;
  addOns: OrderItemAddOn[];
  lineTotal: number;
}

export interface Order {
  id: number;
  userId?: string | null;
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
  taxAmount: number;
  deliveryFee: number;
  paymentMethod: PaymentMethod;
  paymentStatus: PaymentStatus;
  subtotal: number;
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
  customerName: string;
  customerPhone: string;
  deliveryAddress: string;
  items: OrderItemRequest[];
  paymentMethod: PaymentMethod;
  // Re-validated server-side against the live PromoCodes table - never trusted at face
  // value for the discount amount.
  promoCodeText?: string | null;
  deliveryFee: number;
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
}
