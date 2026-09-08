export interface RestaurantSettings {
  restaurantName?: string | null;
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
  // Browser tab icon - distinct from logoUrl/centerLogoUrl (both shown in the storefront
  // header itself). Null falls back to the build-time default in index.html.
  faviconUrl?: string | null;
  // Browser tab text - distinct from restaurantName (shown in-app). Null falls back to
  // the same default the tab title already had before this feature existed.
  tabTitle?: string | null;

  // Applied to every order's subtotal (after any promo discount) at checkout.
  taxPercentage: number;
  isCashEnabled: boolean;
  isVisaEnabled: boolean;
  // The Fawry pay-by-link URL customers are redirected to when they choose Visa.
  visaFawryUrl?: string | null;
  isInstapayEnabled: boolean;
  // Free text (phone, email, or @username) shown to the customer at checkout.
  instapayAccount?: string | null;

  // The flat delivery fee applied to every order - see CartService.deliveryFee and
  // AdminDashboardComponent's Create Order tab, both of which read this instead of a
  // hardcoded constant now.
  baseDeliveryFee: number;

  // Up to 3 WhatsApp numbers the detailed "new order" alert is sent to on order creation -
  // deliberately separate from `phone` above, which is the public contact number shown to
  // customers in the footer. Each is independently optional; all three empty skips the
  // manager alert entirely (the customer's own confirmation still sends regardless) - see
  // WhatsAppNotificationService.SendOrderNotificationsAsync.
  managerWhatsApp1?: string | null;
  managerWhatsApp2?: string | null;
  managerWhatsApp3?: string | null;
}

export type UpdateRestaurantSettingsRequest = RestaurantSettings;
