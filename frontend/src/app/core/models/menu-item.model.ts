import { AddOn } from './add-on.model';

export interface MenuItem {
  id: number;
  name: string;
  description?: string | null;
  price: number;
  category: string;
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
  imageUrl?: string | null;
  isAvailable: boolean;
  addOnIds: number[];
}
