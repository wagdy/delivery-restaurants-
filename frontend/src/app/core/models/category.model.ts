export interface Category {
  id: number;
  name: string;
  displayOrder: number;
  imageUrl: string | null;
}

export interface CategoryRequest {
  name: string;
  imageUrl?: string | null;
}
