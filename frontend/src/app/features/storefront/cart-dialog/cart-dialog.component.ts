import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { AuthService } from '../../../core/services/auth.service';
import { CartService } from '../../../core/services/cart.service';
import { AddOnNamesPipe } from '../../../shared/pipes/add-on-names.pipe';

@Component({
  selector: 'app-cart-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, AddOnNamesPipe],
  templateUrl: './cart-dialog.component.html',
  styleUrl: './cart-dialog.component.scss'
})
export class CartDialogComponent {
  protected readonly cart = inject(CartService);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly ref = inject(MatDialogRef<CartDialogComponent>);

  // Swaps the dialog's own content/actions to the loyalty prompt below rather than
  // stacking a second MatDialog on top of this one - this cart is already a dialog, so a
  // nested modal would be an awkward pattern. A signal (not a plain field) so the
  // template's @if reacts to it, matching this codebase's existing convention (see
  // CustomerInsightsComponent, etc.) over manual change detection.
  readonly showGuestPrompt = signal(false);

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
  // abandoning the cart dialog itself (CartService's own state is preserved regardless,
  // so their items are still there once they're back).
  registerNow(): void {
    this.ref.close();
    this.router.navigateByUrl('/register');
  }

  // "المتابعة كضيف" (Checkout as Guest) - the same path checkout() would have taken
  // immediately for a logged-in customer.
  continueAsGuest(): void {
    this.proceedToCheckout();
  }

  private proceedToCheckout(): void {
    this.ref.close();
    this.router.navigateByUrl('/checkout');
  }

  close(): void {
    this.ref.close();
  }
}
