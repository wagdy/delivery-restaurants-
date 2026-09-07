import { AdminModuleName } from './role.model';

export type UserRole = 'Customer' | 'Admin' | 'CaptainOrder';

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  address?: string | null;
  role: UserRole;
  // Effective admin modules granted to this user - null for Customer/CaptainOrder.
  modules?: AdminModuleName[] | null;
  // Granular sub-permissions (e.g. "Orders.Create") within the modules above - null for
  // Customer/CaptainOrder. Absence of any entry for a given module means "no restriction
  // recorded", full access to everything under it - see AuthService.hasPermission.
  granularPermissions?: string[] | null;
}
