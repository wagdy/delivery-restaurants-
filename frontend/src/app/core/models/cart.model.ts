import { MenuItem, MenuItemVariant } from './menu-item.model';
import { AddOn } from './add-on.model';

export interface CartLine {
  menuItem: MenuItem;
  quantity: number;
  selectedAddOns: AddOn[];

  // The chosen size/weight, or null for items that have no variants. Part of the line's
  // identity: two different sizes of the same item are two lines, not one with
  // quantity 2 - see cartLineKey.
  selectedVariant: MenuItemVariant | null;
}
