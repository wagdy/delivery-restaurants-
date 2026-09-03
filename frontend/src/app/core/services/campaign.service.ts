import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  Campaign,
  CreateCampaignRequest,
  CustomerCampaignProgress,
  PunchRequest,
  PunchResult,
  RedeemRewardRequest,
  RedeemRewardResult
} from '../models/campaign.model';

@Injectable({ providedIn: 'root' })
export class CampaignService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/campaigns`;

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
    return this.http.get<CustomerCampaignProgress[]>(`${this.baseUrl}/my-progress`);
  }

  punch(request: PunchRequest): Observable<PunchResult> {
    return this.http.post<PunchResult>(`${this.baseUrl}/punch`, request);
  }

  redeemReward(request: RedeemRewardRequest): Observable<RedeemRewardResult> {
    return this.http.post<RedeemRewardResult>(`${this.baseUrl}/redeem-reward`, request);
  }
}
