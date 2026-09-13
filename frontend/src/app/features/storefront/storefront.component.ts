import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule, NgOptimizedImage } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MenuItemService } from '../../core/services/menu-item.service';
import { CategoryService } from '../../core/services/category.service';
import { SubCategoryService } from '../../core/services/sub-category.service';
import { CartService, cartLineKey } from '../../core/services/cart.service';
import { AuthService } from '../../core/services/auth.service';
import { SettingsService } from '../../core/services/settings.service';
import { MenuItem } from '../../core/models/menu-item.model';
import { Category } from '../../core/models/category.model';
import { SubCategory } from '../../core/models/sub-category.model';
import { MenuItemDetailsDialogComponent } from './menu-item-details-dialog/menu-item-details-dialog.component';
import { MyOrdersComponent } from '../my-orders/my-orders.component';
import { LoyaltyRewardsComponent } from './loyalty-rewards/loyalty-rewards.component';
import { iconForCategory } from '../../shared/utils/category-icon.util';

export type MenuViewMode = 'list' | 'grid';
export type HeroTab = 'menu' | 'rewards' | 'orders';
// 'categories' is the visual landing grid (one card per category); 'items' is a single
// selected category's own item list - see selectCategory()/backToCategories() below.
export type MenuDrillView = 'categories' | 'items';

// One inline sub-grouping within a category section - a sub-category headline followed
// by just its own items. Never a clickable card, unlike the category card in the
// landing grid.
export interface SubCategorySection {
  subCategory: SubCategory;
  items: MenuItem[];
}

// One category's items, grouped - the unit the item view renders for whichever category
// is currently selected.
export interface CategorySection {
  category: string;
  ungroupedItems: MenuItem[];
  subSections: SubCategorySection[];
  totalCount: number;
}

// One card in the category landing grid.
export interface CategoryCard {
  name: string;
  imageUrl: string | null;
  itemCount: number;
}

@Component({
  selector: 'app-storefront',
  standalone: true,
  imports: [
    CommonModule,
    NgOptimizedImage,
    RouterLink,
    MatDialogModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MyOrdersComponent,
    LoyaltyRewardsComponent
  ],
  templateUrl: './storefront.component.html',
  // Two files rather than one - see storefront-category-grid.scss's own comment for why
  // (Angular's per-component style budget is checked per stylesheet file).
  styleUrls: ['./storefront.component.scss', './storefront-category-grid.scss']
})
export class StorefrontComponent {
  private readonly menuItemService = inject(MenuItemService);
  private readonly categoryService = inject(CategoryService);
  private readonly subCategoryService = inject(SubCategoryService);
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

  // The hero's tab bar - Menu/Rewards/My Orders.
  readonly activeTab = signal<HeroTab>('menu');

  // Drives the 2-step drill-down: a beautiful category grid first, then a single
  // category's own items once picked - see selectCategory()/backToCategories().
  readonly menuView = signal<MenuDrillView>('categories');
  readonly selectedCategory = signal<string | null>(null);

  // Admin-configured display order plus per-category image, fetched separately from the
  // menu items themselves since Category is its own entity (see
  // CategoryManagementDialogComponent's drag-and-drop for how this is edited).
  private readonly categoryDisplayOrder = signal<Category[]>([]);

  // All sub-categories across every category, fetched once like categoryDisplayOrder
  // above - each category section below narrows this down to just its own sub-categories,
  // in display order.
  private readonly subCategories = signal<SubCategory[]>([]);

  readonly categories = computed(() => {
    const order = this.categoryDisplayOrder().map((c) => c.name);
    const present = new Set(this.menuItems().map((m) => m.category));
    // Only categories that actually have menu items right now, in admin-configured
    // order. Any item category with no matching Category row (a data edge case, since
    // MenuItem.category is a free-text field, not a foreign key) is appended
    // alphabetically at the end rather than silently dropped from the grid/rail.
    const ordered = order.filter((name) => present.has(name));
    const knownNames = new Set(order);
    const extras = Array.from(present)
      .filter((name) => !knownNames.has(name))
      .sort();
    return [...ordered, ...extras];
  });

  // Raw (never search-filtered) per-category item counts - used for both the landing
  // grid's card counts and the item view's rail badges, which should stay stable
  // reference points rather than fluctuating while the user searches within one category.
  readonly categoryTotalCounts = computed(() => {
    const counts = new Map<string, number>();
    for (const item of this.menuItems()) {
      counts.set(item.category, (counts.get(item.category) ?? 0) + 1);
    }
    return counts;
  });

