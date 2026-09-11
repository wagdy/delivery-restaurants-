import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { SurveyMatrixSection, SurveyMatrixSectionRequest } from '../models/review.model';

@Injectable({ providedIn: 'root' })
export class SurveySectionService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/survey-sections`;

  getAll(): Observable<SurveyMatrixSection[]> {
    return this.http.get<SurveyMatrixSection[]>(this.baseUrl);
  }

  create(request: SurveyMatrixSectionRequest): Observable<SurveyMatrixSection> {
    return this.http.post<SurveyMatrixSection>(this.baseUrl, request);
  }

  // Blocked server-side (409) if any question still uses this section - see
  // SurveySectionService.DeleteAsync on the backend.
  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
