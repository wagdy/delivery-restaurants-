import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { PagedResult } from '../models/order.model';
import {
  OrderReview,
  OrderReviewDetail,
  SubmitReviewRequest,
  SurveyQuestion,
  SurveyQuestionRequest
} from '../models/review.model';

@Injectable({ providedIn: 'root' })
export class ReviewService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/reviews`;
  private readonly publicBaseUrl = `${environment.apiUrl}/public/reviews`;

  // activeOnly is omitted (not just passed false) by the admin builder, which needs to
  // see and re-activate retired questions too - only a future public survey page would
  // pass true.
  getQuestions(activeOnly?: boolean): Observable<SurveyQuestion[]> {
    return this.http.get<SurveyQuestion[]>(`${this.baseUrl}/questions`, {
      params: activeOnly ? { activeOnly } : {}
    });
  }

  createQuestion(request: SurveyQuestionRequest): Observable<SurveyQuestion> {
    return this.http.post<SurveyQuestion>(`${this.baseUrl}/questions`, request);
  }

  updateQuestion(id: number, request: SurveyQuestionRequest): Observable<SurveyQuestion> {
    return this.http.put<SurveyQuestion>(`${this.baseUrl}/questions/${id}`, request);
  }

  deleteQuestion(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/questions/${id}`);
  }

  getAll(page: number, pageSize: number): Observable<PagedResult<OrderReview>> {
    return this.http.get<PagedResult<OrderReview>>(this.baseUrl, { params: { page, pageSize } });
  }

  getById(id: number): Observable<OrderReviewDetail> {
    return this.http.get<OrderReviewDetail>(`${this.baseUrl}/${id}`);
  }

  // --- Public survey page (/rate/store, /rate/order/:orderId) - no auth required. ---

  getPublicQuestions(): Observable<SurveyQuestion[]> {
    return this.http.get<SurveyQuestion[]>(`${this.publicBaseUrl}/questions`);
  }

  submit(request: SubmitReviewRequest): Observable<OrderReviewDetail> {
    return this.http.post<OrderReviewDetail>(`${this.publicBaseUrl}/submit`, request);
  }
}
