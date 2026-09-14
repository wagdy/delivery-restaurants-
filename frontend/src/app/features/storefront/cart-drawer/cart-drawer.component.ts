import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';
import { SettingsService } from '../../../core/services/settings.service';
import { AddOnNamesPipe } from '../../../shared/pipes/add-on-names.pipe';
import { estimatedDeliveryLabel as formatDeliveryLabel } from '../../../shared/utils/delivery-time.util';

// The minimal slice of MatSidenav this component actually needs to close itself - mirrors
// AppComponent.selectCategory's own `drawer: { close: () => void }` parameter, the
// existing convention in this codebase for a component that lives inside a <mat-sidenav>
// but has no dialog/overlay service of its own to close through.
export interface DrawerHandle {
  close(): void;
}

@Component({
  selector: 'app-cart-drawer',
  standalone: true,
  imports: [CommonModule, MatButtonModule, MatIconModule, AddOnNamesPipe],
  // OnPush: every piece of state this component renders is a signal, so Angular
  // can skip it entirely unless one of them actually changed. Without it, the cart sheet, re-rendered on every quantity change
  // was re-checked on every unrelated async event anywhere in the app.
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './cart-drawer.component.html',
  styleUrl: './cart-drawer.component.scss'
})
export class CartDrawerComponent {
  // The host <mat-sidenav #cartDrawer> itself (see app.component.html) - passed in
  // rather than injected, since this component is plain content inside a sidenav, not a
  // MatDialogRef-backed dialog.
  readonly drawer = input.required<DrawerHandle>();

  protected readonly cart = inject(CartService);
  protected readonly settingsService = inject(SettingsService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly estimatedDeliveryLabel = computed(() => formatDeliveryLabel(this.settingsService.settings()));

  // Swaps the drawer's own content/actions to the loyalty prompt below rather than
  // opening a dialog on top of it - a signal (not a plain field) so the template's @if
  // reacts to it, matching this codebase's existing convention.
  //
  // Unlike the old MatDialog-based cart, this component lives inside a <mat-sidenav> and
  // is created once, not re-instantiated on every open - so this signal would otherwise
  // stay stuck true forever after the first guest-checkout attempt, even for a customer
  // who later logs in. Reset from app.component.html's (closed) on the sidenav itself
  // instead, via resetOnClose() below - (closed) rather than (closedStart) so the reset
  // happens after the panel is off-screen, not mid-close-animation where it would flash.
  readonly showGuestPrompt = signal(false);

  // Called from the host <mat-sidenav>'s (closed) - see the comment on showGuestPrompt
  // above for why a persistent component needs this at all.
  resetOnClose(): void {
    this.showGuestPrompt.set(false);
  }

  // A guest here means "no authenticated session" - AuthService.isAuthenticated covers
  // customers, captains and admins alike, but only a customer ever reaches this cart, so
  // it's an exact proxy for "not logged in as a customer" with no extra role check needed.
  checkout(): void {
    if (this.authService.isAuthenticated()) {
      this.proceedToCheckout();
      return;
    }

    this.showGuestPrompt.set(true);
  }

  // "تسجيل حساب" (Register) - takes the guest to the self-service registration tab,
  // abandoning the cart drawer itself (CartService's own state is preserved regardless,
  // so their items are still there once they're back).
  registerNow(): void {
    this.drawer().close();
    this.router.navigateByUrl('/register');
  }

  // "المتابعة كضيف" (Checkout as Guest) - the same path checkout() would have taken
  // immediately for a logged-in customer.
  continueAsGuest(): void {
    this.proceedToCheckout();
  }

  private proceedToCheckout(): void {
    this.drawer().close();
    this.router.navigateByUrl('/checkout');
  }

  close(): void {
    this.drawer().close();
  }
}
