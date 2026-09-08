export type AdminModuleName =
  | 'Orders'
  | 'MenuItems'
  | 'Settings'
  | 'Staff'
  | 'Customers'
  | 'Crm'
  | 'Campaigns'
  | 'Scanner'
  | 'PromoCodes'
  | 'Reviews';

export interface SubPermissionOption {
  value: string;
  label: string;
}

// Sub-permission catalog for modules that have their own internal tabs/sections needing
// finer-grained control than "has the module or not". Mirrors the backend's
// GranularPermissions catalog (backend/src/RestaurantDelivery.Core/Common/GranularPermissions.cs)
// value-for-value - a module with no entry here is simply granted or denied wholesale,
// exactly as it worked before this feature existed.
export const MODULE_SUB_PERMISSIONS: Partial<Record<AdminModuleName, SubPermissionOption[]>> = {
  Orders: [
    { value: 'Orders.Create', label: 'Create Order' },
    { value: 'Orders.AllOrders', label: 'All Orders' },
    { value: 'Orders.ActiveStatus', label: 'Active Status' },
    { value: 'Orders.Reports', label: 'Reports' }
  ],
  Settings: [
    { value: 'Settings.Branding', label: 'Branding' },
    { value: 'Settings.Contact', label: 'Contact & Footer' },
    { value: 'Settings.Checkout', label: 'Checkout & Taxes' },
    { value: 'Settings.Payment', label: 'Payment Systems' }
  ]
};

export interface Role {
  id: number;
  name: string;
  modules: AdminModuleName[];
  // No entries recorded for a module = full access to everything under it (backward
  // compatible default for every role created before this feature existed).
  granularPermissions: string[];
}

export interface RoleRequest {
  name: string;
  modules: AdminModuleName[];
  granularPermissions: string[];
}
