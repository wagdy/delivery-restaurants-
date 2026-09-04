import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SubCategory, SubCategoryRequest } from '../models/sub-category.model';

@Injectable({ providedIn: 'root' })
export class SubCategoryService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/subcategories`;

  getAll(): Observable<SubCategory[]> {
    return this.http.get<SubCategory[]>(this.baseUrl);
  }

  create(request: SubCategoryRequest): Observable<SubCategory> {
    return this.http.post<SubCategory>(this.baseUrl, request);
  }

  update(id: number, request: SubCategoryRequest): Observable<SubCategory> {
    return this.http.put<SubCategory>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  reorder(categoryId: number, orderedIds: number[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/reorder`, { categoryId, orderedIds });
  }
}
