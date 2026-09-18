import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { BulkActionResult, BulkMenuItemImportResult, MenuItem, MenuItemFilter, MenuItemRequest } from '../models/menu-item.model';

// Mirrors the server-side cache on this exact query (see ReadThroughCache), so the two
// behave as one system rather than two with different ideas about how stale is too stale.
const AVAILABLE_ITEMS_TTL_MS = 60_000;

@Injectable({ providedIn: 'root' })
export class MenuItemService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/menuitems`;

  private availableItems$: Observable<MenuItem[]> | null = null;
  private availableItemsFetchedAt = 0;

  // The full available-items list, shared by everything that needs it.
  //
  // Two separate callers ask for this on the same page load - StorefrontComponent for the
  // grid, and CategoryService.loadActiveCategoryNames for the sidebar's category list -
  // and they were each issuing their own request, so every visitor downloaded the same
  // 62 KB payload twice. They are constructed moments apart rather than simultaneously,
  // so simple in-flight deduplication would not reliably catch it; this holds the result
  // for a short window instead.
  //
  // Dropped immediately by any menu mutation below, so an admin who edits a price does
  // not then see the old one on the storefront.
  getAvailable(): Observable<MenuItem[]> {
    const isFresh =
      this.availableItems$ !== null && Date.now() - this.availableItemsFetchedAt < AVAILABLE_ITEMS_TTL_MS;

    if (!isFresh) {
      this.availableItemsFetchedAt = Date.now();
      this.availableItems$ = this.getAll({ isAvailable: true }).pipe(
        // Without this a failed request would be replayed to every later caller for the
        // rest of the window - clearing the field means the next one retries instead.
        // Callers already subscribed still see the error, which is correct: they did fail.
        tap({ error: () => this.invalidateAvailable() }),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }

    return this.availableItems$!;
  }

  // Public because category writes have to drop this too: renaming a category rewrites
  // MenuItem.Category on every item beneath it, so a held menu list would still be
  // grouped under the old name. Mirrors what the server-side cache already does across
  // its own groups.
  invalidateAvailable(): void {
    this.availableItems$ = null;
    this.availableItemsFetchedAt = 0;
  }

  getAll(filter?: MenuItemFilter): Observable<MenuItem[]> {
    let params = new HttpParams();
    if (filter?.searchQuery) params = params.set('searchQuery', filter.searchQuery);
    if (filter?.categoryId !== undefined) params = params.set('categoryId', filter.categoryId);
    if (filter?.isAvailable !== undefined) params = params.set('isAvailable', filter.isAvailable);
    if (filter?.hasAddons !== undefined) params = params.set('hasAddons', filter.hasAddons);
    if (filter?.deleted) params = params.set('deleted', filter.deleted);
    return this.http.get<MenuItem[]>(this.baseUrl, { params });
  }

  getById(id: number): Observable<MenuItem> {
    return this.http.get<MenuItem>(`${this.baseUrl}/${id}`);
  }

  create(request: MenuItemRequest): Observable<MenuItem> {
    return this.http.post<MenuItem>(this.baseUrl, request).pipe(tap(() => this.invalidateAvailable()));
  }

  update(id: number, request: MenuItemRequest): Observable<MenuItem> {
    return this.http.put<MenuItem>(`${this.baseUrl}/${id}`, request).pipe(tap(() => this.invalidateAvailable()));
  }

  // Bulk counterparts to delete/update below. Both invalidate the shared menu cache the
  // same way, so the storefront stops serving rows that just disappeared from the admin.
  bulkDelete(ids: number[]): Observable<BulkActionResult> {
    return this.http
      .post<BulkActionResult>(`${this.baseUrl}/bulk-delete`, { ids })
      .pipe(tap(() => this.invalidateAvailable()));
  }

  bulkRestore(ids: number[]): Observable<BulkActionResult> {
    return this.http
      .post<BulkActionResult>(`${this.baseUrl}/bulk-restore`, { ids })
      .pipe(tap(() => this.invalidateAvailable()));
  }

  bulkSetAvailability(ids: number[], isAvailable: boolean): Observable<BulkActionResult> {
    return this.http
      .post<BulkActionResult>(`${this.baseUrl}/bulk-availability`, { ids, isAvailable })
      .pipe(tap(() => this.invalidateAvailable()));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(tap(() => this.invalidateAvailable()));
  }

  uploadImage(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/upload-image`, formData);
  }

  downloadExcelTemplate(): Observable<Blob> {
    return this.http.get(`${this.baseUrl}/excel-template`, { responseType: 'blob' });
  }

  bulkUpload(file: File): Observable<BulkMenuItemImportResult> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<BulkMenuItemImportResult>(`${this.baseUrl}/bulk-upload`, formData).pipe(tap(() => this.invalidateAvailable()));
  }
}
