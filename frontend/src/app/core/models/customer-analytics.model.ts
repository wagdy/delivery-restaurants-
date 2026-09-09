// Replaces the old, separate CustomerCrm (from the CRM page) and CustomerInsight (from
// the Customers page) models - the merged Customer Insights dashboard shows both in one
// row instead of two admin pages with an overlapping-but-different slice of the same data.
export interface CustomerAnalytics {
  id: string;
  fullName: string;
  contactInfo: string;
  // Raw phone number (may be null for the rare account with only an email) - distinct
  // from contactInfo, which falls back to email for display. The Edit dialog prefills
  // from this field, never from contactInfo.
  phoneNumber: string | null;
  totalPoints: number;
  currentPoints: number;
  membershipTier: string;
  totalOrders: number;
  averageCheck: number;
  isPunchCardEnrolled: boolean;
  punchCardRedeemsCount: number;
  totalLifetimeValue: number;
  lastOrderDate: string | null;
}

// Computed client-side per row (see CustomerInsightsComponent.customerStatus) rather
// than stored - it's a live classification of the same fetched fields, not a fact about
// the customer that the backend needs to persist or query by.
export type CustomerStatus = 'Active' | 'AtRisk' | 'VIP';

export interface UpdateCustomerRequest {
  fullName: string;
  phoneNumber: string;
}
