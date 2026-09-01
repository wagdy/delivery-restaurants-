export type UserRole = 'Customer' | 'Admin' | 'CaptainOrder';

export interface UserProfile {
  id: string;
  email: string;
  fullName: string;
  phoneNumber?: string | null;
  address?: string | null;
  role: UserRole;
}
