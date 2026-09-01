import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MenuItemService } from '../../../core/services/menu-item.service';
import { MenuItem } from '../../../core/models/menu-item.model';
import { AddOn } from '../../../core/models/add-on.model';

export interface MenuItemFormDialogData {
  mode: 'create' | 'edit';
  menuItem?: MenuItem;
  categories: string[];
  availableAddOns: AddOn[];
}

const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif'];
const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;

@Component({
  selector: 'app-menu-item-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatSelectModule,
    MatIconModule,
    MatCheckboxModule
  ],
  templateUrl: './menu-item-form-dialog.component.html',
  styleUrl: './menu-item-form-dialog.component.scss'
})
export class MenuItemFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly menuItemService = inject(MenuItemService);
  private readonly ref = inject(MatDialogRef<MenuItemFormDialogComponent>);
  readonly data: MenuItemFormDialogData = inject(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly uploading = signal(false);
  readonly uploadError = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    name: [this.data.menuItem?.name ?? '', [Validators.required, Validators.maxLength(150)]],
    description: [this.data.menuItem?.description ?? ''],
    price: [this.data.menuItem?.price ?? 0, [Validators.required, Validators.min(0.01)]],
    category: [this.data.menuItem?.category ?? '', [Validators.required, Validators.maxLength(100)]],
    imageUrl: [this.data.menuItem?.imageUrl ?? ''],
    isAvailable: [this.data.menuItem?.isAvailable ?? true]
  });

  readonly selectedAddOnIds = signal<Set<number>>(
    new Set(this.data.menuItem?.addOns.map((a) => a.id) ?? [])
  );

  isAddOnSelected(addOnId: number): boolean {
    return this.selectedAddOnIds().has(addOnId);
  }

  toggleAddOn(addOnId: number): void {
    const next = new Set(this.selectedAddOnIds());
    if (next.has(addOnId)) {
      next.delete(addOnId);
    } else {
      next.add(addOnId);
    }
    this.selectedAddOnIds.set(next);
  }

  onPhotoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file) {
      return;
    }

    this.uploadError.set(null);

    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      this.uploadError.set('Only JPG, PNG, WEBP, and GIF images are allowed.');
      return;
    }

    if (file.size > MAX_IMAGE_SIZE_BYTES) {
      this.uploadError.set('Image must be 5 MB or smaller.');
      return;
    }

    this.uploading.set(true);
    this.menuItemService.uploadImage(file).subscribe({
      next: (res) => {
        this.uploading.set(false);
        this.form.controls.imageUrl.setValue(res.url);
      },
      error: (err) => {
        this.uploading.set(false);
        this.uploadError.set(err.error?.errors?.[0] ?? 'Failed to upload image.');
      }
    });
  }

  removePhoto(): void {
    this.form.controls.imageUrl.setValue('');
  }

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const request = {
      name: raw.name,
      description: raw.description || null,
      price: raw.price,
      category: raw.category,
      imageUrl: raw.imageUrl || null,
      isAvailable: raw.isAvailable,
      addOnIds: Array.from(this.selectedAddOnIds())
    };

    const request$ =
      this.data.mode === 'create'
        ? this.menuItemService.create(request)
        : this.menuItemService.update(this.data.menuItem!.id, request);

    request$.subscribe({
      next: (menuItem) => {
        this.saving.set(false);
        this.ref.close(menuItem);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to save menu item.');
      }
    });
  }

  cancel(): void {
    this.ref.close();
  }
}
