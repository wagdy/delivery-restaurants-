import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MenuItemService } from '../../../core/services/menu-item.service';
import { CategoryService } from '../../../core/services/category.service';
import { AddOnService } from '../../../core/services/add-on.service';
import { MenuItem } from '../../../core/models/menu-item.model';
import { Category } from '../../../core/models/category.model';
import { AddOn } from '../../../core/models/add-on.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { MenuItemFormDialogComponent } from '../menu-item-form-dialog/menu-item-form-dialog.component';
import { CategoryManagementDialogComponent } from '../category-management-dialog/category-management-dialog.component';
import { AddOnManagementDialogComponent } from '../add-on-management-dialog/add-on-management-dialog.component';

type AvailabilityFilter = 'all' | 'available' | 'unavailable';
type AddOnsFilter = 'all' | 'has' | 'none';

@Component({
  selector: 'app-menu-management',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatToolbarModule
  ],
  templateUrl: './menu-management.component.html',
  styleUrl: './menu-management.component.scss'
})
export class MenuManagementComponent {
  private readonly menuItemService = inject(MenuItemService);
  private readonly categoryService = inject(CategoryService);
  private readonly addOnService = inject(AddOnService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly menuItems = signal<MenuItem[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly addOns = signal<AddOn[]>([]);

  // The four active filter criteria - applyFilters() reads all of them together on
  // every call, so "search X AND category Y AND available Z" is always one combined
  // server query rather than several client-side passes layered on top of each other.
  readonly searchTerm = signal('');
  readonly categoryFilter = signal<number | null>(null);
  readonly availabilityFilter = signal<AvailabilityFilter>('all');
  readonly addOnsFilter = signal<AddOnsFilter>('all');

  readonly displayedColumns = ['photo', 'name', 'category', 'price', 'available', 'actions'];

  readonly categoryNames = computed(() => this.categories().map((c) => c.name));

  // Keystrokes go through this Subject rather than straight onto searchTerm, so typing
  // doesn't fire a request per character - dropdown changes still apply immediately.
  private readonly searchInput$ = new Subject<string>();

  constructor() {
    this.searchInput$.pipe(debounceTime(300), distinctUntilChanged()).subscribe((term) => {
      this.searchTerm.set(term);
      this.applyFilters();
    });

    this.applyFilters();
    this.loadCategories();
    this.loadAddOns();
  }

  onSearchInput(value: string): void {
    this.searchInput$.next(value);
  }

  onCategoryFilterChange(categoryId: number | null): void {
    this.categoryFilter.set(categoryId);
    this.applyFilters();
  }

  onAvailabilityFilterChange(value: AvailabilityFilter): void {
    this.availabilityFilter.set(value);
    this.applyFilters();
  }

  onAddOnsFilterChange(value: AddOnsFilter): void {
    this.addOnsFilter.set(value);
    this.applyFilters();
  }

  // The unified filtering method: reads every active filter and fetches the matching
  // menu items in a single request.
  applyFilters(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    const availability = this.availabilityFilter();
    const addOns = this.addOnsFilter();

    this.menuItemService
      .getAll({
        searchQuery: this.searchTerm().trim() || undefined,
        categoryId: this.categoryFilter() ?? undefined,
        isAvailable: availability === 'all' ? undefined : availability === 'available',
        hasAddons: addOns === 'all' ? undefined : addOns === 'has'
      })
      .subscribe({
        next: (items) => {
          this.menuItems.set(items);
          this.loading.set(false);
        },
        error: () => {
          this.loading.set(false);
          this.errorMessage.set('Failed to load menu items.');
        }
      });
  }

  loadCategories(): void {
    this.categoryService.getAll().subscribe({
      next: (categories) => this.categories.set(categories),
      error: () => this.snackBar.open('Failed to load categories.', 'Dismiss', { duration: 4000 })
    });
  }

  loadAddOns(): void {
    this.addOnService.getAll().subscribe({
      next: (addOns) => this.addOns.set(addOns),
      error: () => this.snackBar.open('Failed to load add-ons.', 'Dismiss', { duration: 4000 })
    });
  }

  openCategoryManagement(): void {
    const dialogRef = this.dialog.open(CategoryManagementDialogComponent, { width: '480px' });

    dialogRef.afterClosed().subscribe((mutated: boolean | undefined) => {
      if (mutated) {
        this.loadCategories();
        this.applyFilters();
      }
    });
  }

  openAddOnManagement(): void {
    const dialogRef = this.dialog.open(AddOnManagementDialogComponent, { width: '540px' });

    dialogRef.afterClosed().subscribe((mutated: boolean | undefined) => {
      if (mutated) {
        this.loadAddOns();
        this.applyFilters();
      }
    });
  }

  openCreate(): void {
    const dialogRef = this.dialog.open(MenuItemFormDialogComponent, {
      width: '520px',
      data: { mode: 'create', categories: this.categoryNames(), availableAddOns: this.addOns() }
    });

    dialogRef.afterClosed().subscribe((created) => {
      if (created) {
        this.applyFilters();
      }
    });
  }

  openEdit(menuItem: MenuItem): void {
    const dialogRef = this.dialog.open(MenuItemFormDialogComponent, {
      width: '520px',
      data: {
        mode: 'edit',
        menuItem,
        categories: this.categoryNames(),
        availableAddOns: this.addOns()
      }
    });

    dialogRef.afterClosed().subscribe((updated) => {
      if (updated) {
        this.applyFilters();
      }
    });
  }

  toggleAvailability(menuItem: MenuItem): void {
    const nextAvailable = !menuItem.isAvailable;
    this.menuItemService
      .update(menuItem.id, {
        name: menuItem.name,
        description: menuItem.description,
        price: menuItem.price,
        category: menuItem.category,
        imageUrl: menuItem.imageUrl,
        isAvailable: nextAvailable,
        addOnIds: menuItem.addOns.map((a) => a.id)
      })
      .subscribe({
        next: () => this.applyFilters(),
        error: () => this.snackBar.open('Failed to update availability.', 'Dismiss', { duration: 4000 })
      });
  }

  delete(menuItem: MenuItem): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete menu item',
        message: `Permanently delete "${menuItem.name}"? This cannot be undone.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.menuItemService.delete(menuItem.id).subscribe({
        next: () => this.applyFilters(),
        error: (err) => {
          const message = err.error?.errors?.[0] ?? 'Failed to delete menu item.';
          this.snackBar.open(message, 'Dismiss', { duration: 6000 });
        }
      });
    });
  }
}
