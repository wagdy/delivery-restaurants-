import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { ValidatePromoRequest, ValidatePromoResponse } from '../models/checkout.model';

@Injectable({ providedIn: 'root' })
export class CheckoutService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/checkout`;

  validatePromo(request: ValidatePromoRequest): Observable<ValidatePromoResponse> {
    return this.http.post<ValidatePromoResponse>(`${this.baseUrl}/validate-promo`, request);
  }
}
