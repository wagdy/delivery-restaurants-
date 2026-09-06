import { Component, computed, effect, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatRadioModule } from '@angular/material/radio';
import { MatIconModule } from '@angular/material/icon';
import { CartService } from '../../core/services/cart.service';
import { AuthService } from '../../core/services/auth.service';
import { OrderService } from '../../core/services/order.service';
import { SettingsService } from '../../core/services/settings.service';
import { CheckoutService } from '../../core/services/checkout.service';
import { CreateOrderRequest } from '../../core/models/order.model';
import { PaymentMethod, ValidatePromoResponse } from '../../core/models/checkout.model';
import { AddOnNamesPipe } from '../../shared/pipes/add-on-names.pipe';

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    AddOnNamesPipe,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatRadioModule,
    MatIconModule
  ],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent {
  // Kept identical to the [RegularExpression] patterns on CreateOrderRequest
  // (backend/.../DTOs/Orders/CreateOrderRequest.cs) — client-side validation is only a
  // fast-feedback convenience, the backend re-checks the same rule regardless.
  static readonly NAME_PATTERN = /^[A-Za-z ]+$/;
  static readonly PHONE_PATTERN = /^[0-9]+$/;

  protected readonly cart = inject(CartService);
  protected readonly authService = inject(AuthService);
  protected readonly settingsService = inject(SettingsService);
  private readonly orderService = inject(OrderService);
  private readonly checkoutService = inject(CheckoutService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    customerName: [
      this.authService.currentUser()?.fullName ?? '',
      [Validators.required, Validators.maxLength(200), Validators.pattern(CheckoutComponent.NAME_PATTERN)]
    ],
    customerPhone: [
      this.authService.currentUser()?.phoneNumber ?? '',
      [Validators.required, Validators.maxLength(30), Validators.pattern(CheckoutComponent.PHONE_PATTERN)]
    ],
    deliveryAddress: [
      this.authService.currentUser()?.address ?? '',
      [Validators.required, Validators.maxLength(500)]
    ]
  });

  // Settings are already loaded app-wide before bootstrap (see app.config.ts's
  // APP_INITIALIZER) - reading the shared signal directly here, no separate load() call.
  readonly enabledPaymentMethods = computed<PaymentMethod[]>(() => {
    const settings = this.settingsService.settings();
    const methods: PaymentMethod[] = [];
    if (settings.isCashEnabled) {
      methods.push('Cash');
    }
    if (settings.isVisaEnabled) {
      methods.push('Visa');
    }
    if (settings.isInstapayEnabled) {
      methods.push('Instapay');
    }
    return methods;
  });

  readonly paymentMethod = signal<PaymentMethod | null>(null);

  readonly promoCodeInput = signal('');
  readonly promoResult = signal<ValidatePromoResponse | null>(null);
  readonly promoError = signal<string | null>(null);
  readonly applyingPromo = signal(false);

  readonly affectedMenuItemIds = computed(() => new Set(this.promoResult()?.affectedMenuItemIds ?? []));

  // Tax/total fall back to a plain client-side estimate (no discount) whenever no promo
  // is applied, so the summary always shows a real number without waiting on a network
  // round trip just to display the tax line.
  readonly taxAmount = computed(() => {
    const promo = this.promoResult();
    if (promo) {
      return promo.taxAmount;
    }
    return Math.round(this.cart.subtotal() * (this.settingsService.settings().taxPercentage / 100) * 100) / 100;
  });

  readonly discountAmount = computed(() => this.promoResult()?.discountAmount ?? 0);
  readonly deliveryDiscountAmount = computed(() => this.promoResult()?.deliveryDiscountAmount ?? 0);

  readonly finalTotal = computed(() => {
    const promo = this.promoResult();
    if (promo) {
      return promo.total;
    }
    return this.cart.subtotal() + this.taxAmount() + this.cart.deliveryFee();
  });

  constructor() {
    // Defaults to the first enabled method the moment the list is known (immediately,
    // since settings are already loaded before bootstrap) and re-picks a valid one if
    // the currently selected method is ever no longer enabled.
    effect(() => {
      const methods = this.enabledPaymentMethods();
      const current = this.paymentMethod();
      if (methods.length === 0) {
        this.paymentMethod.set(null);
      } else if (current === null || !methods.includes(current)) {
        this.paymentMethod.set(methods[0]);
      }
    });
  }

  applyPromoCode(): void {
    const codeText = this.promoCodeInput().trim();
    if (!codeText || this.cart.lines().length === 0) {
      return;
    }

    this.applyingPromo.set(true);
    this.promoError.set(null);

    this.checkoutService
      .validatePromo({
        codeText,
        deliveryFee: this.cart.deliveryFee(),
        items: this.cart.lines().map((l) => ({
          menuItemId: l.menuItem.id,
          quantity: l.quantity,
          addOnIds: l.selectedAddOns.map((a) => a.id)
        }))
      })
      .subscribe({
        next: (result) => {
          this.applyingPromo.set(false);
          this.promoResult.set(result);
        },
        error: (err) => {
          this.applyingPromo.set(false);
          this.promoResult.set(null);
          this.promoError.set(err.error?.errors?.[0] ?? 'This promo code could not be applied.');
        }
      });
  }

  removePromoCode(): void {
    this.promoCodeInput.set('');
    this.promoResult.set(null);
    this.promoError.set(null);
  }

  placeOrder(): void {
    const method = this.paymentMethod();
    if (this.form.invalid || this.cart.lines().length === 0 || !method) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitting.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const request: CreateOrderRequest = {
      customerName: raw.customerName,
      customerPhone: raw.customerPhone,
      deliveryAddress: raw.deliveryAddress,
      items: this.cart.lines().map((l) => ({
        menuItemId: l.menuItem.id,
        quantity: l.quantity,
        addOnIds: l.selectedAddOns.map((a) => a.id)
      })),
      paymentMethod: method,
      promoCodeText: this.promoResult() ? this.promoCodeInput().trim() : null,
      deliveryFee: this.cart.deliveryFee()
    };

    this.orderService.create(request).subscribe({
      next: (order) => {
        this.submitting.set(false);
        this.cart.clear();

        const fawryUrl = this.settingsService.settings().visaFawryUrl;
        if (method === 'Visa' && fawryUrl) {
          // The order is already saved as Pending Payment (see OrderService.CreateAsync) -
          // a full navigation, not a router link, since Fawry is an external site this
          // app has no further control over.
          window.location.href = fawryUrl;
          return;
        }

        this.router.navigate(['/order-confirmation'], { state: { order } });
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to place order. Please try again.');
      }
    });
  }
}
