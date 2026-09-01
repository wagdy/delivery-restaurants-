import { MenuItem } from './menu-item.model';
import { AddOn } from './add-on.model';

export interface CartLine {
  menuItem: MenuItem;
  quantity: number;
  selectedAddOns: AddOn[];
}
