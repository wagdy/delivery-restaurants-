export interface SubCategory {
  id: number;
  name: string;
  // Optional Arabic display name; null/blank falls back to `name` - see LocalNamePipe.
  nameAr?: string | null;
  displayOrder: number;
  categoryId: number;
}

export interface SubCategoryRequest {
  name: string;
  nameAr?: string | null;
  categoryId: number;
}
