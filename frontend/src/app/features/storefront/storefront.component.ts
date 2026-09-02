import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MenuItemService } from '../../core/services/menu-item.service';
import { CategoryService } from '../../core/services/category.service';
import { CartService } from '../../core/services/cart.service';
import { AuthService } from '../../core/services/auth.service';
import { SettingsService } from '../../core/services/settings.service';
import { MenuItem } from '../../core/models/menu-item.model';
import { MenuItemDetailsDialogComponent } from './menu-item-details-dialog/menu-item-details-dialog.component';

export type MenuViewMode = 'list' | 'grid';

// Keyword -> icon for a bit of visual personality on the category cards (View 1) since
// Category has no image field to show instead. Falls back to a generic icon below for
// any name that doesn't match - this is cosmetic only, never blocks a category from
// rendering.
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

function iconForCategory(name: string): string {
  const lower = name.toLowerCase();
  return CATEGORY_ICONS.find((entry) => entry.keywords.some((k) => lower.includes(k)))?.icon ?? DEFAULT_CATEGORY_ICON;
}

@Component({
  selector: 'app-storefront',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './storefront.component.html',
  styleUrl: './storefront.component.scss'
})
export class StorefrontComponent {
  private readonly menuItemService = inject(MenuItemService);
  private readonly categoryService = inject(CategoryService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  protected readonly cart = inject(CartService);
  protected readonly authService = inject(AuthService);
  protected readonly settingsService = inject(SettingsService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly menuItems = signal<MenuItem[]>([]);
  readonly searchTerm = signal('');
  readonly viewMode = signal<MenuViewMode>('grid');

  // Two-step navigation: null shows the top-level categories grid (View 1); a category
  // name drills into just that category's items (View 2). There is no continuous
  // multi-category scroll anymore, so unlike before, exactly one of the two views is
  // ever on screen at a time.
  readonly selectedCategory = signal<string | null>(null);

  // Admin-configured display order (see CategoryManagementDialogComponent's drag-and-drop),
  // fetched separately from the menu items themselves since Category is its own entity.
  private readonly categoryDisplayOrder = signal<string[]>([]);

  readonly categories = computed(() => {
    const order = this.categoryDisplayOrder();
    const present = new Set(this.menuItems().map((m) => m.category));
    // Only categories that actually have menu items right now, in admin-configured
    // order. Any item category with no matching Category row (a data edge case, since
    // MenuItem.category is a free-text field, not a foreign key) is appended
    // alphabetically at the end rather than silently dropped from the grid.
    const ordered = order.filter((name) => present.has(name));
    const knownNames = new Set(order);
    const extras = Array.from(present)
      .filter((name) => !knownNames.has(name))
      .sort();
    return [...ordered, ...extras];
  });

  // Item count shown on each category card in View 1.
  readonly categoryCounts = computed(() => {
    const counts = new Map<string, number>();
    for (const item of this.menuItems()) {
      counts.set(item.category, (counts.get(item.category) ?? 0) + 1);
    }
    return counts;
  });

  // Only meaningful in View 2 (selectedCategory is always set there) - filtered by both
  // the drilled-into category and, optionally, a search term typed within that view.
  readonly filteredItems = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const category = this.selectedCategory();
    return this.menuItems().filter((item) => {
      const matchesCategory = !category || item.category === category;
      const matchesSearch = !term || item.name.toLowerCase().includes(term);
      return matchesCategory && matchesSearch;
    });
  });

  constructor() {
    this.menuItemService.getAll({ isAvailable: true }).subscribe({
      next: (items) => {
        this.menuItems.set(items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load the menu. Please try again later.');
      }
    });

    // Best-effort: if this fails, categories() falls back to alphabetical via its
    // "extras" branch rather than the whole page erroring out.
    this.categoryService.getAll().subscribe({
      next: (categories) => {
        this.categoryDisplayOrder.set(
          [...categories].sort((a, b) => a.displayOrder - b.displayOrder).map((c) => c.name)
        );
      }
    });

    // The hamburger drawer (app.component) links here with ?category=X - drill straight
    // into that category's View 2. A live subscription (not just route.snapshot) is
    // needed since Angular reuses this component instance rather than recreating it
    // when only the query param changes while already sitting on this route.
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const category = params.get('category');
      if (category) {
        this.selectedCategory.set(category);
      }
    });
  }

  iconFor(category: string): string {
    return iconForCategory(category);
  }

  quantityFor(menuItemId: number): number {
    return this.cart.quantityForMenuItem(menuItemId);
  }

  openDetails(item: MenuItem): void {
    this.dialog.open(MenuItemDetailsDialogComponent, {
      width: '480px',
      data: { menuItem: item }
    });
  }

  openCategory(category: string): void {
    this.selectedCategory.set(category);
  }

  backToCategories(): void {
    this.selectedCategory.set(null);
    this.searchTerm.set('');
  }
}
