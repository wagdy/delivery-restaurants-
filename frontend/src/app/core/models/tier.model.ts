export interface Tier {
  id: number;
  name: string;
  minPoints: number;
  // Null means "no ceiling" - the top tier.
  maxPoints: number | null;
}

export interface TierRequest {
  name: string;
  minPoints: number;
  maxPoints: number | null;
}
