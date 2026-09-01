import { Injectable, computed, signal } from '@angular/core';
import { CartLine } from '../models/cart.model';
import { MenuItem } from '../models/menu-item.model';
import { AddOn } from '../models/add-on.model';

const STORAGE_KEY = 'rd_cart';
export const DELIVERY_FEE = 4.99;

// Two lines can share the same base menu item with different add-on selections
// (e.g. one burger plain, one with extra cheese) — they must stay distinct lines,
// so cart lines are identified by this composite key rather than menuItem.id alone.
export function cartLineKey(menuItemId: number, addOnIds: number[]): string {
  return `${menuItemId}:${[...addOnIds].sort((a, b) => a - b).join(',')}`;
}

function lineUnitPrice(line: CartLine): number {
  return line.menuItem.price + line.selectedAddOns.reduce((sum, a) => sum + a.price, 0);
}

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly _lines = signal<CartLine[]>(this.restore());

  readonly lines = this._lines.asReadonly();
  readonly itemCount = computed(() => this._lines().reduce((sum, l) => sum + l.quantity, 0));
  readonly subtotal = computed(() =>
    this._lines().reduce((sum, l) => sum + lineUnitPrice(l) * l.quantity, 0)
  );
  readonly deliveryFee = computed(() => (this._lines().length > 0 ? DELIVERY_FEE : 0));
  readonly estimatedTotal = computed(() => this.subtotal() + this.deliveryFee());

  keyFor(line: CartLine): string {
    return cartLineKey(line.menuItem.id, line.selectedAddOns.map((a) => a.id));
  }

  lineUnitPrice(line: CartLine): number {
    return lineUnitPrice(line);
  }

  add(menuItem: MenuItem, selectedAddOns: AddOn[] = [], quantity = 1): void {
    const key = cartLineKey(
      menuItem.id,
      selectedAddOns.map((a) => a.id)
    );
    const lines = this._lines();
    const existing = lines.find((l) => this.keyFor(l) === key);

    if (existing) {
      this.setQuantity(key, existing.quantity + quantity);
      return;
    }

    this.persist([...lines, { menuItem, quantity, selectedAddOns }]);
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
      // Defensive default for carts persisted before add-ons existed.
      return parsed.map((l) => ({ ...l, selectedAddOns: l.selectedAddOns ?? [] }));
    } catch {
      return [];
    }
  }
}
