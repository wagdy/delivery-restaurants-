export interface RestaurantSettings {
  restaurantName: string;
  logoUrl?: string | null;
  primaryColor: string;
  accentColor: string;
  // Independent of primaryColor/accentColor above - these specifically paint the
  // storefront's top navbar and page background (see SettingsService.applyTheme).
  headerColor: string;
  bodyColor: string;
  // Overrides bodyColor as the page background when set.
  backgroundImageUrl?: string | null;
  // Prominent logo shown centered in the header; falls back to a solid headerColor
  // block when null (distinct from logoUrl, the small brand-mark by the name).
  centerLogoUrl?: string | null;
  address?: string | null;
  phone?: string | null;
  email?: string | null;
  footerAbout?: string | null;
}

export type UpdateRestaurantSettingsRequest = RestaurantSettings;
