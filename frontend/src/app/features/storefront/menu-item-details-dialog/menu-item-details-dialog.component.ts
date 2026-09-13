import { Component, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CartService } from '../../../core/services/cart.service';
import { MenuItem } from '../../../core/models/menu-item.model';
import { AddOn } from '../../../core/models/add-on.model';

export interface MenuItemDetailsDialogData {
  menuItem: MenuItem;
}

@Component({
  selector: 'app-menu-item-details-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule],
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

  // Replays the price "roll" animation on every change that moves lineTotal, including
  // rapid repeats (e.g. holding +) - see pulsePrice() below for why a plain signal.set
  // isn't enough on its own.
  readonly pricePulse = signal(false);
  private pricePulseResetTimer: ReturnType<typeof setTimeout> | null = null;

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
    this.pulsePrice();
  }

  incrementQuantity(): void {
    this.quantity.update((q) => q + 1);
    this.pulsePrice();
  }

  decrementQuantity(): void {
    this.quantity.update((q) => Math.max(1, q - 1));
    this.pulsePrice();
  }

  addToCart(): void {
    this.cart.add(this.data.menuItem, this.selectedAddOns(), this.quantity());
    this.ref.close();
  }

  close(): void {
    this.ref.close();
  }

  // Re-triggering a CSS animation by setting the same signal value twice in a row is a
  // no-op (Angular never removes/re-adds a class that's already applied) - a plain
  // set(true) would silently stop animating on a second rapid click (e.g. holding the +
  // button) before the first play's (animationend) had a chance to reset it. Forcing a
  // false -> (next tick) -> true round trip on every call, cancelling any previous
  // pending tick, makes each change restart the animation regardless of how fast they
  // arrive.
  private pulsePrice(): void {
    this.pricePulse.set(false);
    if (this.pricePulseResetTimer !== null) {
      clearTimeout(this.pricePulseResetTimer);
    }
    this.pricePulseResetTimer = setTimeout(() => this.pricePulse.set(true), 0);
  }
}
