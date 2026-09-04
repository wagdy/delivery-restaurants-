export interface SubCategory {
  id: number;
  name: string;
  displayOrder: number;
  categoryId: number;
}

export interface SubCategoryRequest {
  name: string;
  categoryId: number;
}
