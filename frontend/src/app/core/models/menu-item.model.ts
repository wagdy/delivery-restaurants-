import { AddOn } from './add-on.model';

export interface MenuItem {
  id: number;
  name: string;
  description?: string | null;
  price: number;
  category: string;
  // Optional finer-grained grouping within category above (e.g. "Hot Drinks" inside
  // "Drinks") - a real foreign key, unlike category, which is free text.
  subCategoryId?: number | null;
  subCategoryName?: string | null;
  imageUrl?: string | null;
  isAvailable: boolean;
  addOns: AddOn[];
}

export interface MenuItemFilter {
  searchQuery?: string;
  categoryId?: number;
  isAvailable?: boolean;
  hasAddons?: boolean;
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
  description?: string | null;
  price: number;
  category: string;
  subCategoryId?: number | null;
  imageUrl?: string | null;
  isAvailable: boolean;
  addOnIds: number[];
}
