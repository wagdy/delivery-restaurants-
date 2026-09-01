import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { MenuItem, MenuItemFilter, MenuItemRequest } from '../models/menu-item.model';

@Injectable({ providedIn: 'root' })
export class MenuItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/menuitems`;

  getAll(filter?: MenuItemFilter): Observable<MenuItem[]> {
    let params = new HttpParams();
    if (filter?.searchQuery) params = params.set('searchQuery', filter.searchQuery);
    if (filter?.categoryId !== undefined) params = params.set('categoryId', filter.categoryId);
    if (filter?.isAvailable !== undefined) params = params.set('isAvailable', filter.isAvailable);
    if (filter?.hasAddons !== undefined) params = params.set('hasAddons', filter.hasAddons);
    return this.http.get<MenuItem[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<MenuItem> {
    return this.http.get<MenuItem>(`${this.baseUrl}/${id}`);
  }

  create(request: MenuItemRequest): Observable<MenuItem> {
    return this.http.post<MenuItem>(this.baseUrl, request);
  }

  update(id: number, request: MenuItemRequest): Observable<MenuItem> {
    return this.http.put<MenuItem>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  uploadImage(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/upload-image`, formData);
  }
}
