import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
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
  readonly searchTerm = signal('');

  readonly displayedColumns = ['photo', 'name', 'category', 'price', 'available', 'actions'];

  readonly categoryNames = computed(() => this.categories().map((c) => c.name));

  readonly filteredItems = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const items = this.menuItems();
    if (!term) {
      return items;
    }
    return items.filter(
      (m) => m.name.toLowerCase().includes(term) || m.category.toLowerCase().includes(term)
    );
  });

  constructor() {
    this.load();
    this.loadCategories();
    this.loadAddOns();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);
    this.menuItemService.getAll().subscribe({
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
        this.load();
      }
    });
  }

  openAddOnManagement(): void {
    const dialogRef = this.dialog.open(AddOnManagementDialogComponent, { width: '540px' });

    dialogRef.afterClosed().subscribe((mutated: boolean | undefined) => {
      if (mutated) {
        this.loadAddOns();
        this.load();
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
        this.load();
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
        this.load();
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
        next: () => this.load(),
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
        next: () => this.load(),
        error: (err) => {
          const message = err.error?.errors?.[0] ?? 'Failed to delete menu item.';
          this.snackBar.open(message, 'Dismiss', { duration: 6000 });
        }
      });
    });
  }
}
