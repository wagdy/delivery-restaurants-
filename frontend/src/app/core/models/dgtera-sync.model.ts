export interface SyncOrdersResult {
  ordersFetched: number;
  ordersCreated: number;
  ordersUpdated: number;
  ordersSkipped: number;
  errors: string[];
}
