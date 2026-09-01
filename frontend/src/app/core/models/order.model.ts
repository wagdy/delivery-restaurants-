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
  createdAt: string;
  updatedAt: string;
  items: OrderItem[];
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
}

export type UpdateOrderRequest = CreateOrderRequest;

export interface PagedResult<T> {
  items: T[];
  totalCount: number;
  page: number;
  pageSize: number;
}
