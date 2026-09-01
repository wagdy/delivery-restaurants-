import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AddOn, AddOnRequest } from '../models/add-on.model';

@Injectable({ providedIn: 'root' })
export class AddOnService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/addons`;

  getAll(): Observable<AddOn[]> {
    return this.http.get<AddOn[]>(this.baseUrl);
  }

  create(request: AddOnRequest): Observable<AddOn> {
    return this.http.post<AddOn>(this.baseUrl, request);
  }

  update(id: number, request: AddOnRequest): Observable<AddOn> {
    return this.http.put<AddOn>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
