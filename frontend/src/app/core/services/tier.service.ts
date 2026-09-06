import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Tier, TierRequest } from '../models/tier.model';

@Injectable({ providedIn: 'root' })
export class TierService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/tiers`;

  getAll(): Observable<Tier[]> {
    return this.http.get<Tier[]>(this.baseUrl);
  }

  create(request: TierRequest): Observable<Tier> {
    return this.http.post<Tier>(this.baseUrl, request);
  }

  update(id: number, request: TierRequest): Observable<Tier> {
    return this.http.put<Tier>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
