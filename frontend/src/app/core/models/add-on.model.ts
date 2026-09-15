export interface AddOn {
  id: number;
  name: string;
  // Optional Arabic display name; null/blank falls back to `name` - see LocalNamePipe.
  nameAr?: string | null;
  price: number;
}

export interface AddOnRequest {
  name: string;
  nameAr?: string | null;
  price: number;
}
