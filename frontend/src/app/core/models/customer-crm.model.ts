import { MembershipTier } from './loyalty.model';

/** Mirrors RestaurantDelivery.Core.DTOs.Customers.CustomerCrmResponse. */
export interface CustomerCrm {
  id: string;
  fullName: string;
  phoneNumber?: string | null;
  totalOrders: number;
  averageOrderValue: number;
  currentPoints: number;
  totalLifetimePoints: number;
  membershipTier: MembershipTier;
}
