export type AdminModuleName =
  | 'Orders'
  | 'MenuItems'
  | 'Settings'
  | 'Staff'
  | 'Customers'
  | 'Crm'
  | 'Campaigns'
  | 'Scanner'
  | 'PromoCodes';

export interface Role {
  id: number;
  name: string;
  modules: AdminModuleName[];
}

export interface RoleRequest {
  name: string;
  modules: AdminModuleName[];
}
