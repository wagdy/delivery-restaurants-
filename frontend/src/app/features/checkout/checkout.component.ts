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
import { estimatedDeliveryLabel as formatDeliveryLabel } from '../../shared/utils/delivery-time.util';

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
  // Arabic (؀-ۿ) as well as Latin letters. The old pattern was Latin-only,
  // which rejected any customer who typed their name in Arabic - and because this form
  // prefills the name from the signed-in profile, and registration has always ALLOWED
  // Arabic, a customer registered as "محمد" had their own name filled in and then
  // refused. Kept identical to CreateOrderRequest's [RegularExpression], which had the
  // same Latin-only bug and was rejecting these orders server-side regardless of what
  // the client did.
  static readonly NAME_PATTERN = /^[a-zA-Z\u0600-\u06FF\s]+$/;

  // Egyptian mobile numbers: 010/011/012/015 followed by 8 digits, 11 in total. Stricter
  // than the backend's own ^[0-9]+$, deliberately - that rule is shared with the admin
  // "Create Order" screen and the POS sync, which legitimately carry landlines and
  // foreign numbers. This is the customer-facing form, where a typo means the delivery
  // driver cannot call and the WhatsApp confirmation never arrives.
  static readonly PHONE_PATTERN = /^(010|011|012|015)\d{8}$/;

  protected readonly cart = inject(CartService);
  protected readonly authService = inject(AuthService);
  protected readonly settingsService = inject(SettingsService);
  private readonly orderService = inject(OrderService);
  private readonly checkoutService = inject(CheckoutService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);

  readonly submitting = signal(false);
  readonly errorMessage = signal<string | null>(null);

  // A signed-in customer's name and phone come from their profile and are shown as a
  // summary rather than as editable fields - there is nothing for them to fill in but the
  // address. The controls still exist and are still validated, because the request body
  // needs both values and because a profile saved before these rules existed could hold
  // something that no longer passes - see profileContactIsValid below, which falls back to
  // the editable form in exactly that case rather than trapping the customer behind a
  // summary card they cannot correct.
  readonly isSignedIn = computed(() => this.authService.isAuthenticated());

  // Set by the summary card's "Change" button. There is no profile-editing screen in this
  // app (see app.routes.ts - my-orders is a read-only list), so the only way to let a
  // signed-in customer deliver under a different name or number is to reveal the same
  // fields a guest gets. This is per-order only; it does not write back to the profile.
  readonly editingContact = signal(false);

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
    ],
    // Optional apartment/floor/landmark detail - merged into deliveryAddress on submit
    // (see placeOrder()) rather than sent as its own field, since the backend's
    // CreateOrderRequest has just the one free-text address column.
    deliveryNotes: ['', [Validators.maxLength(200)]]
  });

  // Whether the profile's stored name and phone actually satisfy the rules above. A
  // profile predating them - a name with a digit in it, a phone saved as +20... or a
  // landline - would otherwise render a read-only summary card with no way to fix the
  // very values blocking the order. When this is false the full editable form is shown
  // instead, prefilled, so the customer can correct it and continue.
  readonly profileContactIsValid = computed(() => {
    const user = this.authService.currentUser();
    if (!user) {
      return false;
    }

    return (
      !!user.fullName?.trim() &&
      CheckoutComponent.NAME_PATTERN.test(user.fullName) &&
      !!user.phoneNumber?.trim() &&
      CheckoutComponent.PHONE_PATTERN.test(user.phoneNumber)
    );
  });

  // The summary card replaces the name/phone inputs only when both are present and valid,
  // and only until the customer asks to change them for this order.
  readonly showContactSummary = computed(
    () => this.isSignedIn() && this.profileContactIsValid() && !this.editingContact()
  );

  // Strips anything that isn't a digit as the customer types, so a stray letter or the
  // spaces in "010 1234 5678" never reach the validator as a confusing error - the field
  // simply cannot hold a non-digit. Capped at 11, the full length of an Egyptian mobile
  // number; the pattern validator still enforces the 010/011/012/015 prefix.
  protected onPhoneInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const cleaned = CheckoutComponent.normalizePhone(input.value);

    if (cleaned !== input.value) {
      input.value = cleaned;
      // Written back through the control, not just the DOM, so the form's validity and
      // what is on screen never disagree.
      this.form.controls.customerPhone.setValue(cleaned);
    }
  }

  // Digits only, with the international form folded back to the local one. Numbers copied
  // out of a WhatsApp contact arrive as "+20 101 234 5678" or "00201012345678", which is
  // the same number as 01012345678 - rejecting it as "not an Egyptian mobile" would be
  // both wrong and baffling. Only an exactly-12-digit 20-prefixed string is treated this
  // way, so a customer typing their number one digit at a time never trips it.
  private static normalizePhone(raw: string): string {
    let digits = raw.replace(/\D/g, '');

    if (digits.startsWith('00')) {
      digits = digits.slice(2);
    }
    if (digits.length === 12 && digits.startsWith('20')) {
      digits = `0${digits.slice(2)}`;
    }

    return digits.slice(0, 11);
  }

  // Collapsed by default so the form doesn't look longer than it needs to for the
  // common case of an address that's already complete on its own.
  readonly showDeliveryNotes = signal(false);

  readonly estimatedDeliveryLabel = computed(() => formatDeliveryLabel(this.settingsService.settings()));

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
    const notes = raw.deliveryNotes.trim();
    // Folded into the one free-text address field the backend actually has (see
    // CreateOrderRequest.cs) rather than sent separately - there's no DeliveryNotes
    // column to send it to. Truncated to the backend's own [MaxLength(500)] so a long
    // address plus notes can never fail order placement on a limit the customer never
    // saw enforced on this combined string (each field's own maxLength validates the
    // parts, not the concatenation).
    const deliveryAddress = (notes ? `${raw.deliveryAddress} (${notes})` : raw.deliveryAddress).slice(0, 500);

    const request: CreateOrderRequest = {
      customerName: raw.customerName,
      customerPhone: raw.customerPhone,
      deliveryAddress,
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
