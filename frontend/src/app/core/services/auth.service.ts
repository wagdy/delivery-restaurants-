import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  AuthResponse,
  CreateStaffUserRequest,
  ForgotPasswordRequest,
  LoginRequest,
  RegisterRequest,
  ResetPasswordRequest,
  StaffAccount,
  UpdateStaffUserRequest
} from '../models/auth.model';
import { AdminModuleName } from '../models/role.model';
import { UserProfile } from '../models/user.model';

const STORAGE_KEY = 'rd_auth_session';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  private readonly _user = signal<UserProfile | null>(null);
  private readonly _token = signal<string | null>(null);

  readonly currentUser = this._user.asReadonly();
  readonly token = this._token.asReadonly();
  readonly isAuthenticated = computed(() => this._token() !== null);
  readonly isAdmin = computed(() => this._user()?.role === 'Admin');
  readonly isCaptain = computed(() => this._user()?.role === 'CaptainOrder');

  hasModule(name: AdminModuleName): boolean {
    return this._user()?.modules?.includes(name) ?? false;
  }

  // Mirrors the backend's GranularPermissionAuthorizationHandler: no recorded permission
  // claims for this permission's module means "no restriction", full access - so a role
  // created before this feature existed (or one that just never narrowed this module)
  // keeps seeing everything under it.
  hasPermission(permission: string): boolean {
    const granted = this._user()?.granularPermissions ?? [];
    const modulePrefix = permission.split('.')[0] + '.';
    const hasAnyForModule = granted.some((p) => p.startsWith(modulePrefix));
    return !hasAnyForModule || granted.includes(permission);
  }

  constructor() {
    this.restoreSession();
  }

  // Single sign-in entry point for every account - customers and staff alike. See
  // LoginRequest.identifier.
  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/login`, request)
      .pipe(tap((res) => this.setSession(res)));
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http
      .post<AuthResponse>(`${environment.apiUrl}/auth/register`, request)
      .pipe(tap((res) => this.setSession(res)));
  }

  // Admin-only — does not affect the calling admin's own session (no token returned/set).
  createStaffUser(request: CreateStaffUserRequest): Observable<UserProfile> {
    return this.http.post<UserProfile>(`${environment.apiUrl}/auth/staff`, request);
  }

  // The Staff tab's management table (list/edit/delete existing accounts) - a separate
  // api/staff resource from the api/auth/staff creation endpoint above, see StaffController.
  getStaff(): Observable<StaffAccount[]> {
    return this.http.get<StaffAccount[]>(`${environment.apiUrl}/staff`);
  }

  updateStaffUser(id: string, request: UpdateStaffUserRequest): Observable<StaffAccount> {
    return this.http.put<StaffAccount>(`${environment.apiUrl}/staff/${id}`, request);
  }

  // Hard delete - see StaffController/AuthService.DeleteStaffUserAsync's own doc comments.
  deleteStaffUser(id: string): Observable<void> {
    return this.http.delete<void>(`${environment.apiUrl}/staff/${id}`);
  }

  // Always resolves with the same generic message whether or not the phone number is
  // actually registered - the backend deliberately never reveals that (see
  // AuthService.ForgotPasswordAsync). No session side effect either way.
  forgotPassword(request: ForgotPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${environment.apiUrl}/auth/forgot-password`, request);
  }

  // No session side effect - the customer still has to sign in with their new password
  // afterward via the normal login form.
  resetPassword(request: ResetPasswordRequest): Observable<{ message: string }> {
    return this.http.post<{ message: string }>(`${environment.apiUrl}/auth/reset-password`, request);
  }

  logout(): void {
    localStorage.removeItem(STORAGE_KEY);
    this._user.set(null);
    this._token.set(null);
    this.router.navigateByUrl('/');
  }

  private setSession(res: AuthResponse): void {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(res));
    this._user.set(res.user);
    this._token.set(res.token);
  }

  private restoreSession(): void {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return;
    }

    try {
      const session: AuthResponse = JSON.parse(raw);
      if (new Date(session.expiresAtUtc).getTime() <= Date.now()) {
        localStorage.removeItem(STORAGE_KEY);
        return;
      }

      this._user.set(session.user);
      this._token.set(session.token);
    } catch {
      localStorage.removeItem(STORAGE_KEY);
    }
  }
}
