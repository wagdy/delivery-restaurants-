import { Component, OnDestroy, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { CdkDragDrop, DragDropModule, moveItemInArray } from '@angular/cdk/drag-drop';
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
    DragDropModule,
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
export class CategoryManagementDialogComponent implements OnDestroy {
  private readonly categoryService = inject(CategoryService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly ref = inject(MatDialogRef<CategoryManagementDialogComponent>);

  readonly loading = signal(true);
  readonly categories = signal<Category[]>([]);
  readonly newCategoryName = signal('');
  readonly newCategoryImageFile = signal<File | null>(null);
  // An object URL for the thumbnail preview - always a blob: URL, never the category's
  // real (http) imageUrl, since a brand-new category has no existing image to show.
  readonly newCategoryImagePreview = signal<string | null>(null);
  readonly adding = signal(false);
  readonly editingId = signal<number | null>(null);
  readonly editingName = signal('');
  readonly editingImageFile = signal<File | null>(null);
  // Starts as the category's existing (http) imageUrl; replaced with a blob: object URL
  // once the admin picks a new file. revokeEditPreviewIfBlob() below only ever revokes
  // the latter - revoking a real http URL would be a no-op but is worth avoiding anyway.
  readonly editingImagePreview = signal<string | null>(null);
  readonly savingEdit = signal(false);
  readonly reordering = signal(false);
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

  onNewImageSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    (event.target as HTMLInputElement).value = ''; // lets picking the same file again re-fire 'change'
    if (!file) {
      return;
    }

    this.clearNewImage();
    this.newCategoryImageFile.set(file);
    this.newCategoryImagePreview.set(URL.createObjectURL(file));
  }

  private clearNewImage(): void {
    const preview = this.newCategoryImagePreview();
    if (preview) {
      URL.revokeObjectURL(preview);
    }
    this.newCategoryImageFile.set(null);
    this.newCategoryImagePreview.set(null);
  }

  addCategory(): void {
    const name = this.newCategoryName().trim();
    if (!name) {
      return;
    }

    const formData = new FormData();
    formData.set('name', name);
    const file = this.newCategoryImageFile();
    if (file) {
      formData.set('image', file);
    }

    this.adding.set(true);
    this.categoryService.create(formData).subscribe({
      next: () => {
        this.adding.set(false);
        this.newCategoryName.set('');
        this.clearNewImage();
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
    this.editingImageFile.set(null);
    this.editingImagePreview.set(category.imageUrl);
  }

  cancelEdit(): void {
    this.revokeEditPreviewIfBlob();
    this.editingId.set(null);
    this.editingName.set('');
    this.editingImageFile.set(null);
    this.editingImagePreview.set(null);
  }

  onEditImageSelected(event: Event): void {
    const file = (event.target as HTMLInputElement).files?.[0] ?? null;
    (event.target as HTMLInputElement).value = '';
    if (!file) {
      return;
    }

    this.revokeEditPreviewIfBlob();
    this.editingImageFile.set(file);
    this.editingImagePreview.set(URL.createObjectURL(file));
  }

  private revokeEditPreviewIfBlob(): void {
    const preview = this.editingImagePreview();
    if (preview?.startsWith('blob:')) {
      URL.revokeObjectURL(preview);
    }
  }

  saveEdit(category: Category): void {
    const name = this.editingName().trim();
    const imageFile = this.editingImageFile();
    if (!name) {
      this.cancelEdit();
      return;
    }

    if (name === category.name && !imageFile) {
      this.cancelEdit();
      return;
    }

    const formData = new FormData();
    formData.set('name', name);
    if (imageFile) {
      formData.set('image', imageFile);
    }

    this.savingEdit.set(true);
    this.categoryService.update(category.id, formData).subscribe({
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

  drop(event: CdkDragDrop<Category[]>): void {
    if (event.previousIndex === event.currentIndex) {
      return;
    }

    // Reorder optimistically so the drag feels instant, then persist. On failure,
    // reload from the server rather than trying to hand-unwind the local reorder.
    const reordered = [...this.categories()];
    moveItemInArray(reordered, event.previousIndex, event.currentIndex);
    this.categories.set(reordered);
    this.mutated = true;

    this.reordering.set(true);
    this.categoryService.reorder(reordered.map((c) => c.id)).subscribe({
      next: () => this.reordering.set(false),
      error: (err) => {
        this.reordering.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to save new order.', 'Dismiss', {
          duration: 4000
        });
        this.load();
      }
    });
  }

  close(): void {
    this.ref.close(this.mutated);
  }

  ngOnDestroy(): void {
    this.clearNewImage();
    this.revokeEditPreviewIfBlob();
  }
}
