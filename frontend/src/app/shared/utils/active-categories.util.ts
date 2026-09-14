import { Category } from '../../core/models/category.model';
import { MenuItem } from '../../core/models/menu-item.model';

// Every category name currently in active use by at least one menu item, in display
// order, plus any category name found on a menu item with no matching Category row at
// all (MenuItem.category is a free-text field, not a foreign key) appended alphabetically
// after the ordered ones. `categories` is expected pre-sorted by displayOrder - both
// callers already sort at fetch time.
//
// Shared between the Menu's own category rail/grid (storefront.component.ts) and the
// hamburger drawer's category list (app.component.ts, via CategoryService's
// activeCategoryNames) so a category with zero items - previously hidden from the Menu
// but still shown as a dead end in the drawer - can't drift out of sync between the two
// again.
export function deriveActiveCategoryNames(categories: Category[], menuItems: MenuItem[]): string[] {
  const order = categories.map((c) => c.name);
  const present = new Set(menuItems.map((m) => m.category));
  const ordered = order.filter((name) => present.has(name));
  const knownNames = new Set(order);
  const extras = Array.from(present)
    .filter((name) => !knownNames.has(name))
    .sort();
  return [...ordered, ...extras];
}
