export interface Category {
  id: number;
  name: string;
  // Optional Arabic display name; null/blank falls back to `name` - see LocalNamePipe.
  nameAr?: string | null;
  displayOrder: number;
  imageUrl: string | null;
}
