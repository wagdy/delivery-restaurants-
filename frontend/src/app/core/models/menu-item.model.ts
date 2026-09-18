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
  addOns: AddOn[];
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
