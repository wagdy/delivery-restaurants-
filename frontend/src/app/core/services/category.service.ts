import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Category } from '../models/category.model';

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/categories`;

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>(this.baseUrl);
  }

  // FormData, not JSON - the backend binds these as [FromForm] CategoryFormRequest so it
  // can accept an optional image file (multipart/form-data) alongside the name.
  create(formData: FormData): Observable<Category> {
    return this.http.post<Category>(this.baseUrl, formData);
  }

  update(id: number, formData: FormData): Observable<Category> {
    return this.http.put<Category>(`${this.baseUrl}/${id}`, formData);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  reorder(orderedIds: number[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/reorder`, { orderedIds });
  }
}
