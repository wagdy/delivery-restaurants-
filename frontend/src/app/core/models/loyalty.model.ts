import { CustomerCampaignProgress } from './campaign.model';

// Admin-defined free text now (see Tier/TierRequest in tier.model.ts) - was a fixed
// 'Bronze' | 'Silver' | 'Gold' | 'VIP' union before tiers became dynamically
// configurable. Kept as a named alias (rather than inlining `string` at every call site)
// so it's still self-documenting which string properties mean "a tier name".
export type MembershipTier = string;

/** Mirrors RestaurantDelivery.Core.DTOs.Loyalty.LoyaltyMeResponse. */
export interface LoyaltyMe {
  appUserId: string;
  currentPoints: number;
  totalLifetimePoints: number;
  membershipTier: MembershipTier;
  referralCode: string;
  appleWalletAvailable: boolean;
  googleWalletAvailable: boolean;
}

export interface EarnPointsRequest {
  customerId: string;
  checkAmount: number;
  checkReference?: string | null;
}

export interface RedeemPointsRequest {
  customerId: string;
  pointsToRedeem: number;
  checkReference?: string | null;
}

export interface LoyaltyTransactionResult {
  customerId: string;
  pointsTransacted: number;
  currentPoints: number;
  totalLifetimePoints: number;
  membershipTier: MembershipTier;
  tierUpgraded: boolean;
  discountAmount: number;
}

/** Mirrors RestaurantDelivery.Core.DTOs.Loyalty.ScannerCustomerResponse. */
export interface ScannerCustomer {
  appUserId: string;
  fullName: string;
  phoneNumber: string | null;
  currentPoints: number;
  totalLifetimePoints: number;
  membershipTier: MembershipTier;
  campaigns: CustomerCampaignProgress[];
}

/**
 * Mirrors RestaurantDelivery.Core.DTOs.Loyalty.PointsUpdatedPayload - pushed over the
 * "PointsUpdated" SignalR event (see LoyaltyRealtimeService).
 */
export interface PointsUpdatedEvent {
  currentPoints: number;
  totalLifetimePoints: number;
  membershipTier: MembershipTier;
  tierUpgraded: boolean;
}

/** Mirrors RestaurantDelivery.Core.DTOs.Loyalty.LoyaltySettingsResponse. */
export interface LoyaltySettings {
  pointsPerCurrencyUnit: number;
  redemptionValuePer100Points: number;
}

/** Mirrors RestaurantDelivery.Core.DTOs.Loyalty.UpdateLoyaltySettingsRequest. */
export interface UpdateLoyaltySettingsRequest {
  pointsPerCurrencyUnit: number;
  redemptionValuePer100Points: number;
}
