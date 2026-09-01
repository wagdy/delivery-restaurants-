export interface RestaurantSettings {
  restaurantName: string;
  logoUrl?: string | null;
  primaryColor: string;
  accentColor: string;
  address?: string | null;
  phone?: string | null;
  email?: string | null;
  footerAbout?: string | null;
}

export type UpdateRestaurantSettingsRequest = RestaurantSettings;
