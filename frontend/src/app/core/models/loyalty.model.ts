export type MembershipTier = 'Bronze' | 'Silver' | 'Gold' | 'VIP';

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
