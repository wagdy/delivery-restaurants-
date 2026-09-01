import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CustomerInsight } from '../models/customer-insight.model';

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/customers`;

  getInsights(): Observable<CustomerInsight[]> {
    return this.http.get<CustomerInsight[]>(`${this.baseUrl}/insights`);
  }
}
