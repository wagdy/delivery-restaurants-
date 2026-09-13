// Keyword -> icon fallback for a category with no more specific match. Falls back to a
// generic plate icon below for any name that doesn't match - this is cosmetic only, never
// blocks a category from rendering. Shared between the Menu's category rail
// (storefront.component.ts) and the hamburger drawer's category list (app.component.ts).
const CATEGORY_ICONS: { keywords: string[]; icon: string }[] = [
  { keywords: ['drink', 'beverage', 'juice', 'soda'], icon: 'local_bar' },
  { keywords: ['dessert', 'sweet', 'cake'], icon: 'icecream' },
  { keywords: ['pizza'], icon: 'local_pizza' },
  { keywords: ['salad', 'vegetarian', 'vegan'], icon: 'eco' },
  { keywords: ['breakfast'], icon: 'free_breakfast' },
  { keywords: ['grill', 'bbq', 'meat', 'chicken'], icon: 'outdoor_grill' },
  { keywords: ['soup'], icon: 'soup_kitchen' },
  { keywords: ['pasta'], icon: 'ramen_dining' }
];
const DEFAULT_CATEGORY_ICON = 'restaurant_menu';

export function iconForCategory(name: string): string {
  const lower = name.toLowerCase();
  return CATEGORY_ICONS.find((entry) => entry.keywords.some((k) => lower.includes(k)))?.icon ?? DEFAULT_CATEGORY_ICON;
}
