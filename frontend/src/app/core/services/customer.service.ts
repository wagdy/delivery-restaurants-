import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { FindOrCreateCustomerResult, RegisterCustomerRequest } from '../models/customer.model';

@Injectable({ providedIn: 'root' })
export class CustomerService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/customers`;

  registerCustomer(request: RegisterCustomerRequest): Observable<FindOrCreateCustomerResult> {
    return this.http.post<FindOrCreateCustomerResult>(`${this.baseUrl}/register`, request);
  }
}
