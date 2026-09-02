import { UserProfile, UserRole } from './user.model';

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  user: UserProfile;
}

// Email-based login, kept for the two pre-existing seeded staff accounts and any legacy
// account that predates the switch to phone-based login — see PhoneLoginRequest below,
// which customers and newly-created staff use instead.
export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  phoneNumber: string;
  password: string;
  fullName: string;
  address?: string;
}

// Admin-only — creates an Admin or CaptainOrder account. Customer role is intentionally
// not selectable here: customers always self-register via RegisterRequest. Staff log in
// by phone number, not email — see PhoneLoginRequest.
export interface CreateStaffUserRequest {
  fullName: string;
  phoneNumber: string;
  password: string;
  role: UserRole;
  // Required when role is 'Admin' (must reference an existing Role); omitted/null when
  // role is 'CaptainOrder'.
  roleId?: number | null;
}

// Phone + password login, used by customers and staff alike.
export interface PhoneLoginRequest {
  phoneNumber: string;
  password: string;
}
