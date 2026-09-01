import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { CartService } from '../../../core/services/cart.service';
import { MenuItem } from '../../../core/models/menu-item.model';
import { AddOn } from '../../../core/models/add-on.model';

export interface MenuItemDetailsDialogData {
  menuItem: MenuItem;
}

@Component({
  selector: 'app-menu-item-details-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, MatCheckboxModule],
  templateUrl: './menu-item-details-dialog.component.html',
  styleUrl: './menu-item-details-dialog.component.scss'
})
export class MenuItemDetailsDialogComponent {
  private readonly cart = inject(CartService);
  private readonly ref = inject(MatDialogRef<MenuItemDetailsDialogComponent>);
  readonly data: MenuItemDetailsDialogData = inject(MAT_DIALOG_DATA);

  readonly quantity = signal(1);
  readonly selectedAddOnIds = signal<Set<number>>(new Set());

  readonly selectedAddOns = computed(() =>
    this.data.menuItem.addOns.filter((a) => this.selectedAddOnIds().has(a.id))
  );

  readonly unitPrice = computed(
    () => this.data.menuItem.price + this.selectedAddOns().reduce((sum, a) => sum + a.price, 0)
  );

  readonly lineTotal = computed(() => this.unitPrice() * this.quantity());

  isSelected(addOn: AddOn): boolean {
    return this.selectedAddOnIds().has(addOn.id);
  }

  toggleAddOn(addOn: AddOn): void {
    const next = new Set(this.selectedAddOnIds());
    if (next.has(addOn.id)) {
      next.delete(addOn.id);
    } else {
      next.add(addOn.id);
    }
    this.selectedAddOnIds.set(next);
  }

  incrementQuantity(): void {
    this.quantity.update((q) => q + 1);
  }

  decrementQuantity(): void {
    this.quantity.update((q) => Math.max(1, q - 1));
  }

  addToCart(): void {
    this.cart.add(this.data.menuItem, this.selectedAddOns(), this.quantity());
    this.ref.close();
  }

  close(): void {
    this.ref.close();
  }
}
