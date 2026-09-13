import { Component, effect, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SwUpdate, VersionReadyEvent } from '@angular/service-worker';
import { AuthService } from './core/services/auth.service';
import { CartService } from './core/services/cart.service';
import { SettingsService } from './core/services/settings.service';
import { CategoryService } from './core/services/category.service';
import { LoyaltyRealtimeService } from './core/services/loyalty-realtime.service';
import { Category } from './core/models/category.model';
import { CartDrawerComponent } from './features/storefront/cart-drawer/cart-drawer.component';
import { AppFooterComponent } from './shared/app-footer/app-footer.component';
import { iconForCategory } from './shared/utils/category-icon.util';

@Component({
  selector: 'app-root',
  imports: [
    RouterOutlet,
    RouterLink,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatBadgeModule,
    MatSidenavModule,
    MatListModule,
    CartDrawerComponent,
    AppFooterComponent
  ],
  templateUrl: './app.component.html',
  styleUrl: './app.component.scss'
})
export class AppComponent {
  protected readonly authService = inject(AuthService);
  protected readonly cart = inject(CartService);
  protected readonly settingsService = inject(SettingsService);
  private readonly categoryService = inject(CategoryService);
  private readonly router = inject(Router);
  private readonly swUpdate = inject(SwUpdate);
  private readonly snackBar = inject(MatSnackBar);

  // Never referenced again after this - injecting it here is what instantiates the
  // providedIn: 'root' singleton and starts its connect/disconnect effect() app-wide.
  // Angular's DI is lazy otherwise, so without an eager injection like this the service
  // would never actually run until something else happened to need it.
  private readonly loyaltyRealtimeService = inject(LoyaltyRealtimeService);

  // The hamburger's category drawer - best-effort, same "don't block the app on this"
  // reasoning as StorefrontComponent's own category fetch.
  readonly categories = signal<Category[]>([]);

  // Replays the category list's entrance stagger every time the drawer opens (rather
  // than once at app init) - see cart-drawer's own shake signals for why a round trip
  // through false is needed on a persistent, never-recreated component like this one.
  readonly categoryDrawerAnimate = signal(false);

  // Bounces the header cart badge whenever the item count actually changes. Driven by
  // an effect() rather than calling this explicitly wherever the count changes - the
  // count is mutated from many unrelated places (menu cards' Smart Add, the cart
  // drawer's own stepper, checkout), and this is the one place that owns the badge.
  readonly cartBadgeBump = signal(false);
  // Seeded from the cart's actual starting count (not e.g. -1) so the effect's own
  // first run - which always fires immediately on creation, before anything has really
  // "changed" - doesn't mistake the initial value for a change and bump on page load.
  private previousCartCount = this.cart.itemCount();
  private cartBadgeBumpTimer: ReturnType<typeof setTimeout> | null = null;

  // True for the fully public, chrome-free pages: "/rate/store", "/customer-review/:id"
  // (reached from a WhatsApp link by a customer who may not even be logged in - the
  // app-wide toolbar/sidenav would look completely out of place there), plus the
  // customer-facing auth screens "/login", "/register", "/forgot-password" (a premium
  // full-bleed split-screen design doesn't work under a busy toolbar still showing the
  // cart/hamburger/account icons for a session that isn't signed in yet - the new
  // AuthShellComponent supplies its own "back to Otantik" link in place of the toolbar).
  // "/email-login" deliberately stays out of this list - it's the separate, intentionally
  // lower-key staff/legacy flow this redesign doesn't touch. Initialized from the current
  // URL (not just NavigationEnd) so a hard-reload directly on one of these URLs starts
  // bare too, rather than flashing the full chrome for one frame before the first
  // navigation event.
  protected readonly isBarePage = signal(AppComponent.isBareUrl(this.router.url));

  constructor() {
    this.categoryService.getAll().subscribe({
      next: (categories) => {
        this.categories.set([...categories].sort((a, b) => a.displayOrder - b.displayOrder));
      }
    });

    this.router.events.pipe(filter((event) => event instanceof NavigationEnd)).subscribe((event) => {
      this.isBarePage.set(AppComponent.isBareUrl((event as NavigationEnd).urlAfterRedirects));
    });

    effect(() => {
      const count = this.cart.itemCount();
      if (count !== this.previousCartCount) {
        this.previousCartCount = count;
        this.pulseCartBadge();
      }
    });

    // The service worker (see app.config.ts's provideServiceWorker) caches the whole app
    // shell for offline use, which has a sharp edge: without this, a customer who leaves
    // a tab open across a deploy - or even just returns days later without a hard refresh
    // - keeps running whatever JS bundle was cached at their last visit, silently, forever
    // (a plain reload re-fetches index.html but the SW can still serve the OLD cached app
    // shell from before it notices the update). Every future fix ships to the server
    // immediately but not to an already-open tab until this fires - surfacing it here
    // rather than auto-reloading avoids yanking the page out from under someone mid-order.
    if (this.swUpdate.isEnabled) {
      this.swUpdate.versionUpdates
        .pipe(filter((event): event is VersionReadyEvent => event.type === 'VERSION_READY'))
        .subscribe(() => {
          const ref = this.snackBar.open('A new version of the app is available.', 'Refresh', { duration: 0 });
          ref.onAction().subscribe(() => {
            this.swUpdate.activateUpdate().then(() => document.location.reload());
          });
        });
    }
  }

  private static isBareUrl(url: string): boolean {
    // Strip the query string first - "/login?returnUrl=%2Fcheckout" (the common case,
    // arriving via an auth guard redirect) must still match the exact "/login" path below.
    const path = url.split('?')[0].split('#')[0];
    return (
      path.startsWith('/rate/') ||
      path.startsWith('/customer-review/') ||
      path === '/login' ||
      path === '/register' ||
      path === '/forgot-password'
    );
  }

  // Closing the drawer before navigating avoids it staying open over the storefront
  // while the page underneath scrolls to the chosen category.
  selectCategory(name: string, drawer: { close: () => void }): void {
    drawer.close();
    this.router.navigate(['/'], { queryParams: { category: name } });
  }

  // The single header User icon's target - replaces the old separate Login/Register
  // links for a guest. An authenticated (non-captain, non-admin-specific) user goes to
  // their own orders rather than back through the auth form; /my-orders is itself
  // authGuard-protected, so this is purely a shortcut, not the only thing enforcing it.
  protected accountRoute(): string {
    return this.authService.isAuthenticated() ? '/my-orders' : '/login';
  }

  protected iconFor(category: string): string {
    return iconForCategory(category);
  }

  // Same false -> (next tick) -> true round trip as the add-ons dialog's own price
  // pulse - a plain set(true) would silently no-op if two cart changes land close
  // enough together that the first bump's (animationend) hasn't reset the signal yet.
  private pulseCartBadge(): void {
    this.cartBadgeBump.set(false);
    if (this.cartBadgeBumpTimer !== null) {
      clearTimeout(this.cartBadgeBumpTimer);
    }
    this.cartBadgeBumpTimer = setTimeout(() => this.cartBadgeBump.set(true), 0);
  }
}
