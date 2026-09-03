import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  EarnPointsRequest,
  LoyaltyMe,
  LoyaltyTransactionResult,
  RedeemPointsRequest,
  ScannerCustomer
} from '../models/loyalty.model';

@Injectable({ providedIn: 'root' })
export class LoyaltyService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/loyalty`;

  getMe(): Observable<LoyaltyMe> {
    return this.http.get<LoyaltyMe>(`${this.baseUrl}/me`);
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
}
