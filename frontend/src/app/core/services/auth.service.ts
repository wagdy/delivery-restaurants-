import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthResponse, CreateStaffUserRequest, LoginRequest, RegisterRequest } from '../models/auth.model';
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

  constructor() {
    this.restoreSession();
  }

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
