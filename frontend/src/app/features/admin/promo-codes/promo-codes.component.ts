import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { PromoCodeService } from '../../../core/services/promo-code.service';
import { CategoryService } from '../../../core/services/category.service';
import { MenuItemService } from '../../../core/services/menu-item.service';
import { PromoCode, PromoDiscountType } from '../../../core/models/promo-code.model';
import { Category } from '../../../core/models/category.model';
import { MenuItem } from '../../../core/models/menu-item.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

const DISCOUNT_TYPE_OPTIONS: { value: PromoDiscountType; label: string }[] = [
  { value: 'Percentage', label: 'Percentage off order' },
  { value: 'SpecificCategory', label: 'Specific category' },
  { value: 'SpecificItem', label: 'Specific item' },
  { value: 'DeliveryDiscount', label: 'Delivery discount' }
];

@Component({
  selector: 'app-promo-codes',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatToolbarModule
  ],
  templateUrl: './promo-codes.component.html',
  styleUrl: './promo-codes.component.scss'
})
export class PromoCodesComponent {
  private readonly promoCodeService = inject(PromoCodeService);
  private readonly categoryService = inject(CategoryService);
  private readonly menuItemService = inject(MenuItemService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly discountTypeOptions = DISCOUNT_TYPE_OPTIONS;

  readonly promoCodes = signal<PromoCode[]>([]);
  readonly loading = signal(true);
  readonly categories = signal<Category[]>([]);
  readonly menuItems = signal<MenuItem[]>([]);

  readonly saving = signal(false);
  readonly editingId = signal<number | null>(null);

  readonly codeText = signal('');
  readonly discountType = signal<PromoDiscountType>('Percentage');
  readonly discountValue = signal(10);
  readonly targetIds = signal<number[]>([]);
  readonly expiryDate = signal('');
  readonly isActive = signal(true);

  readonly needsCategoryTargets = computed(() => this.discountType() === 'SpecificCategory');
  readonly needsItemTargets = computed(() => this.discountType() === 'SpecificItem');

  readonly canSave = computed(() => {
    const code = this.codeText().trim();
    if (!code || !this.expiryDate() || this.discountValue() <= 0 || this.discountValue() > 100) {
      return false;
    }
    if ((this.needsCategoryTargets() || this.needsItemTargets()) && this.targetIds().length === 0) {
      return false;
    }
    return true;
  });

  constructor() {
    this.load();
    this.categoryService.getAll().subscribe({ next: (categories) => this.categories.set(categories) });
    this.menuItemService.getAll().subscribe({ next: (menuItems) => this.menuItems.set(menuItems) });
  }

  load(): void {
    this.loading.set(true);
    this.promoCodeService.getAll().subscribe({
      next: (promoCodes) => {
        this.promoCodes.set(promoCodes);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load promo codes.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  categoryName(id: number): string {
    return this.categories().find((c) => c.id === id)?.name ?? `#${id}`;
  }

  menuItemName(id: number): string {
    return this.menuItems().find((m) => m.id === id)?.name ?? `#${id}`;
  }

  discountTypeLabel(type: PromoDiscountType): string {
    return this.discountTypeOptions.find((o) => o.value === type)?.label ?? type;
  }

  discountSummary(promo: PromoCode): string {
    switch (promo.discountType) {
      case 'Percentage':
        return `${promo.discountValue}% off order`;
      case 'DeliveryDiscount':
        return `${promo.discountValue}% off delivery`;
      case 'SpecificCategory':
        return `${promo.discountValue}% off ${(promo.targetIds ?? []).map((id) => this.categoryName(id)).join(', ')}`;
      case 'SpecificItem':
        return `${promo.discountValue}% off ${(promo.targetIds ?? []).map((id) => this.menuItemName(id)).join(', ')}`;
      default:
        return '';
    }
  }

  resetForm(): void {
    this.editingId.set(null);
    this.codeText.set('');
    this.discountType.set('Percentage');
    this.discountValue.set(10);
    this.targetIds.set([]);
    this.expiryDate.set('');
    this.isActive.set(true);
  }

  editPromoCode(promo: PromoCode): void {
    this.editingId.set(promo.id);
    this.codeText.set(promo.codeText);
    this.discountType.set(promo.discountType);
    this.discountValue.set(promo.discountValue);
    this.targetIds.set(promo.targetIds ?? []);
    this.expiryDate.set(promo.expiryDate.slice(0, 10));
    this.isActive.set(promo.isActive);
  }

  savePromoCode(): void {
    if (!this.canSave()) {
      return;
    }

    const needsTargets = this.needsCategoryTargets() || this.needsItemTargets();
    const request = {
      codeText: this.codeText().trim(),
      discountType: this.discountType(),
      discountValue: this.discountValue(),
      targetIds: needsTargets ? this.targetIds() : null,
      expiryDate: this.expiryDate(),
      isActive: this.isActive()
    };

    this.saving.set(true);
    const editingId = this.editingId();
    const request$ = editingId !== null ? this.promoCodeService.update(editingId, request) : this.promoCodeService.create(request);

    request$.subscribe({
      next: () => {
        this.saving.set(false);
        this.snackBar.open(editingId !== null ? 'Promo code updated.' : 'Promo code created.', 'Dismiss', { duration: 3000 });
        this.resetForm();
        this.load();
      },
      error: (err) => {
        this.saving.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to save promo code.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  deletePromoCode(promo: PromoCode): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete promo code',
        message: `Delete "${promo.codeText}"? Customers will no longer be able to redeem it.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.promoCodeService.delete(promo.id).subscribe({
        next: () => {
          if (this.editingId() === promo.id) {
            this.resetForm();
          }
          this.load();
        },
        error: (err) => {
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete promo code.', 'Dismiss', { duration: 4000 });
        }
      });
    });
  }
}
