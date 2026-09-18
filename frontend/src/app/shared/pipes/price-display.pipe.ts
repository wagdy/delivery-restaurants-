import { Pipe, PipeTransform } from '@angular/core';
import { MenuItem, MenuItemVariant } from '../../core/models/menu-item.model';

// What to print where a price goes: either the formatted currency, or the admin's note
// for items priced on the day.
//
// A pipe rather than the same @if repeated at every call site, because the rule has to
// hold identically on the menu card, in the bottom sheet, and anywhere a price is shown
// later. Getting it wrong in one place means a customer sees "L.E 0.00" on a kilo of
// meat, which reads as free rather than as "we weigh it first".
//
// Pure, and takes everything it needs as arguments, so it re-runs whenever the item or
// the chosen variant changes without needing pure: false.
@Pipe({ name: 'priceDisplay', standalone: true })
export class PriceDisplayPipe implements PipeTransform {
  transform(item: MenuItem, variant: MenuItemVariant | null = null): string {
    // A chosen size always wins: variants carry real prices, so an item with a base of 0
    // and sized options is priced by the size, not by the note.
    const price = variant?.price ?? item.price;

    if (price > 0) {
      return `L.E ${price.toFixed(2)}`;
    }

    const note = item.priceNote?.trim();
    if (note) {
      return note;
    }

    // Nothing to show and no note to explain it. "L.E 0.00" would read as free, so this
    // falls back to asking rather than quoting - the honest answer for a legacy row
    // saved before the note field existed.
    return 'Price on request';
  }
}
