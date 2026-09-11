/** Mirrors RestaurantDelivery.Core.DTOs.Loyalty.CampaignResponse. */
export interface Campaign {
  id: string;
  title: string;
  description: string;
  categoryName: string | null;
  targetPunches: number;
  isActive: boolean;
  startDate: string | null;
  endDate: string | null;
  createdAt: string;
}

export interface CreateCampaignRequest {
  title: string;
  description: string;
  categoryName: string | null;
  targetPunches: number;
  // Mandatory (unlike Campaign.endDate above, which stays nullable for campaigns created
  // before this field became required).
  endDate: string;
  // Fires a WhatsApp broadcast to every active customer announcing this new campaign -
  // see WhatsAppBroadcastBackgroundService. Opt-in per creation, never implied.
  notifyCustomersViaWhatsApp: boolean;
}

/** Mirrors RestaurantDelivery.Core.DTOs.Loyalty.CustomerCampaignProgressResponse. */
export interface CustomerCampaignProgress {
  campaignId: string;
  campaignTitle: string;
  campaignDescription: string;
  targetPunches: number;
  currentPunches: number;
  rewardsEarned: number;
  endDate: string | null;
}

export interface PunchRequest {
  customerId: string;
  campaignId: string;
  checkReference?: string | null;
}

export interface RedeemRewardRequest {
  customerId: string;
  campaignId: string;
  checkReference?: string | null;
}

export interface PunchResult {
  customerId: string;
  campaignId: string;
  campaignTitle: string;
  currentPunches: number;
  targetPunches: number;
  rewardsEarned: number;
  rewardEarnedThisPunch: boolean;
}

export interface RedeemRewardResult {
  customerId: string;
  campaignId: string;
  campaignTitle: string;
  rewardsEarned: number;
}

/**
 * Mirrors RestaurantDelivery.Core.DTOs.Loyalty.PunchUpdatedPayload - pushed over the
 * "PunchUpdated" SignalR event (see LoyaltyRealtimeService).
 */
export interface PunchUpdatedEvent {
  campaignId: string;
  campaignTitle: string;
  currentPunches: number;
  targetPunches: number;
  rewardsEarned: number;
  rewardEarnedThisPunch: boolean;
}
