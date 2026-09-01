import { UserProfile, UserRole } from './user.model';

export interface AuthResponse {
  token: string;
  expiresAtUtc: string;
  user: UserProfile;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface RegisterRequest {
  email: string;
  password: string;
  fullName: string;
  phoneNumber?: string;
  address?: string;
}

// Admin-only — creates an Admin or CaptainOrder account. Customer role is intentionally
// not selectable here: customers always self-register via RegisterRequest.
export interface CreateStaffUserRequest {
  fullName: string;
  email: string;
  phoneNumber?: string;
  password: string;
  role: UserRole;
}
