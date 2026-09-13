import { AfterViewInit, Component, DestroyRef, ElementRef, QueryList, ViewChildren, computed, inject, signal } from '@angular/core';
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

// One inline sub-grouping within a category section - a sub-category headline followed
// by just its own items. Never a clickable card, unlike the category headline above it.
export interface SubCategorySection {
  subCategory: SubCategory;
  items: MenuItem[];
}

// One continuous-page section per category (see menuSections() below) - the unit the
// sticky category rail scrolls/jumps to, replacing the old "drill into one category"
// view. Ungrouped items render first with no headline, then each sub-category gets its
// own headline, mirroring the category-level ungrouped/grouped split one level down.
export interface CategorySection {
  category: string;
  ungroupedItems: MenuItem[];
  subSections: SubCategorySection[];
  totalCount: number;
}

// Turns an arbitrary admin-entered category name into a safe, unique DOM id for the
// scroll-spy/anchor-jump nav below - category names are free text (see MenuItem.category
// on the model), not slugs, so this can't assume anything about their characters. The
// index suffix is what guarantees uniqueness (two categories could otherwise slugify to
// the same string, e.g. "Drinks!" and "Drinks?").
function categoryAnchorId(category: string, index: number): string {
  const slug = category
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return `menu-section-${index}-${slug || 'category'}`;
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
  styleUrl: './storefront.component.scss'
})
export class StorefrontComponent implements AfterViewInit {
  private readonly menuItemService = inject(MenuItemService);
  private readonly categoryService = inject(CategoryService);
  private readonly subCategoryService = inject(SubCategoryService);
  private readonly dialog = inject(MatDialog);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);
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

  // Which category chip is highlighted in the sticky rail - driven by whichever section
  // scrollToCategory() jumped to (immediate) or the IntersectionObserver in
  // ngAfterViewInit reports as nearest the top while the customer scrolls manually.
  readonly activeCategory = signal<string | null>(null);

  // Set by a ?category=X deep link (see the queryParamMap subscription below) before the
  // menu has necessarily finished loading - consumed by the effect-free check inside
  // menuSections()'s consumer in the template's constructor-time scroll, once that
  // category's section actually exists to scroll to.
  private pendingScrollCategory: string | null = null;

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
    // alphabetically at the end rather than silently dropped from the rail.
    const ordered = order.filter((name) => present.has(name));
    const knownNames = new Set(order);
    const extras = Array.from(present)
      .filter((name) => !knownNames.has(name))
      .sort();
    return [...ordered, ...extras];
  });

  // One continuous-page section per category with at least one match, in admin-configured
  // order - the replacement for the old two-view drill-down. Typing a search term narrows
  // every section at once and a category whose matches all disappear simply drops out of
  // the page entirely, same UX as the old per-category "no items match" empty state, just
  // applied per-section instead of gating a whole separate view.
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

  constructor() {
    this.menuItemService.getAll({ isAvailable: true }).subscribe({
      next: (items) => {
        this.menuItems.set(items);
        this.loading.set(false);
        if (!this.activeCategory()) {
          this.activeCategory.set(this.categories()[0] ?? null);
        }
        this.tryConsumePendingScroll();
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

    // The hamburger drawer (app.component) links here with ?category=X - scrolls straight
    // to that category's section, switching back to the Menu tab first in case the link
    // arrived while Rewards or My Orders was showing. A live subscription (not just
    // route.snapshot) is needed since Angular reuses this component instance rather than
    // recreating it when only the query param changes while already sitting on this route.
    this.route.queryParamMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const category = params.get('category');
      if (category) {
        this.activeTab.set('menu');
        this.pendingScrollCategory = category;
        this.tryConsumePendingScroll();
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

  // Only fires the actual scroll once that category's section exists in menuSections() -
  // harmless no-op otherwise (e.g. the deep link arrived before the menu finished
  // loading), retried from the menu-items subscription's next() above once it has.
  private tryConsumePendingScroll(): void {
    const category = this.pendingScrollCategory;
    if (!category || !this.menuSections().some((s) => s.category === category)) {
      return;
    }
    this.pendingScrollCategory = null;
    // Wait one tick so the section (and its id) has actually rendered before scrolling.
    setTimeout(() => this.scrollToCategory(category), 0);
  }

  // ---------------------------------------------------------------------------
  // Sticky rail: anchor ids, click-to-jump, and scroll-spy
  // ---------------------------------------------------------------------------

  @ViewChildren('categorySectionEl') private categorySectionEls!: QueryList<ElementRef<HTMLElement>>;
  private sectionObserver: IntersectionObserver | null = null;

  ngAfterViewInit(): void {
    this.categorySectionEls.changes.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => this.observeSections());
    this.observeSections();
    this.destroyRef.onDestroy(() => this.sectionObserver?.disconnect());
  }

  // Just below the tallest sticky header (toolbar + mobile rail) at any breakpoint - the
  // horizontal line a section's own headline has to cross to count as "current".
  private static readonly SCROLL_SPY_TRIGGER_Y = 150;

  private observeSections(): void {
    this.sectionObserver?.disconnect();
    // The observer here is only a cheap trigger for "something scrolled past a section
    // boundary, go recompute" - the rootMargin just narrows where a crossing fires down
    // to near SCROLL_SPY_TRIGGER_Y, so recomputes happen right when they matter instead
    // of only at the raw viewport edges. The actual "which section is active" decision
    // happens in recomputeActiveCategory() below, from every section's live position,
    // not from which entries this particular callback happened to report - an earlier
    // version picked from entries directly and had a dead zone (nothing "isIntersecting")
    // right at the top of the page, where the active chip would freeze on a stale value.
    this.sectionObserver = new IntersectionObserver(() => this.recomputeActiveCategory(), {
      rootMargin: `-${StorefrontComponent.SCROLL_SPY_TRIGGER_Y}px 0px -50% 0px`,
      threshold: 0
    });
    this.categorySectionEls.forEach((el) => this.sectionObserver!.observe(el.nativeElement));
    this.recomputeActiveCategory();
  }

  // The current section is whichever one's headline is the last to have crossed the
  // trigger line from below - i.e. the section the reader has scrolled into. Falls back
  // to the first section above the trigger line (page not yet scrolled) rather than
  // leaving the previous value in place, so there's never a position with no correct
  // answer.
  private recomputeActiveCategory(): void {
    // A click's own choice wins outright for the whole ride to its destination - see
    // scrollToCategory() below. Without this, a section boundary crossing the trigger
    // line mid-flight (unavoidable for a long menu, where the smooth scroll can pass
    // through several sections before settling) would recompute from wherever the
    // animation currently is and stomp the just-clicked chip's highlight before it
    // ever reaches the target.
    if (this.isProgrammaticScroll) {
      return;
    }

    // At the very bottom of the page, the last section's headline may never be able to
    // reach the trigger line - scrollIntoView's block:'start' can't push it any further
    // up once the document itself has run out of room to scroll (there's nothing below
    // it to make room). Without this, clicking - or manually scrolling to - the very
    // last category would leave the SECOND-TO-LAST one highlighted forever, since the
    // last one's top never actually crosses the line. Whatever's on screen at max
    // scroll is unambiguously the last section, regardless of exactly where its top sits.
    const atBottom = window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 2;
    if (atBottom && this.categorySectionEls.last) {
      this.activeCategory.set(this.categorySectionEls.last.nativeElement.getAttribute('data-category'));
      return;
    }

    let current: string | null = null;
    this.categorySectionEls.forEach((el) => {
      if (el.nativeElement.getBoundingClientRect().top <= StorefrontComponent.SCROLL_SPY_TRIGGER_Y) {
        current = el.nativeElement.getAttribute('data-category');
      }
    });
    this.activeCategory.set(current ?? this.categorySectionEls.first?.nativeElement.getAttribute('data-category') ?? null);
  }

  anchorId(category: string, index: number): string {
    return categoryAnchorId(category, index);
  }

  // Set while a scrollToCategory()-triggered smooth scroll is in flight - see
  // recomputeActiveCategory() above. scrollGuardToken lets a NEWER click's own release
  // win if an OLDER click's scrollend/timeout fires after it (rapid re-clicking a
  // different chip before the first scroll settles) - only the still-latest click may
  // lift the guard.
  private isProgrammaticScroll = false;
  private scrollGuardToken = 0;

  scrollToCategory(category: string): void {
    this.activeCategory.set(category);
    const index = this.menuSections().findIndex((s) => s.category === category);
    if (index === -1) {
      return;
    }
    const target = document.getElementById(this.anchorId(category, index));
    if (!target) {
      return;
    }

    const token = ++this.scrollGuardToken;
    this.isProgrammaticScroll = true;
    const release = () => {
      if (this.scrollGuardToken !== token) {
        return;
      }
      this.isProgrammaticScroll = false;
      // Reconcile against wherever the scroll actually settled - the fallback timeout
      // below can fire slightly before a very long scroll (a menu with hundreds of
      // items) truly finishes, and this is a harmless no-op if it's already correct.
      this.recomputeActiveCategory();
    };
    window.addEventListener('scrollend', release, { once: true });
    // Fallback for browsers without scrollend, and for the "already at the target, so
    // scrollIntoView causes no motion and scrollend never fires" case - generous enough
    // to outlast even a full-length jump on a very long menu.
    setTimeout(release, 2500);

    target.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  iconFor(category: string): string {
    return iconForCategory(category);
  }

  // ---------------------------------------------------------------------------
  // Item image loading reveal
  // ---------------------------------------------------------------------------

  // Tracks which item photos have actually finished downloading, so the template can show
  // a skeleton placeholder (instant, zero network cost) until each image's own (load)
  // event fires, then reveal it - keyed by item id, same pattern the old category-card
  // images used (see git history), just generalized to every menu item's own photo now
  // that those are the primary visual content of the page.
  private readonly loadedItemImages = signal<ReadonlySet<number>>(new Set());

  isItemImageLoaded(itemId: number): boolean {
    return this.loadedItemImages().has(itemId);
  }

  onItemImageLoad(itemId: number): void {
    this.loadedItemImages.update((current) => new Set(current).add(itemId));
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
