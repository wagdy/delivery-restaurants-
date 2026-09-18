import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { startWith } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormControl, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
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
import { MenuItem, MenuItemVariant } from '../../../core/models/menu-item.model';
import { AddOn } from '../../../core/models/add-on.model';
import { Category } from '../../../core/models/category.model';
import { SubCategory } from '../../../core/models/sub-category.model';

export interface MenuItemFormDialogData {
  mode: 'create' | 'edit';
  menuItem?: MenuItem;
  categories: Category[];
  subCategories: SubCategory[];
  availableAddOns: AddOn[];
}

// Spelled out rather than inferred: buildVariantGroup() below is referenced by the
// `variants` field's own initializer, and TypeScript cannot infer a type that depends on
// itself. Naming it also makes the array's element shape checkable at the call sites.
type VariantFormGroup = FormGroup<{
  id: FormControl<number | null>;
  name: FormControl<string>;
  nameAr: FormControl<string>;
  price: FormControl<number>;
  displayOrder: FormControl<number>;
  isAvailable: FormControl<boolean>;
}>;

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
    // Optional. Blank is normalised to null on submit below so the storefront falls
    // back to the English name rather than rendering an empty heading.
    nameAr: [this.data.menuItem?.nameAr ?? '', [Validators.maxLength(150)]],
    description: [this.data.menuItem?.description ?? ''],
    // min(0), not min(0.01): 0 is a legitimate saved state meaning "priced on the day",
    // explained by priceNote below. Variant prices keep their 0.01 floor - a size exists
    // precisely to carry a concrete price.
    price: [this.data.menuItem?.price ?? 0, [Validators.required, Validators.min(0)]],
    priceNote: [this.data.menuItem?.priceNote ?? '', [Validators.maxLength(100)]],
    category: [this.data.menuItem?.category ?? '', [Validators.required, Validators.maxLength(100)]],
    subCategoryId: [this.data.menuItem?.subCategoryId ?? null] as [number | null],
    imageUrl: [this.data.menuItem?.imageUrl ?? ''],
    isAvailable: [this.data.menuItem?.isAvailable ?? true]
  });

  // ---------------------------------------------------------------------------
  // Price note - shown only while the price is 0
  // ---------------------------------------------------------------------------

  // Drives the field's visibility in the template. Seeded from the loaded item so
  // editing an existing by-weight item opens with the note already showing.
  readonly showPriceNote = signal((this.data.menuItem?.price ?? 0) <= 0);

  // startWith so the rule is applied on open too, not only on the first edit - otherwise
  // reopening a saved by-weight item shows the field (the signal above is seeded) while
  // its required validator is still missing, and the note could be cleared and saved.
  private readonly priceNoteSync = this.form.controls.price.valueChanges
    .pipe(startWith(this.form.controls.price.value), takeUntilDestroyed())
    .subscribe((price) => this.applyPriceNoteState(price));

  // Both directions matter, which is why this is not just a visibility flag:
  //
  // Going to 0 makes the note REQUIRED. An item priced at 0 with no note renders as
  // nothing at all on the menu card - not "free", not a price, just a gap - so the form
  // refuses to save that rather than letting it reach the storefront.
  //
  // Going above 0 clears the value as well as hiding the field. Hiding alone would leave
  // the old text in the control, and it would be submitted and stored against an item
  // that now has a real price - the server drops it anyway (see ResolvePriceNote), but
  // the admin would still see a stale note reappear if they set the price back to 0.
  private applyPriceNoteState(price: number | null): void {
    const isByWeight = !price || price <= 0;
    this.showPriceNote.set(isByWeight);

    const control = this.form.controls.priceNote;
    if (isByWeight) {
      control.setValidators([Validators.required, Validators.maxLength(100)]);
    } else {
      control.clearValidators();
      control.setValue('', { emitEvent: false });
    }

    control.updateValueAndValidity({ emitEvent: false });
  }

  // ---------------------------------------------------------------------------
  // Variants (sizes/weights)
  // ---------------------------------------------------------------------------

  // A FormArray rather than a plain signal array: each row has its own required-name and
  // min-price validation, and the dialog's single form.invalid check then covers the
  // variant rows too instead of letting a blank size name through to the server.
  readonly variants: FormArray<VariantFormGroup> = this.fb.array(
    (this.data.menuItem?.variants ?? []).map((v) => this.buildVariantGroup(v))
  );

  // formArrayName needs a parent FormGroup to resolve against; the array is kept
  // separate from the main `form` so an invalid size row cannot be missed by a
  // form.invalid check that was written before variants existed (see submit()).
  readonly variantsForm = this.fb.group({ variants: this.variants });

  // Price on the main form becomes a fallback nobody is charged once variants exist, so
  // the template relabels it rather than leaving two prices on screen with no
  // explanation of which one wins.
  readonly hasVariants = signal((this.data.menuItem?.variants ?? []).length > 0);

  private buildVariantGroup(variant?: MenuItemVariant): VariantFormGroup {
    return this.fb.nonNullable.group({
      // Carried through so MenuItemService can match and update the existing row instead
      // of deleting it and inserting a new id - which would detach it from the order
      // history and from any cart holding it.
      id: [variant?.id ?? null] as [number | null],
      name: [variant?.name ?? '', [Validators.required, Validators.maxLength(100)]],
      nameAr: [variant?.nameAr ?? '', [Validators.maxLength(100)]],
      price: [variant?.price ?? 0, [Validators.required, Validators.min(0.01)]],
      displayOrder: [variant?.displayOrder ?? 0],
      isAvailable: [variant?.isAvailable ?? true]
    });
  }

  addVariant(): void {
    this.variants.push(this.buildVariantGroup());
    this.hasVariants.set(true);
  }

  removeVariant(index: number): void {
    this.variants.removeAt(index);
    this.hasVariants.set(this.variants.length > 0);
  }

  // Re-read on every change to the category control below, so switching category updates
  // the sub-category dropdown's options without a page reload.
  private readonly selectedCategoryName = signal(this.data.menuItem?.category ?? '');

  readonly availableSubCategories = computed(() => {
    const categoryId = this.data.categories.find((c) => c.name === this.selectedCategoryName())?.id;
    return categoryId === undefined ? [] : this.data.subCategories.filter((sc) => sc.categoryId === categoryId);
  });

  onCategoryChange(categoryName: string): void {
    this.selectedCategoryName.set(categoryName);

    // The previously selected sub-category may belong to a different category now -
    // clear it rather than silently submitting a mismatched pair (MenuItemService
    // rejects that combination server-side anyway).
    const stillValid = this.availableSubCategories().some(
      (sc) => sc.id === this.form.controls.subCategoryId.value
    );
    if (!stillValid) {
      this.form.controls.subCategoryId.setValue(null);
    }
  }

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
    // The variants array is a sibling of `form`, not a control inside it, so
    // form.invalid alone would happily submit a size with a blank name.
    if (this.form.invalid || this.variants.invalid) {
      this.form.markAllAsTouched();
      this.variants.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const request = {
      name: raw.name,
      nameAr: raw.nameAr.trim() || null,
      description: raw.description || null,
      price: raw.price,
      // Sent as null above zero so the stored shape matches what the server would
      // normalise it to anyway - one value for "no note", never an empty string.
      priceNote: raw.price > 0 ? null : raw.priceNote.trim() || null,
      category: raw.category,
      subCategoryId: raw.subCategoryId || null,
      imageUrl: raw.imageUrl || null,
      isAvailable: raw.isAvailable,
      addOnIds: Array.from(this.selectedAddOnIds()),
      // Sent as the complete desired set - the server removes anything already on the
      // item that is missing here, so deleting a row in this dialog deletes the variant.
      // DisplayOrder is taken from the row's position rather than a field the admin has
      // to keep in sync by hand.
      variants: this.variants.controls.map((group: VariantFormGroup, index: number) => {
        const value = group.getRawValue();
        return {
          id: value.id,
          name: value.name.trim(),
          nameAr: value.nameAr.trim() || null,
          price: value.price,
          displayOrder: index,
          isAvailable: value.isAvailable
        };
      })
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
