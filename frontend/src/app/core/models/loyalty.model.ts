import { CustomerCampaignProgress } from './campaign.model';

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
