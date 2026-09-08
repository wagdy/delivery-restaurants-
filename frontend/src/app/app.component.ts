import { Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { filter } from 'rxjs';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatSidenavModule } from '@angular/material/sidenav';
import { MatListModule } from '@angular/material/list';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from './core/services/auth.service';
import { CartService } from './core/services/cart.service';
import { SettingsService } from './core/services/settings.service';
import { CategoryService } from './core/services/category.service';
import { LoyaltyRealtimeService } from './core/services/loyalty-realtime.service';
import { Category } from './core/models/category.model';
import { CartDialogComponent } from './features/storefront/cart-dialog/cart-dialog.component';
import { AppFooterComponent } from './shared/app-footer/app-footer.component';

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
  private readonly dialog = inject(MatDialog);
  private readonly router = inject(Router);

  // Never referenced again after this - injecting it here is what instantiates the
  // providedIn: 'root' singleton and starts its connect/disconnect effect() app-wide.
  // Angular's DI is lazy otherwise, so without an eager injection like this the service
  // would never actually run until something else happened to need it.
  private readonly loyaltyRealtimeService = inject(LoyaltyRealtimeService);

  // The hamburger's category drawer - best-effort, same "don't block the app on this"
  // reasoning as StorefrontComponent's own category fetch.
  readonly categories = signal<Category[]>([]);

  // True for the fully public, chrome-free customer-facing pages ("/rate/store",
  // "/customer-review/:orderId") - this app-wide toolbar/sidenav (cart, login, admin
  // link, category drawer) would look completely out of place on a page reached from a
  // WhatsApp link by a customer who may not even be logged in. Initialized from the
  // current URL (not just NavigationEnd) so a hard-reload directly on one of these URLs
  // starts bare too, rather than flashing the full chrome for one frame before the first
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
  }

  private static isBareUrl(url: string): boolean {
    return url.startsWith('/rate/') || url.startsWith('/customer-review/');
  }

  openCart(): void {
    this.dialog.open(CartDialogComponent, {
      width: '448px',
      // Panel-level backstop alongside the dialog's own internal max-width: 28rem (see
      // cart-dialog.component.scss's .cart-body) - without this, the 448px target width
      // alone would still overflow any viewport narrower than that.
      maxWidth: '95vw'
    });
  }

  // Closing the drawer before navigating avoids it staying open over the storefront
  // while the page underneath scrolls to the chosen category.
  selectCategory(name: string, drawer: { close: () => void }): void {
    drawer.close();
    this.router.navigate(['/'], { queryParams: { category: name } });
  }
}
