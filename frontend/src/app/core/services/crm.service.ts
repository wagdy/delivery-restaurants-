import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { CustomerCrm } from '../models/customer-crm.model';

@Injectable({ providedIn: 'root' })
export class CrmService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/crm`;

  getCustomers(): Observable<CustomerCrm[]> {
    return this.http.get<CustomerCrm[]>(`${this.baseUrl}/customers`);
  }
}
