import {
  Component,
  ElementRef,
  HostListener,
  QueryList,
  ViewChildren,
  computed,
  inject,
  signal
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MenuItemService } from '../../core/services/menu-item.service';
import { CategoryService } from '../../core/services/category.service';
import { CartService } from '../../core/services/cart.service';
import { MenuItem } from '../../core/models/menu-item.model';
import { MenuItemDetailsDialogComponent } from './menu-item-details-dialog/menu-item-details-dialog.component';
import { CategoryFabComponent } from './category-fab/category-fab.component';
import { MenuSearchFilterComponent, MenuViewMode } from '../../shared/menu-search-filter/menu-search-filter.component';

@Component({
  selector: 'app-storefront',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    CategoryFabComponent,
    MenuSearchFilterComponent
  ],
  templateUrl: './storefront.component.html',
  styleUrl: './storefront.component.scss'
})
export class StorefrontComponent {
  private readonly menuItemService = inject(MenuItemService);
  private readonly categoryService = inject(CategoryService);
  private readonly dialog = inject(MatDialog);
  protected readonly cart = inject(CartService);

  // One element per rendered category <section> (see #categorySection in the template) —
  // used both to detect which section is currently in view while scrolling, and as the
  // scrollIntoView target when a category is picked from the FAB popup.
  @ViewChildren('categorySection') private categorySectionEls?: QueryList<ElementRef<HTMLElement>>;

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly menuItems = signal<MenuItem[]>([]);
  readonly searchTerm = signal('');
  readonly selectedCategory = signal<string | null>(null);
  readonly viewMode = signal<MenuViewMode>('grid');
  readonly activeScrollCategory = signal<string | null>(null);

  // Admin-configured display order (see CategoryManagementDialogComponent's drag-and-drop),
  // fetched separately from the menu items themselves since Category is its own entity.
  private readonly categoryDisplayOrder = signal<string[]>([]);

  readonly categories = computed(() => {
    const order = this.categoryDisplayOrder();
    const present = new Set(this.menuItems().map((m) => m.category));
    // Only categories that actually have menu items right now, in admin-configured
    // order. Any item category with no matching Category row (a data edge case, since
    // MenuItem.category is a free-text field, not a foreign key) is appended
    // alphabetically at the end rather than silently dropped from the filter chips.
    const ordered = order.filter((name) => present.has(name));
    const knownNames = new Set(order);
    const extras = Array.from(present)
      .filter((name) => !knownNames.has(name))
      .sort();
    return [...ordered, ...extras];
  });

  readonly filteredItems = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const category = this.selectedCategory();
    return this.menuItems().filter((item) => {
      const matchesCategory = !category || item.category === category;
      const matchesSearch = !term || item.name.toLowerCase().includes(term);
      return matchesCategory && matchesSearch;
    });
  });

  readonly itemsByCategory = computed(() => {
    const groups = new Map<string, MenuItem[]>();
    for (const item of this.filteredItems()) {
      const list = groups.get(item.category) ?? [];
      list.push(item);
      groups.set(item.category, list);
    }
    // Reuse categories() as the single source of ordering, so the section order here
    // always matches the filter chip order above.
    return this.categories()
      .filter((name) => groups.has(name))
      .map((name): [string, MenuItem[]] => [name, groups.get(name)!]);
  });

  // When a category chip filter is active, only that one section is rendered, so it's
  // trivially "the active category" — otherwise fall back to whichever section scrolling
  // has brought into view.
  readonly fabActiveCategory = computed(() => this.selectedCategory() ?? this.activeScrollCategory());

  constructor() {
    this.menuItemService.getAll(undefined, undefined, true).subscribe({
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
  }

  // A section counts as "active" once its top has scrolled up past this line — comfortably
  // below the hero/search row so the very first section isn't already "active" at scrollY 0.
  private static readonly ACTIVE_LINE_PX = 160;

  @HostListener('window:scroll')
  onWindowScroll(): void {
    const sections = this.categorySectionEls;
    if (!sections || sections.length === 0) {
      return;
    }

    let current: string | null = null;
    for (const { nativeElement } of sections) {
      if (nativeElement.getBoundingClientRect().top <= StorefrontComponent.ACTIVE_LINE_PX) {
        current = nativeElement.dataset['category'] ?? null;
      } else {
        break;
      }
    }

    this.activeScrollCategory.set(current ?? this.itemsByCategory()[0]?.[0] ?? null);
  }

  scrollToCategory(category: string): void {
    // The target section only exists in the DOM once any active chip filter is cleared —
    // clearing it first (rather than silently failing to scroll) is what makes the FAB
    // reliable regardless of what the user was doing when they opened it. A macrotask
    // (not queueMicrotask) is used deliberately: it's the reliable way to wait for
    // Angular's zone.js-driven re-render to actually flush to the DOM before querying it.
    if (this.selectedCategory() !== null) {
      this.selectedCategory.set(null);
      setTimeout(() => this.performScrollToCategory(category), 0);
    } else {
      this.performScrollToCategory(category);
    }
  }

  private performScrollToCategory(category: string): void {
    const target = this.categorySectionEls?.find(
      (el) => el.nativeElement.dataset['category'] === category
    );
    target?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
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

  selectCategory(category: string | null): void {
    this.selectedCategory.set(this.selectedCategory() === category ? null : category);
  }
}
