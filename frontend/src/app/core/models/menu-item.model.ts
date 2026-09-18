import { AddOn } from './add-on.model';

export interface MenuItem {
  id: number;
  name: string;
  // Optional Arabic display name; null/blank falls back to `name` - see LocalNamePipe.
  nameAr?: string | null;
  description?: string | null;
  price: number;
  category: string;
  // Optional finer-grained grouping within category above (e.g. "Hot Drinks" inside
  // "Drinks") - a real foreign key, unlike category, which is free text.
  subCategoryId?: number | null;
  subCategoryName?: string | null;
  imageUrl?: string | null;
  isAvailable: boolean;
  // True only on rows returned by a "show deleted" admin query; the storefront's copy
  // of this model always carries false.
  isDeleted?: boolean;
  // Set only when `price` is 0 - what to show in place of the currency for items priced
  // on the day (meat, fish sold by weight). See PriceNotePipe.
  priceNote?: string | null;
  // When true the item's price IS the sum of its selected add-ons, and the add-ons
  // section becomes a required choice rather than an optional one.
  isPriceBasedOnAddons?: boolean;
  // Empty for most items. When non-empty the customer MUST choose one, and `price`
  // above is a placeholder nobody pays - the server refuses a line that names no
  // variant for an item that has them.
  variants: MenuItemVariant[];
  addOns: AddOn[];
}

// One purchasable size/weight of an item. `price` is ABSOLUTE - it replaces the item's
// base price rather than adding to it. That is the whole difference between a variant
// and an add-on, and it is why the two render with different price formatting.
export interface MenuItemVariant {
  id: number;
  name: string;
  nameAr?: string | null;
  price: number;
  displayOrder: number;
  isAvailable: boolean;
}

export type DeletedFilter = 'Active' | 'Deleted' | 'All';

export interface MenuItemFilter {
  searchQuery?: string;
  categoryId?: number;
  isAvailable?: boolean;
  hasAddons?: boolean;
  // Matches the server's DeletedFilter. Omitted means 'active', so no existing caller
  // changes behaviour.
  deleted?: DeletedFilter;
}

export interface BulkMenuItemImportResult {
  rowsProcessed: number;
  itemsCreated: number;
  itemsUpdated: number;
  rowsSkipped: number;
  errors: string[];
}

export interface MenuItemRequest {
  name: string;
  nameAr?: string | null;
  description?: string | null;
  price: number;
  category: string;
  subCategoryId?: number | null;
  imageUrl?: string | null;
  isAvailable: boolean;
  addOnIds: number[];
}

// What a bulk admin action actually did. Requested and affected differ when a selected
// id no longer exists - deleted by someone else between the page loading and the button
// being pressed - and the admin is told rather than shown a count that includes rows
// nothing happened to.
export interface BulkActionResult {
  requested: number;
  affected: number;
}
