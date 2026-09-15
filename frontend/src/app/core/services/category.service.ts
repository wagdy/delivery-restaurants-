import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { forkJoin, Observable, shareReplay, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Category } from '../models/category.model';
import { MenuItemService } from './menu-item.service';
import { deriveActiveCategoryNames } from '../../shared/utils/active-categories.util';

// Matches MenuItemService's own window, and the server-side cache on this data.
const CATEGORIES_TTL_MS = 60_000;

@Injectable({ providedIn: 'root' })
export class CategoryService {
  private readonly http = inject(HttpClient);
  private readonly menuItemService = inject(MenuItemService);
  private readonly baseUrl = `${environment.apiUrl}/categories`;

  // Single source of truth for "does this category actually have anything to show a
  // customer" - every category name currently in active use by at least one menu item,
  // derived via the same deriveActiveCategoryNames() helper the Menu's own category
  // rail/grid uses (storefront.component.ts), so the two can never disagree again (a
  // category with zero items used to still show up as a dead end in the hamburger
  // drawer even though the Menu already knew to hide it). getAll() below still returns
  // every category unfiltered - admin screens (CategoryManagementDialog, menu item
  // forms, promo/campaign category pickers) need to see and manage empty categories
  // too, e.g. to add the first item to a newly-created one.
  // The Category rows themselves, not just their names: the sidebar renders these and
  // needs nameAr to localize its labels. Name stays the identity the click handler
  // selects by, so what this adds is the Arabic label, not a new key.
  private readonly _activeCategories = signal<Category[]>([]);
  readonly activeCategories = this._activeCategories.asReadonly();
  private activeCategoryNamesRequested = false;

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>(this.baseUrl);
  }

  // Same story as MenuItemService.getAvailable, and the same page load: this list is
  // requested once here for the sidebar and once by StorefrontComponent for its display
  // order. Small next to the menu payload, but it is the identical request twice, and the
  // fix is the same. Dropped by every category write below.
  getAllShared(): Observable<Category[]> {
    const isFresh = this.allCategories$ !== null && Date.now() - this.allCategoriesFetchedAt < CATEGORIES_TTL_MS;

    if (!isFresh) {
      this.allCategoriesFetchedAt = Date.now();
      this.allCategories$ = this.getAll().pipe(
        tap({ error: () => this.invalidateShared() }),
        shareReplay({ bufferSize: 1, refCount: false })
      );
    }

    return this.allCategories$!;
  }

  private allCategories$: Observable<Category[]> | null = null;
  private allCategoriesFetchedAt = 0;

  // Drops the menu list as well as the category list, for the same reason the server
  // does: renaming a category rewrites MenuItem.Category on every item beneath it, so a
  // held menu would still be grouped under the old name.
  private invalidateShared(): void {
    this.allCategories$ = null;
    this.allCategoriesFetchedAt = 0;
    this.menuItemService.invalidateAvailable();
  }

  // Called by the hamburger drawer (app.component.ts) on init. Guarded so a second
  // caller elsewhere is a no-op rather than re-fetching/re-deriving the same result -
  // this rarely changes within a single session.
  loadActiveCategoryNames(): void {
    if (this.activeCategoryNamesRequested) {
      return;
    }
    this.activeCategoryNamesRequested = true;

    // getAvailable() rather than a second getAll({ isAvailable: true }): it is the same
    // query StorefrontComponent runs moments later for the grid, so sharing it means one
    // request per page load instead of two identical ones. It also guarantees both derive
    // from byte-identical data - a category whose items are all out of stock has to look
    // empty in the sidebar and the Menu alike, which is the drift this whole path exists
    // to prevent.
    forkJoin([this.getAllShared(), this.menuItemService.getAvailable()]).subscribe(([categories, menuItems]) => {
      const ordered = [...categories].sort((a, b) => a.displayOrder - b.displayOrder);
      // deriveActiveCategoryNames stays the single source of "which categories have
      // items"; this maps its answer back onto the rows so the labels can localize
      // without a second definition of active.
      const activeNames = new Set(deriveActiveCategoryNames(ordered, menuItems));
      this._activeCategories.set(ordered.filter((c) => activeNames.has(c.name)));
    });
  }

  // FormData, not JSON - the backend binds these as [FromForm] CategoryFormRequest so it
  // can accept an optional image file (multipart/form-data) alongside the name.
  create(formData: FormData): Observable<Category> {
    return this.http.post<Category>(this.baseUrl, formData).pipe(tap(() => this.invalidateShared()));
  }

  update(id: number, formData: FormData): Observable<Category> {
    return this.http.put<Category>(`${this.baseUrl}/${id}`, formData).pipe(tap(() => this.invalidateShared()));
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`).pipe(tap(() => this.invalidateShared()));
  }

  reorder(orderedIds: number[]): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/reorder`, { orderedIds }).pipe(tap(() => this.invalidateShared()));
  }
}
