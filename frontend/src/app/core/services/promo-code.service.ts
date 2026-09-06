import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PromoCode, PromoCodeRequest } from '../models/promo-code.model';

@Injectable({ providedIn: 'root' })
export class PromoCodeService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/promo-codes`;

  getAll(): Observable<PromoCode[]> {
    return this.http.get<PromoCode[]>(this.baseUrl);
  }

  create(request: PromoCodeRequest): Observable<PromoCode> {
    return this.http.post<PromoCode>(this.baseUrl, request);
  }

  update(id: number, request: PromoCodeRequest): Observable<PromoCode> {
    return this.http.put<PromoCode>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
