import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Campaign,
  CreateCampaignRequest,
  CustomerCampaignProgress,
  PunchRequest,
  PunchResult,
  PunchUpdatedEvent,
  RedeemRewardRequest,
  RedeemRewardResult
} from '../models/campaign.model';

@Injectable({ providedIn: 'root' })
export class CampaignService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/campaigns`;

  // Shared state, mirroring LoyaltyService.me - the realtime service pushes live punch
  // updates into this same signal the Rewards tab reads from.
  private readonly _myProgress = signal<CustomerCampaignProgress[]>([]);
  readonly myProgress = this._myProgress.asReadonly();

  getAll(): Observable<Campaign[]> {
    return this.http.get<Campaign[]>(this.baseUrl);
  }

  create(request: CreateCampaignRequest): Observable<Campaign> {
    return this.http.post<Campaign>(this.baseUrl, request);
  }

  toggleStatus(id: string): Observable<Campaign> {
    return this.http.put<Campaign>(`${this.baseUrl}/${id}/toggle-status`, {});
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  getMyProgress(): Observable<CustomerCampaignProgress[]> {
    return this.http
      .get<CustomerCampaignProgress[]>(`${this.baseUrl}/my-progress`)
      .pipe(tap((progress) => this._myProgress.set(progress)));
  }

  punch(request: PunchRequest): Observable<PunchResult> {
    return this.http.post<PunchResult>(`${this.baseUrl}/punch`, request);
  }

  redeemReward(request: RedeemRewardRequest): Observable<RedeemRewardResult> {
    return this.http.post<RedeemRewardResult>(`${this.baseUrl}/redeem-reward`, request);
  }

  // Called by LoyaltyRealtimeService when a "PunchUpdated" event arrives. No-ops if the
  // campaign isn't present yet (only possible for a campaign created after the last
  // fetch - a rare edge case the customer's next real fetch will pick up).
  applyPunchUpdate(event: PunchUpdatedEvent): void {
    this._myProgress.update((current) => {
      const index = current.findIndex((c) => c.campaignId === event.campaignId);
      if (index === -1) {
        return current;
      }

      const updated = [...current];
      updated[index] = {
        ...updated[index],
        currentPunches: event.currentPunches,
        rewardsEarned: event.rewardsEarned
      };
      return updated;
    });
  }
}
