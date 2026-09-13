import { RestaurantSettings } from '../../core/models/restaurant-settings.model';

// "30-45 min" - a plain admin-set expectation (RestaurantSettings.EstimatedDelivery*
// Minutes), not a computed ETA. Collapses to a single number when an admin sets both
// ends the same, rather than showing a pointless "30-30 min".
export function estimatedDeliveryLabel(settings: RestaurantSettings): string {
  const { estimatedDeliveryMinMinutes: min, estimatedDeliveryMaxMinutes: max } = settings;
  return min === max ? `${min} min` : `${min}-${max} min`;
}