  // The landing grid's own cards, in admin-configured order.
  readonly categoryCards = computed<CategoryCard[]>(() => {
    const imageByName = new Map(this.categoryDisplayOrder().map((c) => [c.name, c.imageUrl]));
    const counts = this.categoryTotalCounts();
    return this.categories().map((name) => ({
      name,
      imageUrl: imageByName.get(name) ?? null,
      itemCount: counts.get(name) ?? 0
    }));
  });

  // The actual Largest Contentful Paint candidate for NgOptimizedImage's own "priority"
  // attribute is whichever card is the first to render a REAL <img> - not necessarily
  // card 0, since an early category with no uploaded photo renders a plain CSS gradient
  // instead (no <img> element at all, so it can never be the LCP element).
  readonly firstImageCardIndex = computed(() => this.categoryCards().findIndex((c) => c.imageUrl != null));

  // Every category's items, grouped and search-filtered - only ever consumed through
  // selectedSection() below, but computed for every category (not just the selected one)
  // so switching categories via the rail doesn't need to redo this grouping work.
  readonly menuSections = computed<CategorySection[]>(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const categoryIdByName = new Map(this.categoryDisplayOrder().map((c) => [c.name, c.id]));
    const subCategoriesByCategoryId = new Map<number, SubCategory[]>();
    for (const sc of this.subCategories()) {
      const bucket = subCategoriesByCategoryId.get(sc.categoryId);
      if (bucket) {
        bucket.push(sc);
      } else {
        subCategoriesByCategoryId.set(sc.categoryId, [sc]);
      }
    }

    const sections: CategorySection[] = [];
    for (const category of this.categories()) {
      const matches = this.menuItems().filter(
        (item) => item.category === category && (!term || item.name.toLowerCase().includes(term))
      );
      if (matches.length === 0) {
        continue;
      }

      const itemsBySubCategoryId = new Map<number, MenuItem[]>();
      const ungroupedItems: MenuItem[] = [];
      for (const item of matches) {
        if (item.subCategoryId == null) {
          ungroupedItems.push(item);
          continue;
        }
        const bucket = itemsBySubCategoryId.get(item.subCategoryId);
        if (bucket) {
          bucket.push(item);
        } else {
          itemsBySubCategoryId.set(item.subCategoryId, [item]);
        }
      }

      const categoryId = categoryIdByName.get(category);
      const subSections: SubCategorySection[] = (categoryId != null ? subCategoriesByCategoryId.get(categoryId) ?? [] : [])
        .slice()
        .sort((a, b) => a.displayOrder - b.displayOrder)
        .map((subCategory) => ({ subCategory, items: itemsBySubCategoryId.get(subCategory.id) ?? [] }))
        .filter((section) => section.items.length > 0);

      sections.push({ category, ungroupedItems, subSections, totalCount: matches.length });
    }

    return sections;
  });

  // The single section the item view actually renders - null while data is still
  // loading, the category has no items, or a search term matches nothing in it (the
  // template tells these apart via searchTerm() for the empty-state message).
  readonly selectedSection = computed<CategorySection | null>(() => {
    const category = this.selectedCategory();
    if (!category) {
      return null;
    }
    return this.menuSections().find((s) => s.category === category) ?? null;
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
        this.categoryDisplayOrder.set([...categories].sort((a, b) => a.displayOrder - b.displayOrder));
      }
    });

    // Best-effort: if this fails, every section just shows a flat grid with no
    // sub-headlines (subSections comes back empty), same as a category with none defined.
    this.subCategoryService.getAll().subscribe({
      next: (subCategories) => this.subCategories.set(subCategories)
    });

    // The hamburger drawer (app.component) links here with ?category=X - jumps straight
    // into that category's item view, switching back to the Menu tab first in case the
    // link arrived while Rewards or My Orders was showing. A live subscription (not just
    // route.snapshot) is needed since Angular reuses this component instance rather than
    // recreating it when only the query param changes while already sitting on this
    // route. No loading-order dance needed here (unlike the old continuous-scroll
    // version's pendingScrollCategory) - selectedSection() is a plain computed, so it
    // resolves correctly on its own once menuItems()/categoryDisplayOrder() finish
    // loading, whatever order this subscription fires relative to those.
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const category = params.get('category');
      if (category) {
        this.activeTab.set('menu');
        this.selectCategory(category);
      }

      // Lets the WhatsApp welcome message link straight to the Rewards tab
      // (?tab=rewards) - see WhatsAppNotificationService.SendWelcomeMessageAsync.
      if (params.get('tab') === 'rewards') {
        this.activeTab.set('rewards');
      }

      // Lets the header's "My Orders" quick link (app.component.html) jump straight
      // here from any page, same deep-link mechanism as ?tab=rewards above.
      if (params.get('tab') === 'orders') {
        this.activeTab.set('orders');
      }
    });
  }

  // Drills into one category's item view - clicking a landing-grid card, a rail chip
  // (to jump to a different category without going back to the grid), or a ?category=X
  // deep link all funnel through here. Clears any in-progress search so switching
  // categories never carries over a stale filter the user didn't intend for the new one.
  selectCategory(category: string): void {
    this.selectedCategory.set(category);
    this.menuView.set('items');
    this.searchTerm.set('');
  }

  backToCategories(): void {
    this.menuView.set('categories');
    this.selectedCategory.set(null);
    this.searchTerm.set('');
  }

  iconFor(category: string): string {
    return iconForCategory(category);
  }

  // ---------------------------------------------------------------------------
  // Item image loading reveal
  // ---------------------------------------------------------------------------

  // Tracks which item photos have actually finished downloading, so the template can show
  // a skeleton placeholder (instant, zero network cost) until each image's own (load)
  // event fires, then reveal it - keyed by item id, same pattern the category cards'
  // images use (see isCategoryImageLoaded below), just applied to menu items instead.
  private readonly loadedItemImages = signal<ReadonlySet<number>>(new Set());

  isItemImageLoaded(itemId: number): boolean {
    return this.loadedItemImages().has(itemId);
  }

  onItemImageLoad(itemId: number): void {
    this.loadedItemImages.update((current) => new Set(current).add(itemId));
  }

  // Same skeleton-reveal pattern as the menu items above, keyed by category name instead
  // of id since that's the only stable identifier categoryCards() carries.
  private readonly loadedCategoryImages = signal<ReadonlySet<string>>(new Set());

  isCategoryImageLoaded(name: string): boolean {
    return this.loadedCategoryImages().has(name);
  }

  onCategoryImageLoad(name: string): void {
    this.loadedCategoryImages.update((current) => new Set(current).add(name));
  }

  quantityFor(menuItemId: number): number {
    return this.cart.quantityForMenuItem(menuItemId);
  }

  openDetails(item: MenuItem): void {
    this.dialog.open(MenuItemDetailsDialogComponent, {
      width: '440px',
      // Panel-level backstops alongside the dialog's own internal max-height/width (see
      // menu-item-details-dialog.component.scss's .addon-modal) - without these, the
      // 440px/85vh targets alone would still overflow a viewport narrower/shorter than
      // that. panelClass hooks the mobile bottom-sheet positioning in styles.scss (the
      // CDK overlay pane it targets sits outside this component's own view, so that
      // override has to live globally rather than in this dialog's own stylesheet).
      maxWidth: '95vw',
      maxHeight: '90vh',
      panelClass: 'addon-dialog-panel',
      autoFocus: false,
      data: { menuItem: item }
    });
  }

  // Smart Add: an item with no add-ons has nothing left to configure, so the dialog
  // would just be an extra click to confirm "1x, no extras" - add it straight to the
  // cart instead. An item WITH add-ons still needs the dialog (Scenario A) since the
  // user has real choices to make there.
  smartAdd(item: MenuItem): void {
    if (item.addOns.length > 0) {
      this.openDetails(item);
      return;
    }
    this.cart.add(item, [], 1);
  }

  // The card's inline stepper only ever targets the plain (no add-ons) line - an item
  // with add-ons never shows the stepper (see showInlineStepper below) - so this fixed
  // key, with no add-on ids, is always the right line to adjust.
  incrementPlainLine(item: MenuItem): void {
    this.cart.increment(cartLineKey(item.id, []));
  }

  decrementPlainLine(item: MenuItem): void {
    this.cart.decrement(cartLineKey(item.id, []));
  }

  // Only a no-add-ons item still in the cart gets the inline [ - ] qty [ + ] control in
  // place of the Add button - an item with add-ons always shows Add (re-opening the
  // dialog lets the user add another, differently-configured line) even once it's
  // already in the cart.
  showInlineStepper(item: MenuItem): boolean {
    return item.addOns.length === 0 && this.quantityFor(item.id) > 0;
  }
}
