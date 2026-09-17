import { ChangeDetectionStrategy, Component, computed, inject, input } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { MatIconModule } from '@angular/material/icon';
import { CartLine } from '../../core/models/cart.model';
import { CartService } from '../../core/services/cart.service';
import { LanguageService } from '../../core/services/language.service';
import { LocalNamePipe } from '../pipes/local-name.pipe';
import {
  MenuItemDetailsDialogComponent,
  MenuItemDetailsDialogData
} from '../../features/storefront/menu-item-details-dialog/menu-item-details-dialog.component';

// One cart line: the dish, whatever add-ons were chosen with it, its thumbnail and the
// quantity control. Presentation only - every mutation goes through CartService, which
// owns the line list and its localStorage persistence.
//
// On the model: CartLine is ALREADY the nested shape this needs - one menuItem with a
// selectedAddOns array - so nothing here introduces a new one. See cart.model.ts.
@Component({
  selector: 'app-cart-item',
  standalone: true,
  imports: [MatIconModule, LocalNamePipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cart-item.component.html',
  styleUrl: './cart-item.component.scss'
})
export class CartItemComponent {
  readonly line = input.required<CartLine>();

  // The cart drawer wants one-tap deletion at ANY quantity, which the pill alone cannot
  // give: its trash only appears at one. Opt-in, because the checkout review step is
  // happy with the pill.
  readonly allowRemove = input(false);

  private readonly cart = inject(CartService);
  private readonly dialog = inject(MatDialog);
  protected readonly languageService = inject(LanguageService);

  // CartService identifies a line by menuItem + its sorted add-on ids, not by index -
  // two lines can share a dish and differ only by add-ons, so the key is what every
  // mutation below addresses.
  protected readonly lineKey = computed(() => this.cart.keyFor(this.line()));

  // "Can, Full Cream, Signature" - the add-on subtitle under the dish name. Localised
  // rather than raw: an Arabic customer reading an Arabic dish name should not get its
  // add-ons back in English, and LocalNamePipe already owns that fallback rule.
  protected readonly addOnSummary = computed(() => {
    const language = this.languageService.language();
    return this.line()
      .selectedAddOns.map((addOn) => (language === 'ar' && addOn.nameAr?.trim() ? addOn.nameAr.trim() : addOn.name))
      .join(', ');
  });

  // Dish plus add-ons, times quantity - what this line actually costs, which is the
  // number the customer is checking against their total.
  protected readonly lineTotal = computed(() => this.cart.lineUnitPrice(this.line()) * this.line().quantity);

  // Only worth showing when it differs from the line total - at quantity one the two are
  // the same number printed twice.
  protected readonly unitPrice = computed(() => this.cart.lineUnitPrice(this.line()));
  protected readonly showUnitPrice = computed(() => this.line().quantity > 1);

  // At one, decrementing would empty the line, so the control says so: a trash icon
  // rather than a minus that silently removes the item.
  protected readonly removesOnDecrement = computed(() => this.line().quantity <= 1);

  // Only offered when there is something to choose - a dish with no add-ons has nothing
  // to edit, and a pencil that opens a dialog with one button would be a dead end.
  // Only above one. At a quantity of one the pill's own trash already deletes the line,
  // and showing both put two different controls for the same destructive action on one
  // row - which is the duplication allowRemove exists to avoid, not to create.
  protected readonly showRemove = computed(() => this.allowRemove() && this.line().quantity > 1);

  protected readonly canEdit = computed(() => this.line().menuItem.addOns.length > 0);

  protected increment(): void {
    this.cart.increment(this.lineKey());
  }

  // decrement() already removes the line when it would hit zero (see CartService), so
  // this is one call either way - the icon changes, the behaviour does not need to.
  protected decrement(): void {
    this.cart.decrement(this.lineKey());
  }

  protected removeLine(): void {
    this.cart.remove(this.lineKey());
  }

  protected edit(): void {
    const line = this.line();
    const data: MenuItemDetailsDialogData = {
      menuItem: line.menuItem,
      // Reopens the dish with its current choices already ticked and replaces this line
      // on save, instead of adding a second one beside it.
      editingLineKey: this.lineKey(),
      initialAddOnIds: line.selectedAddOns.map((addOn) => addOn.id),
      initialQuantity: line.quantity
    };

    // Same config the storefront opens this dialog with (see StorefrontComponent.
    // openDetails) - the panelClass in particular hooks the mobile bottom-sheet
    // positioning that lives in global styles, so opening it any other way here would
    // give the same dialog a different shape depending on where it was launched from.
    this.dialog.open(MenuItemDetailsDialogComponent, {
      width: '440px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      panelClass: 'addon-dialog-panel',
      autoFocus: false,
      data
    });
  }
}
