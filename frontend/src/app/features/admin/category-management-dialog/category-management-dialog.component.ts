import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialog, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CategoryService } from '../../../core/services/category.service';
import { Category } from '../../../core/models/category.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-category-management-dialog',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './category-management-dialog.component.html',
  styleUrl: './category-management-dialog.component.scss'
})
export class CategoryManagementDialogComponent {
  private readonly categoryService = inject(CategoryService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly ref = inject(MatDialogRef<CategoryManagementDialogComponent>);

  readonly loading = signal(true);
  readonly categories = signal<Category[]>([]);
  readonly newCategoryName = signal('');
  readonly adding = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly editingName = signal('');
  readonly savingEdit = signal(false);
  private mutated = false;

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.categoryService.getAll().subscribe({
      next: (categories) => {
        this.categories.set(categories);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load categories.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  addCategory(): void {
    const name = this.newCategoryName().trim();
    if (!name) {
      return;
    }

    this.adding.set(true);
    this.categoryService.create({ name }).subscribe({
      next: () => {
        this.adding.set(false);
        this.newCategoryName.set('');
        this.mutated = true;
        this.load();
      },
      error: (err) => {
        this.adding.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to create category.', 'Dismiss', {
          duration: 4000
        });
      }
    });
  }

  startEdit(category: Category): void {
    this.editingId.set(category.id);
    this.editingName.set(category.name);
  }

  cancelEdit(): void {
    this.editingId.set(null);
    this.editingName.set('');
  }

  saveEdit(category: Category): void {
    const name = this.editingName().trim();
    if (!name || name === category.name) {
      this.cancelEdit();
      return;
    }

    this.savingEdit.set(true);
    this.categoryService.update(category.id, { name }).subscribe({
      next: () => {
        this.savingEdit.set(false);
        this.mutated = true;
        this.cancelEdit();
        this.load();
      },
      error: (err) => {
        this.savingEdit.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to rename category.', 'Dismiss', {
          duration: 4000
        });
      }
    });
  }

  delete(category: Category): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete category',
        message: `Delete "${category.name}"? This only works if no menu items use it.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.categoryService.delete(category.id).subscribe({
        next: () => {
          this.mutated = true;
          this.load();
        },
        error: (err) => {
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete category.', 'Dismiss', {
            duration: 6000
          });
        }
      });
    });
  }

  close(): void {
    this.ref.close(this.mutated);
  }
}
