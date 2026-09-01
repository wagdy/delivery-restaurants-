import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SyncOrdersResult } from '../models/dgtera-sync.model';

@Injectable({ providedIn: 'root' })
export class DgteraSyncService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/sync`;

  syncOrders(): Observable<SyncOrdersResult> {
    return this.http.post<SyncOrdersResult>(`${this.baseUrl}/orders`, {});
  }
}
