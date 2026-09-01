import { Component, inject } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatBadgeModule } from '@angular/material/badge';
import { MatDialog } from '@angular/material/dialog';
import { AuthService } from './core/services/auth.service';
import { CartService } from './core/services/cart.service';
import { SettingsService } from './core/services/settings.service';
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
  private readonly dialog = inject(MatDialog);

  openCart(): void {
    this.dialog.open(CartDialogComponent, { width: '520px' });
  }
}
