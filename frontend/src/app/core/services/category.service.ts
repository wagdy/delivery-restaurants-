import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { forkJoin, Observable } from 'rxjs';
import { environment } from '../../../environments/environment';
import { Category } from '../models/category.model';
import { MenuItemService } from './menu-item.service';
import { deriveActiveCategoryNames } from '../../shared/utils/active-categories.util';

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
  private readonly _activeCategoryNames = signal<string[]>([]);
  readonly activeCategoryNames = this._activeCategoryNames.asReadonly();
  private activeCategoryNamesRequested = false;

  getAll(): Observable<Category[]> {
    return this.http.get<Category[]>(this.baseUrl);
  }

  // Called by the hamburger drawer (app.component.ts) on init. Guarded so a second
  // caller elsewhere is a no-op rather than re-fetching/re-deriving the same result -
  // this rarely changes within a single session.
  loadActiveCategoryNames(): void {
    if (this.activeCategoryNamesRequested) {
      return;
    }
    this.activeCategoryNamesRequested = true;

    // { isAvailable: true } matches StorefrontComponent's own menu item fetch exactly -
    // without it, a category whose items are all currently marked unavailable (out of
    // stock) would count as "active" here while the Menu itself already treats it as
    // empty, reintroducing the same drift this change exists to eliminate.
    forkJoin([this.getAll(), this.menuItemService.getAll({ isAvailable: true })]).subscribe(([categories, menuItems]) => {
      const ordered = [...categories].sort((a, b) => a.displayOrder - b.displayOrder);
      this._activeCategoryNames.set(deriveActiveCategoryNames(ordered, menuItems));
    });
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
