import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
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
import { Category } from './core/models/category.model';
import { CartDialogComponent } from './features/storefront/cart-dialog/cart-dialog.component';
import { UserAvatarComponent } from './shared/user-avatar/user-avatar.component';
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
    UserAvatarComponent,
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

  // The hamburger's category drawer - best-effort, same "don't block the app on this"
  // reasoning as StorefrontComponent's own category fetch.
  readonly categories = signal<Category[]>([]);

  constructor() {
    this.categoryService.getAll().subscribe({
      next: (categories) => {
        this.categories.set([...categories].sort((a, b) => a.displayOrder - b.displayOrder));
      }
    });
  }

  openCart(): void {
    this.dialog.open(CartDialogComponent, { width: '520px' });
  }

  // Closing the drawer before navigating avoids it staying open over the storefront
  // while the page underneath scrolls to the chosen category.
  selectCategory(name: string, drawer: { close: () => void }): void {
    drawer.close();
    this.router.navigate(['/'], { queryParams: { category: name } });
  }
}
