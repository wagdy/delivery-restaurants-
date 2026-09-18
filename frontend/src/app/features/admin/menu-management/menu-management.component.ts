import { Component, ElementRef, ViewChild, inject, signal } from '@angular/core';
import { SelectionModel } from '@angular/cdk/collections';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subject, debounceTime, distinctUntilChanged } from 'rxjs';
import { saveAs } from 'file-saver';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { MenuItemService } from '../../../core/services/menu-item.service';
import { CategoryService } from '../../../core/services/category.service';
import { SubCategoryService } from '../../../core/services/sub-category.service';
import { AddOnService } from '../../../core/services/add-on.service';
import { BulkActionResult, DeletedFilter, MenuItem } from '../../../core/models/menu-item.model';
import { Category } from '../../../core/models/category.model';
import { SubCategory } from '../../../core/models/sub-category.model';
import { AddOn } from '../../../core/models/add-on.model';
import { ConfirmDialogComponent, ConfirmDialogData } from '../../../shared/confirm-dialog/confirm-dialog.component';
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
    MatCheckboxModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './menu-management.component.html',
  styleUrl: './menu-management.component.scss'
})
export class MenuManagementComponent {
  private readonly menuItemService = inject(MenuItemService);
  private readonly categoryService = inject(CategoryService);
  private readonly subCategoryService = inject(SubCategoryService);
  private readonly addOnService = inject(AddOnService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  @ViewChild('fileInput') private readonly fileInput?: ElementRef<HTMLInputElement>;

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly menuItems = signal<MenuItem[]>([]);
  readonly categories = signal<Category[]>([]);
  readonly subCategories = signal<SubCategory[]>([]);
  readonly addOns = signal<AddOn[]>([]);
  readonly downloadingTemplate = signal(false);
  readonly uploading = signal(false);

  // The four active filter criteria - applyFilters() reads all of them together on
  // every call, so "search X AND category Y AND available Z" is always one combined
  // server query rather than several client-side passes layered on top of each other.
  readonly searchTerm = signal('');
  readonly categoryFilter = signal<number | null>(null);
  readonly availabilityFilter = signal<AvailabilityFilter>('all');
  readonly addOnsFilter = signal<AddOnsFilter>('all');

  // 'Active' is the default and matches the server's own default, so the table opens on
  // the live menu exactly as it always has. The other two values are what make a soft
  // delete recoverable without going to the database.
  readonly deletedFilter = signal<DeletedFilter>('Active');

  // True when the current view can contain deleted rows - drives whether the bulk bar
  // offers Restore, and whether the per-row action is Delete or Restore.
  readonly showingDeleted = signal(false);

  readonly displayedColumns = ['select', 'photo', 'name', 'category', 'price', 'available', 'actions'];

  // ---------------------------------------------------------------------------
  // Bulk selection
  // ---------------------------------------------------------------------------

  // Angular CDK's SelectionModel rather than a Set of ids: it already has the
  // toggle/clear/hasValue semantics the header checkbox and the action bar need, and it
  // is what the Material table examples use, so the next person recognises it.
  readonly selection = new SelectionModel<number>(true, []);

  // A plain signal mirroring the model, because SelectionModel is not reactive - the
  // template reads this so the action bar appears the moment a row is ticked, instead of
  // waiting for some unrelated change detection to notice.
  readonly selectedCount = signal(0);

  readonly bulkWorking = signal(false);

  private syncSelectionCount(): void {
    this.selectedCount.set(this.selection.selected.length);
  }

  toggleRow(id: number): void {
    this.selection.toggle(id);
    this.syncSelectionCount();
  }

  isRowSelected(id: number): boolean {
    return this.selection.isSelected(id);
  }

  // "All" means everything currently on screen after filtering - not everything in the
  // database. Ticking the header while a category filter is active and then deleting
  // should not reach rows the admin cannot see.
  isAllVisibleSelected(): boolean {
    const visible = this.menuItems();
    return visible.length > 0 && visible.every((i) => this.selection.isSelected(i.id));
  }

  isSomeVisibleSelected(): boolean {
    const visible = this.menuItems();
    return visible.some((i) => this.selection.isSelected(i.id)) && !this.isAllVisibleSelected();
  }

  toggleAllVisible(): void {
    if (this.isAllVisibleSelected()) {
      this.menuItems().forEach((i) => this.selection.deselect(i.id));
    } else {
      this.menuItems().forEach((i) => this.selection.select(i.id));
    }
    this.syncSelectionCount();
  }

  clearSelection(): void {
    this.selection.clear();
    this.syncSelectionCount();
  }

  // Deleting is irreversible from the admin's point of view even though the row survives
  // in the database, so it asks first and names the count - this is the button that can
  // empty a menu in one press.
  bulkDelete(): void {
    const ids = this.selection.selected;
    if (ids.length === 0) {
      return;
    }

    const plural = ids.length === 1 ? '' : 's';
    const ref = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: `Delete ${ids.length} item${plural}?`,
        message:
          `${ids.length} item${plural} will be removed from the menu. ` +
          `Past orders that include ${ids.length === 1 ? 'it' : 'them'} keep their receipts intact.`,
        confirmLabel: 'Delete',
        danger: true
      } satisfies ConfirmDialogData
    });

    ref.afterClosed().subscribe((confirmed: boolean | undefined) => {
      if (!confirmed) {
        return;
      }

      this.bulkWorking.set(true);
      this.menuItemService.bulkDelete(ids).subscribe({
        next: (result) => {
          this.bulkWorking.set(false);
          this.clearSelection();
          this.applyFilters();
          this.reportBulkResult(result, 'deleted');
        },
        error: (err) => {
          this.bulkWorking.set(false);
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete the selected items.', 'Dismiss', { duration: 5000 });
        }
      });
    });
  }

  // Undo for bulkDelete. No confirm dialog: restoring is the safe direction, and an
  // admin who restores the wrong row can simply delete it again.
  bulkRestore(): void {
    const ids = this.selection.selected;
    if (ids.length === 0) {
      return;
    }

    this.bulkWorking.set(true);
    this.menuItemService.bulkRestore(ids).subscribe({
      next: (result) => {
        this.bulkWorking.set(false);
        this.clearSelection();
        this.applyFilters();
        this.reportBulkResult(result, 'restored');
      },
      error: (err) => {
        this.bulkWorking.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to restore the selected items.', 'Dismiss', { duration: 5000 });
      }
    });
  }

  // Row-level restore runs through the same endpoint with a single id, so there is one
  // code path to reason about rather than two that can drift.
  restoreItem(item: MenuItem): void {
    this.bulkWorking.set(true);
    this.menuItemService.bulkRestore([item.id]).subscribe({
      next: () => {
        this.bulkWorking.set(false);
        this.applyFilters();
        this.snackBar.open(`"${item.name}" is back on the menu.`, 'Dismiss', { duration: 4000 });
      },
      error: (err) => {
        this.bulkWorking.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to restore this item.', 'Dismiss', { duration: 5000 });
      }
    });
  }

  bulkSetAvailability(isAvailable: boolean): void {
    const ids = this.selection.selected;
    if (ids.length === 0) {
      return;
    }

    this.bulkWorking.set(true);
    this.menuItemService.bulkSetAvailability(ids, isAvailable).subscribe({
      next: (result) => {
        this.bulkWorking.set(false);
        this.clearSelection();
        this.applyFilters();
        this.reportBulkResult(result, isAvailable ? 'made available' : 'hidden');
      },
      error: (err) => {
        this.bulkWorking.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to update the selected items.', 'Dismiss', { duration: 5000 });
      }
    });
  }

  // Says so when the server touched fewer rows than were selected - someone else having
  // already removed one is worth knowing about, not rounding away.
  private reportBulkResult(result: BulkActionResult, verb: string): void {
    const message =
      result.affected === result.requested
        ? `${result.affected} item${result.affected === 1 ? '' : 's'} ${verb}.`
        : `${result.affected} of ${result.requested} ${verb} - the rest were no longer on the menu.`;
    this.snackBar.open(message, 'Dismiss', { duration: 5000 });
  }

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
    this.loadSubCategories();
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
  onDeletedFilterChange(value: DeletedFilter): void {
    this.deletedFilter.set(value);
    // Selections do not survive the switch: the ids on screen are about to be replaced
    // by a different population, and a Restore aimed at a live row (or a Delete aimed at
    // an already-deleted one) is never what the admin meant.
    this.clearSelection();
    this.applyFilters();
  }

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
        hasAddons: addOns === 'all' ? undefined : addOns === 'has',
        deleted: this.deletedFilter()
      })
      .subscribe({
        next: (items) => {
          this.menuItems.set(items);
          this.showingDeleted.set(this.deletedFilter() !== 'Active');
          // Drop anything that is no longer on screen. Without this, ticking three rows
          // and then changing the category filter leaves "3 selected" showing above a
          // table that contains none of them - and Delete would reach rows the admin
          // can no longer see. Rows that survive the new filter stay ticked, so a plain
          // Refresh doesn't throw the selection away.
          const visible = new Set(items.map((i) => i.id));
          this.selection.deselect(...this.selection.selected.filter((id) => !visible.has(id)));
          this.syncSelectionCount();
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

  loadSubCategories(): void {
    this.subCategoryService.getAll().subscribe({
      next: (subCategories) => this.subCategories.set(subCategories),
      error: () => this.snackBar.open('Failed to load sub-categories.', 'Dismiss', { duration: 4000 })
    });
  }

  loadAddOns(): void {
    this.addOnService.getAll().subscribe({
      next: (addOns) => this.addOns.set(addOns),
      error: () => this.snackBar.open('Failed to load add-ons.', 'Dismiss', { duration: 4000 })
    });
  }

  downloadTemplate(): void {
    if (this.downloadingTemplate()) {
      return;
    }

    this.downloadingTemplate.set(true);
    this.menuItemService.downloadExcelTemplate().subscribe({
      next: (blob) => {
        this.downloadingTemplate.set(false);
        saveAs(blob, 'menu-items-template.xlsx');
      },
      error: () => {
        this.downloadingTemplate.set(false);
        this.snackBar.open('Failed to download the template.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  triggerFileInput(): void {
    this.fileInput?.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    // Reset immediately so re-selecting the same file (e.g. after fixing it and
    // re-uploading) still fires this handler - the native input only emits "change"
    // when its value actually differs from before.
    input.value = '';

    if (!file) {
      return;
    }

    this.uploading.set(true);
    this.menuItemService.bulkUpload(file).subscribe({
      next: (result) => {
        this.uploading.set(false);

        const summary =
          `Imported ${result.itemsCreated} new item${result.itemsCreated === 1 ? '' : 's'}, ` +
          `updated ${result.itemsUpdated}`;

        if (result.errors.length > 0) {
          this.snackBar.open(
            `${summary}, ${result.rowsSkipped} row${result.rowsSkipped === 1 ? '' : 's'} skipped: ${result.errors[0]}`,
            'Dismiss',
            { duration: 8000 }
          );
        } else {
          this.snackBar.open(`${summary}.`, 'Dismiss', { duration: 5000 });
        }

        // A new item can bring a brand-new category with it, so the category filter
        // dropdown needs refreshing too, not just the table.
        this.loadCategories();
        this.applyFilters();
      },
      error: (err) => {
        this.uploading.set(false);
        const message = err.error?.errors?.[0] ?? 'Failed to import the Excel file.';
        this.snackBar.open(message, 'Dismiss', { duration: 6000 });
      }
    });
  }

  openCategoryManagement(): void {
    const dialogRef = this.dialog.open(CategoryManagementDialogComponent, { width: '560px' });

    dialogRef.afterClosed().subscribe((mutated: boolean | undefined) => {
      if (mutated) {
        this.loadCategories();
        // Sub-categories are managed inline within this same dialog (renaming a
        // category, deleting one, etc. can all affect them), so refresh alongside.
        this.loadSubCategories();
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
      data: {
        mode: 'create',
        categories: this.categories(),
        subCategories: this.subCategories(),
        availableAddOns: this.addOns()
      }
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
        categories: this.categories(),
        subCategories: this.subCategories(),
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
        subCategoryId: menuItem.subCategoryId ?? null,
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
