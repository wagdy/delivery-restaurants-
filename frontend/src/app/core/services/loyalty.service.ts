import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  EarnPointsRequest,
  LoyaltyMe,
  LoyaltySettings,
  LoyaltyTransactionResult,
  PointsUpdatedEvent,
  RedeemPointsRequest,
  ScannerCustomer,
  UpdateLoyaltySettingsRequest
} from '../models/loyalty.model';

@Injectable({ providedIn: 'root' })
export class LoyaltyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/loyalty`;

  // Shared state (not just a per-call Observable) so every consumer - the Rewards tab,
  // the realtime service pushing live updates - reads the same live value. Mirrors
  // SettingsService's existing load()-taps-into-a-signal pattern.
  private readonly _me = signal<LoyaltyMe | null>(null);
  readonly me = this._me.asReadonly();

  getMe(): Observable<LoyaltyMe> {
    return this.http.get<LoyaltyMe>(`${this.baseUrl}/me`).pipe(tap((me) => this._me.set(me)));
  }

  // Called by LoyaltyRealtimeService when a "PointsUpdated" event arrives - merges just
  // the changed fields, preserving referralCode/wallet-availability from the last fetch.
  applyPointsUpdate(event: PointsUpdatedEvent): void {
    this._me.update((current) =>
      current
        ? {
            ...current,
            currentPoints: event.currentPoints,
            totalLifetimePoints: event.totalLifetimePoints,
            membershipTier: event.membershipTier
          }
        : current
    );
  }

  earn(request: EarnPointsRequest): Observable<LoyaltyTransactionResult> {
    return this.http.post<LoyaltyTransactionResult>(`${this.baseUrl}/earn`, request);
  }

  redeem(request: RedeemPointsRequest): Observable<LoyaltyTransactionResult> {
    return this.http.post<LoyaltyTransactionResult>(`${this.baseUrl}/redeem`, request);
  }

  // The jwt interceptor attaches the bearer token automatically; a plain <a href> can't,
  // which is why the Apple pass has to be fetched as a blob and opened from an Object URL
  // rather than linked to directly.
  getAppleWalletPassBlob(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/wallet/apple/pass`, { responseType: 'blob' });
  }

  getGoogleWalletSaveLink(): Observable<{ saveUrl: string }> {
    return this.http.get<{ saveUrl: string }>(`${this.baseUrl}/wallet/google/save-link`);
  }

  // For the Scanner tool - customerId comes from decoding a customer's digital-card QR.
  getScannerCustomer(customerId: string): Observable<ScannerCustomer> {
    return this.http.get<ScannerCustomer>(`${this.baseUrl}/scanner/${customerId}`);
  }

  // Manual fallback for the Scanner tool when a QR code can't be scanned.
  getScannerCustomerByPhone(phone: string): Observable<ScannerCustomer> {
    return this.http.get<ScannerCustomer>(`${this.baseUrl}/scanner/by-phone`, { params: { phone } });
  }

  // Global earn/redeem ratios shown in the Campaign Manager's Points Configuration card.
  getSettings(): Observable<LoyaltySettings> {
    return this.http.get<LoyaltySettings>(`${this.baseUrl}/settings`);
  }

  updateSettings(request: UpdateLoyaltySettingsRequest): Observable<LoyaltySettings> {
    return this.http.put<LoyaltySettings>(`${this.baseUrl}/settings`, request);
  }
}
