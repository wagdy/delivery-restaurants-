import { UserProfile, UserRole } from './user.model';

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  user: UserProfile;
}

// Single sign-in entry point for every account - customers and staff alike. identifier is
// either an email address or a phone number; the backend detects which (AuthService.LoginAsync).
export interface LoginRequest {
  identifier: string;
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
// by phone number, not email — see LoginRequest.
export interface CreateStaffUserRequest {
  fullName: string;
  phoneNumber: string;
  password: string;
  role: UserRole;
  // Required when role is 'Admin' (must reference an existing Role); omitted/null when
  // role is 'CaptainOrder'.
  roleId?: number | null;
}
