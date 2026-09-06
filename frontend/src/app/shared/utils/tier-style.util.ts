// Tier names are now admin-defined free text (see Tier in core/models/tier.model.ts),
// not a fixed enum, so a template can no longer safely do `'tier-' + name.toLowerCase()`
// and assume the result is both a valid CSS class and a color someone actually designed
// for - a name like "Otantik Special" would produce "tier-otantik special" (invalid,
// contains a space) with no matching style rule at all.
//
// Case-insensitive keyword matching keeps the 4 original looks for anyone who names
// their tiers the conventional way (including the pre-existing Bronze/Silver/Gold/VIP
// names seeded by the migration that introduced dynamic tiers - see
// AddLoyaltyTiersAndConvertMembershipTierToString on the backend), while any other
// custom name still resolves to a real, deliberately neutral default class instead of
// silently rendering unstyled. Mirrors ApplePassBuilder.ResolveTierColors on the backend.
const TIER_KEYWORD_CLASSES: { keyword: string; className: string }[] = [
  { keyword: 'vip', className: 'tier-vip' },
  { keyword: 'gold', className: 'tier-gold' },
  { keyword: 'silver', className: 'tier-silver' },
  { keyword: 'bronze', className: 'tier-bronze' }
];

export const DEFAULT_TIER_CLASS = 'tier-default';

export function tierStyleClass(tierName: string | null | undefined): string {
  if (tierName) {
    const lower = tierName.toLowerCase();
    const match = TIER_KEYWORD_CLASSES.find((entry) => lower.includes(entry.keyword));
    if (match) {
      return match.className;
    }
  }

  return DEFAULT_TIER_CLASS;
}
