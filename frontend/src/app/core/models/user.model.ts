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
}
