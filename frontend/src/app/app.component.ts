import { Component, ViewChild, computed, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
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

  // The cart bottom sheet - a plain signal + custom fixed-position container instead of
  // <mat-sidenav> (see app.component.html), since a side-drawer/dialog's own animation
  // system can't give the true bottom-sheet slide-up this pattern calls for. Content
  // component (CartDrawerComponent) is unchanged either way - see DrawerHandle, the same
  // minimal `{ close(): void }` contract it already used with the old sidenav.
  readonly cartOpen = signal(false);
  protected readonly cartSheetHandle = { close: () => this.closeCartSheet() };
  @ViewChild('cartDrawerContent') private cartDrawerContentRef?: CartDrawerComponent;
  private cartSheetResetTimer: ReturnType<typeof setTimeout> | null = null;

  // Drives both the floating "Review Order" bar's visibility and .page-content's own
  // extra bottom padding (see app.component.scss) - hidden once the sheet itself is open
  // so the bar never shows stacked behind/under it.
  readonly showCartFab = computed(() => !this.authService.isCaptain() && this.cart.itemCount() > 0 && !this.cartOpen());

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
          // verticalPosition: 'top' - the cart's own floating "Review Order" bar (see
          // cartOpen/showCartFab above) now permanently occupies the bottom of the
          // screen whenever there's anything in the cart, which is prime real estate for
          // Material's own default bottom-center snackbar position. Moving this one
          // notification to the top sidesteps the collision entirely, rather than
          // trying to carefully coordinate z-index/offsets between two independent
          // bottom-anchored elements that would otherwise fight for the same space.
          const ref = this.snackBar.open('A new version of the app is available.', 'Refresh', {
            duration: 0,
            verticalPosition: 'top'
          });
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

  // The drawer's own "Menu" header, doubling as a Home button - navigates to the main
  // storefront (or the captain's own deliveries screen for a captain, mirroring the
  // header brand logo's own routing) and resets StorefrontComponent back to its
  // default category-landing view via its ?tab=menu handling.
  //
  // The two-step navigate (clear query params, then set tab=menu) isn't decorative -
  // Angular's Router simply doesn't re-emit ActivatedRoute.queryParamMap for a
  // navigation whose resulting params are identical to the current ones (confirmed:
  // even passing onSameUrlNavigation: 'reload' doesn't help - that only forces the
  // navigation pipeline itself, guards/resolvers, to rerun, not the param observable to
  // emit a "changed" value when nothing about the params actually differs). Clicking
  // this a second time - or any time the URL already happens to be exactly
  // "/?tab=menu" from an earlier click, even if the user has since switched to
  // Rewards/My Orders entirely client-side with no URL change - would otherwise
  // silently do nothing. Clearing first guarantees each step's target state genuinely
  // differs from whatever came immediately before it, so the reset fires every time.
  goHome(drawer: { close: () => void }): void {
    drawer.close();
    if (this.authService.isCaptain()) {
      this.router.navigate(['/captain']);
      return;
    }
    this.router.navigate(['/']).then(() => {
      this.router.navigate(['/'], { queryParams: { tab: 'menu' } });
    });
  }

  // The sidebar's own auth quick-link target (see the drawer's "Login / Register"/"My
  // Orders" item in app.component.html). An authenticated (non-captain, non-admin-
  // specific) user goes to their own orders rather than back through the auth form;
  // /my-orders is itself authGuard-protected, so this is purely a shortcut, not the
  // only thing enforcing it.
  protected accountRoute(): string {
    return this.authService.isAuthenticated() ? '/my-orders' : '/login';
  }

  protected iconFor(category: string): string {
    return iconForCategory(category);
  }

  // Mirrors the old <mat-sidenav>'s (closed) timing for CartDrawerComponent's own
  // resetOnClose() (see that component's own comment on showGuestPrompt for why it
  // needs this at all) - resetting immediately on click, instead of waiting for the
  // close transition to actually finish, would flash the drawer back to its default
  // content while it's still visibly sliding off-screen. 320ms matches .cart-sheet's
  // own transition duration in app.component.scss.
  protected closeCartSheet(): void {
    this.cartOpen.set(false);
    if (this.cartSheetResetTimer !== null) {
      clearTimeout(this.cartSheetResetTimer);
    }
    this.cartSheetResetTimer = setTimeout(() => this.cartDrawerContentRef?.resetOnClose(), 320);
  }
}
