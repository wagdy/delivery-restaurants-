import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CustomerAnalytics } from '../models/customer-analytics.model';
import { PagedResult } from '../models/order.model';

@Injectable({ providedIn: 'root' })
export class CustomerAnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/crm/customers`;

  getPaged(page: number, pageSize: number, search: string | null): Observable<PagedResult<CustomerAnalytics>> {
    let params = new HttpParams().set('page', page).set('pageSize', pageSize);
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get<PagedResult<CustomerAnalytics>>(this.baseUrl, { params });
  }

  exportToExcel(search: string | null): Observable<Blob> {
    let params = new HttpParams();
    if (search) {
      params = params.set('search', search);
    }
    return this.http.get(`${this.baseUrl}/export`, { params, responseType: 'blob' });
  }
}
