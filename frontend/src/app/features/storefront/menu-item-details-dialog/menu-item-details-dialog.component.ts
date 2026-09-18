import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { CartService } from '../../../core/services/cart.service';
import { MenuItem, MenuItemVariant } from '../../../core/models/menu-item.model';
import { AddOn } from '../../../core/models/add-on.model';
import { LocalNamePipe } from '../../../shared/pipes/local-name.pipe';
import { LanguageService } from '../../../core/services/language.service';

export interface MenuItemDetailsDialogData {
  menuItem: MenuItem;

  // Set only when reopening the dialog to CHANGE a line already in the cart (the cart
  // item's Edit button). The dialog seeds itself from these instead of starting empty,
  // and replaces that line on save rather than adding a second one beside it.
  editingLineKey?: string;
  initialAddOnIds?: number[];
  initialQuantity?: number;
  initialVariantId?: number | null;
}

@Component({
  selector: 'app-menu-item-details-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, LocalNamePipe],
  // OnPush: every piece of state this component renders is a signal, so Angular
  // can skip it entirely unless one of them actually changed. Without it, the item dialog
  // was re-checked on every unrelated async event anywhere in the app.
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './menu-item-details-dialog.component.html',
  styleUrl: './menu-item-details-dialog.component.scss'
})
export class MenuItemDetailsDialogComponent {
  private readonly cart = inject(CartService);
  protected readonly languageService = inject(LanguageService);
  private readonly ref = inject(MatDialogRef<MenuItemDetailsDialogComponent>);
  readonly data: MenuItemDetailsDialogData = inject(MAT_DIALOG_DATA);

  readonly quantity = signal(this.data.initialQuantity ?? 1);
  readonly selectedAddOnIds = signal<Set<number>>(new Set(this.data.initialAddOnIds ?? []));

  // Drives the button wording: "Add to cart" is wrong when the line is already there.
  readonly isEditing = !!this.data.editingLineKey;

  // ---------------------------------------------------------------------------
  // Variants (sizes/weights) - single choice, required when the item has any
  // ---------------------------------------------------------------------------

  // Only orderable sizes are offered. A sold-out size stays in the database so the order
  // history keeps its name, but putting it on screen would only let a customer pick
  // something the server is going to refuse.
  readonly availableVariants: MenuItemVariant[] = (this.data.menuItem.variants ?? [])
    .filter((v) => v.isAvailable)
    .sort((a, b) => a.displayOrder - b.displayOrder || a.price - b.price);

  // Two different questions, and conflating them was a bug: "does this item come in
  // sizes" decides whether the SERVER will demand a choice, while "are any of them in
  // stock" decides what this sheet can offer. An item whose every size is sold out
  // answers yes to the first and no to the second - rendering no size section there
  // would let the customer add it at the placeholder base price and only discover at
  // checkout that the order is refused.
  readonly definesVariants = (this.data.menuItem.variants ?? []).length > 0;

  readonly hasVariants = this.availableVariants.length > 0;

  readonly allVariantsSoldOut = this.definesVariants && this.availableVariants.length === 0;

  // Pre-selected so the footer button always shows a real, payable total rather than
  // opening on the base price - which for an item with variants is a placeholder nobody
  // is ever charged. Cheapest first because that is the honest anchor: the price the
  // customer saw on the menu card is the lowest one.
  //
  // When reopening to edit a cart line, the line's own size wins - but only if it is
  // still available, otherwise the customer is silently held to a size that has since
  // sold out.
  readonly selectedVariantId = signal<number | null>(this.resolveInitialVariantId());

  readonly selectedVariant = computed<MenuItemVariant | null>(() => {
    const id = this.selectedVariantId();
    return id === null ? null : this.availableVariants.find((v) => v.id === id) ?? null;
  });

  // ---------------------------------------------------------------------------
  // Price
  // ---------------------------------------------------------------------------

  readonly selectedAddOns = computed(() =>
    this.data.menuItem.addOns.filter((a) => this.selectedAddOnIds().has(a.id))
  );

  // Total = (chosen variant price OR base price when there are no variants)
  //         + sum of chosen add-ons.
  //
  // The variant REPLACES the base price rather than adding to it. That is why variant
  // prices render as absolute values and add-on prices render with a leading "+": the
  // formatting is telling the customer which of the two arithmetic rules applies.
  // CartService.lineUnitPrice and OrderService.BuildOrderItemsAsync compute the same
  // thing; the server's answer is the one that is charged.
  readonly basePrice = computed(() => this.selectedVariant()?.price ?? this.data.menuItem.price);

  readonly addOnsTotal = computed(() =>
    this.selectedAddOns().reduce((sum, a) => sum + a.price, 0)
  );

  readonly unitPrice = computed(() => this.basePrice() + this.addOnsTotal());

  readonly lineTotal = computed(() => this.unitPrice() * this.quantity());

  // An item with variants cannot be ordered without one. The footer button is disabled
  // rather than hidden, so the reason is visible instead of the control just missing.
  readonly canAddToCart = computed(() => !this.definesVariants || this.selectedVariant() !== null);

  private resolveInitialVariantId(): number | null {
    if (this.availableVariants.length === 0) {
      return null;
    }

    const requested = this.data.initialVariantId;
    if (requested != null && this.availableVariants.some((v) => v.id === requested)) {
      return requested;
    }

    // Already sorted cheapest-first above.
    return this.availableVariants[0].id;
  }

  selectVariant(variant: MenuItemVariant): void {
    if (this.selectedVariantId() === variant.id) {
      return;
    }

    this.selectedVariantId.set(variant.id);
    this.pulsePrice();
  }

  isVariantSelected(variant: MenuItemVariant): boolean {
    return this.selectedVariantId() === variant.id;
  }

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
    // Guard as well as the disabled button: a stray Enter on the focused element would
    // otherwise queue a line the server is guaranteed to reject at checkout.
    if (!this.canAddToCart()) {
      return;
    }

    // Editing replaces the original line rather than adding a second one. Removed first,
    // so that if the new add-on selection happens to match another line already in the
    // cart, add()'s own merge folds them together instead of leaving a duplicate.
    const editingKey = this.data.editingLineKey;
    if (editingKey) {
      this.cart.remove(editingKey);
    }

    this.cart.add(this.data.menuItem, this.selectedAddOns(), this.quantity(), this.selectedVariant());
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
