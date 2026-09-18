import { Injectable, computed, inject, signal } from '@angular/core';
import { CartLine } from '../models/cart.model';
import { MenuItem, MenuItemVariant } from '../models/menu-item.model';
import { AddOn } from '../models/add-on.model';
import { SettingsService } from './settings.service';

const STORAGE_KEY = 'rd_cart';

// Two lines can share the same base menu item with different add-on selections
// (e.g. one burger plain, one with extra cheese) — they must stay distinct lines,
// so cart lines are identified by this composite key rather than menuItem.id alone.
//
// The variant is part of that identity for the same reason and a sharper one: 500g and
// 1 Kilo of the same tray cost different amounts, so folding them into one line would
// charge one of the two prices for both.
export function cartLineKey(menuItemId: number, addOnIds: number[], variantId: number | null = null): string {
  return `${menuItemId}:${variantId ?? ''}:${[...addOnIds].sort((a, b) => a - b).join(',')}`;
}

// The single definition of what one unit of a cart line costs, mirrored server-side by
// OrderService.BuildOrderItemsAsync. The variant REPLACES the base price; add-ons ADD to
// whichever of the two applies.
function lineUnitPrice(line: CartLine): number {
  const base = line.selectedVariant?.price ?? line.menuItem.price;
  return base + line.selectedAddOns.reduce((sum, a) => sum + a.price, 0);
}

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly settingsService = inject(SettingsService);
  private readonly _lines = signal<CartLine[]>(this.restore());

  readonly lines = this._lines.asReadonly();
  readonly itemCount = computed(() => this._lines().reduce((sum, l) => sum + l.quantity, 0));
  readonly subtotal = computed(() =>
    this._lines().reduce((sum, l) => sum + lineUnitPrice(l) * l.quantity, 0)
  );
  // Admin-configurable (Site Settings -> Checkout & Taxes) rather than a hardcoded
  // constant - settings are already loaded app-wide before bootstrap (see
  // CheckoutComponent's own doc comment on the same APP_INITIALIZER), so reading the
  // signal directly here needs no separate load() call.
  readonly deliveryFee = computed(() =>
    this._lines().length > 0 ? this.settingsService.settings().baseDeliveryFee : 0
  );
  readonly estimatedTotal = computed(() => this.subtotal() + this.deliveryFee());

  keyFor(line: CartLine): string {
    return cartLineKey(
      line.menuItem.id,
      line.selectedAddOns.map((a) => a.id),
      line.selectedVariant?.id ?? null
    );
  }

  lineUnitPrice(line: CartLine): number {
    return lineUnitPrice(line);
  }

  add(
    menuItem: MenuItem,
    selectedAddOns: AddOn[] = [],
    quantity = 1,
    selectedVariant: MenuItemVariant | null = null
  ): void {
    const key = cartLineKey(
      menuItem.id,
      selectedAddOns.map((a) => a.id),
      selectedVariant?.id ?? null
    );
    const lines = this._lines();
    const existing = lines.find((l) => this.keyFor(l) === key);

    if (existing) {
      this.setQuantity(key, existing.quantity + quantity);
      return;
    }

    this.persist([...lines, { menuItem, quantity, selectedAddOns, selectedVariant }]);
  }

  increment(lineKey: string): void {
    const line = this._lines().find((l) => this.keyFor(l) === lineKey);
    if (line) {
      this.setQuantity(lineKey, line.quantity + 1);
    }
  }

  decrement(lineKey: string): void {
    const line = this._lines().find((l) => this.keyFor(l) === lineKey);
    if (line) {
      this.setQuantity(lineKey, line.quantity - 1);
    }
  }

  setQuantity(lineKey: string, quantity: number): void {
    if (quantity <= 0) {
      this.remove(lineKey);
      return;
    }
    this.persist(this._lines().map((l) => (this.keyFor(l) === lineKey ? { ...l, quantity } : l)));
  }

  remove(lineKey: string): void {
    this.persist(this._lines().filter((l) => this.keyFor(l) !== lineKey));
  }

  quantityForMenuItem(menuItemId: number): number {
    return this._lines()
      .filter((l) => l.menuItem.id === menuItemId)
      .reduce((sum, l) => sum + l.quantity, 0);
  }

  clear(): void {
    this.persist([]);
  }

  private persist(lines: CartLine[]): void {
    this._lines.set(lines);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(lines));
  }

  private restore(): CartLine[] {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) {
      return [];
    }
    try {
      const parsed = JSON.parse(raw) as CartLine[];
      // Defensive defaults for carts persisted before add-ons, and before variants,
      // existed. A returning customer's localStorage predates both: without the
      // menuItem.variants default, reopening such a line for edit reads .length off
      // undefined and the dialog throws instead of opening.
      return parsed.map((l) => ({
        ...l,
        selectedAddOns: l.selectedAddOns ?? [],
        selectedVariant: l.selectedVariant ?? null,
        menuItem: { ...l.menuItem, variants: l.menuItem?.variants ?? [] }
      }));
    } catch {
      return [];
    }
  }
}
