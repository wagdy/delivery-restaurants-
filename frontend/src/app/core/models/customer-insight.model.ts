export interface CustomerInsight {
  customerName: string;
  phoneNumber?: string | null;
  totalOrders: number;
  averageOrderValue: number;
}
