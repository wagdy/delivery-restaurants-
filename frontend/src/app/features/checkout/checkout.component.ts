import { Component, ElementRef, ViewChild, computed, effect, inject, signal } from '@angular/core';
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
import { MatSelectModule } from '@angular/material/select';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CartService } from '../../core/services/cart.service';
import { AuthService } from '../../core/services/auth.service';
import { OrderService } from '../../core/services/order.service';
import { SettingsService } from '../../core/services/settings.service';
import { CheckoutService } from '../../core/services/checkout.service';
import { CreateOrderRequest } from '../../core/models/order.model';
import { PaymentMethod, ValidatePromoResponse } from '../../core/models/checkout.model';
import { AddOnNamesPipe } from '../../shared/pipes/add-on-names.pipe';
import { estimatedDeliveryLabel as formatDeliveryLabel } from '../../shared/utils/delivery-time.util';
import {
  EGYPT_MOBILE_PATTERN,
  NAME_PATTERN,
  normalizeEgyptMobile
} from '../../shared/utils/validation-patterns.util';
import { LocalNamePipe } from '../../shared/pipes/local-name.pipe';
import { LanguageService } from '../../core/services/language.service';

// Delivery or collect-in-store. Kept as a string union rather than a boolean so the
// template reads as what it is and a third mode (curbside, dine-in) is an addition
// rather than a rewrite.
export type Fulfilment = 'delivery' | 'pickup';

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
    MatIconModule,
    MatSelectModule,
    LocalNamePipe
  ],
  templateUrl: './checkout.component.html',
  // Two stylesheets, not one, to stay inside the per-file style budget - see the header
  // comment in checkout-fulfilment.component.scss.
  styleUrls: ['./checkout.component.scss', './checkout-fulfilment.component.scss']
})
export class CheckoutComponent {
  protected readonly cart = inject(CartService);
  protected readonly languageService = inject(LanguageService);
  protected readonly authService = inject(AuthService);
  protected readonly settingsService = inject(SettingsService);
  private readonly orderService = inject(OrderService);
  private readonly checkoutService = inject(CheckoutService);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

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
      [Validators.required, Validators.maxLength(200), Validators.pattern(NAME_PATTERN)]
    ],
    customerPhone: [
      this.authService.currentUser()?.phoneNumber ?? '',
      [Validators.required, Validators.maxLength(30), Validators.pattern(EGYPT_MOBILE_PATTERN)]
    ],
    deliveryAddress: [
      this.authService.currentUser()?.address ?? '',
      [Validators.required, Validators.maxLength(500)]
    ],
    // Optional apartment/floor/landmark detail - merged into deliveryAddress on submit
    // (see placeOrder()) rather than sent as its own field, since the backend's
    // CreateOrderRequest has just the one free-text address column.
    deliveryNotes: ['', [Validators.maxLength(200)]],
    // Pickup only. Folded into the same free-text address column on submit, for the same
    // reason deliveryNotes is: there is no pickup-time field on the order, and a chosen
    // time nobody in the branch can see would be worse than none at all.
    pickupTime: ['asap', [Validators.maxLength(50)]],
    // "Special request" on the review step. Sent as CreateOrderRequest.Notes, whose
    // column is MaxLength(1000) - matched here so an over-long note is caught while the
    // customer is still looking at the box rather than as a server error after submit.
    specialRequest: ['', [Validators.maxLength(1000)]]
  });

  // ---------------------------------------------------------------------------
  // Two-step flow: review what you ordered, then say where it goes
  // ---------------------------------------------------------------------------

  // 1 = review the order, 2 = delivery details and payment. The old layout put delivery
  // first and buried the summary below the Place order button, so the thing a customer
  // most wants to check before committing was the last thing they could see.
  readonly step = signal<1 | 2>(1);

  @ViewChild('stepHeading') private stepHeadingRef?: ElementRef<HTMLElement>;

  protected goToDeliveryStep(): void {
    this.step.set(2);
    this.focusStepHeading();
  }

  protected backToReviewStep(): void {
    this.step.set(1);
    this.focusStepHeading();
  }

  // Swapping the step replaces the whole view, and without this the page keeps the
  // scroll position of the step that just left - on a long order that drops the customer
  // into the middle of the new step. Moving focus to the new heading also tells a screen
  // reader that the view changed, which a plain signal flip does not.
  private focusStepHeading(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
    // After the @if has actually swapped the DOM, not before.
    setTimeout(() => this.stepHeadingRef?.nativeElement.focus(), 0);
  }

  // ---------------------------------------------------------------------------
  // Delivery vs store pickup
  // ---------------------------------------------------------------------------

  readonly fulfilment = signal<Fulfilment>('delivery');
  readonly isPickup = computed(() => this.fulfilment() === 'pickup');

  // Presentation only. The server re-checks this same setting when the order is posted
  // (OrderService.CreateAsync), which is what actually prevents a pickup order while the
  // branch has pickup switched off - this just stops the customer getting that far.
  readonly pickupEnabled = computed(() => this.settingsService.settings().isPickupEnabled);

  // The branch address the admin already maintains under Contact & Footer, rather than a
  // second copy of it hardcoded here - one address to keep correct, not two.
  readonly pickupLocation = computed(
    () => this.settingsService.settings().address?.trim() || this.settingsService.settings().restaurantName?.trim() || null
  );

  readonly pickupDuration = computed(() => this.settingsService.settings().pickupDuration?.trim() || null);

  // Shown inline next to the toggle when someone picks an unavailable option. Separate
  // from the snackbar deliberately: a snackbar is transient and easy to miss if the tap
  // scrolled the page, so the reason stays on screen until they choose something else.
  readonly pickupUnavailableMessage = signal<string | null>(null);

  // Rendered as-is; the customer-facing copy for this one is Arabic per the brief.
  private static readonly PICKUP_UNAVAILABLE =
    'عفواً، خدمة الاستلام من الفرع غير متاحة حالياً (Sorry, Store Pickup is currently unavailable.)';

  // "As soon as possible" plus half-hour slots for the rest of the day. Built once at
  // construction rather than as a computed: it is anchored to when the customer opened
  // checkout, and a list that silently reshuffled underneath them mid-order would be
  // worse than one that is a few minutes stale.
  readonly pickupTimeOptions = CheckoutComponent.buildPickupTimes();

  // The single entry point for changing mode - the template never sets fulfilment()
  // directly, so the disabled-pickup interception below cannot be bypassed by a future
  // caller forgetting to check.
  protected selectFulfilment(next: Fulfilment): void {
    if (next === 'pickup' && !this.pickupEnabled()) {
      // Deliberately does NOT change fulfilment(): the form stays in delivery mode with
      // its address validator intact, so an intercepted click can never leave the order
      // in a half-configured state.
      this.pickupUnavailableMessage.set(CheckoutComponent.PICKUP_UNAVAILABLE);
      this.snackBar.open(CheckoutComponent.PICKUP_UNAVAILABLE, 'حسناً', { duration: 6000 });
      return;
    }

    this.pickupUnavailableMessage.set(null);
    this.fulfilment.set(next);
    this.applyFulfilmentValidators();
  }

  // The address is required to deliver to and meaningless to collect from, so its
  // validators move with the mode rather than being a permanent fixture. Cleared rather
  // than disabled: a disabled control drops out of getRawValue()'s type in a way that
  // would make placeOrder() read undefined, and the value is still wanted if the
  // customer switches back to delivery.
  private applyFulfilmentValidators(): void {
    const address = this.form.controls.deliveryAddress;

    if (this.isPickup()) {
      address.clearValidators();
    } else {
      address.setValidators([Validators.required, Validators.maxLength(500)]);
    }

    // The address field is hidden in pickup mode, so any error it was showing has to go
    // with it - otherwise switching to pickup leaves a red "address is required" alert
    // pointing at a field that is no longer on screen.
    address.markAsUntouched();
    address.updateValueAndValidity();
  }

  private static buildPickupTimes(): { value: string; label: string }[] {
    const options = [{ value: 'asap', label: 'As soon as possible' }];
    const slot = new Date();

    // Start at the next half hour that is at least 20 minutes out - enough lead time for
    // the kitchen, and rounded so the list reads as times rather than odd minutes.
    slot.setMinutes(slot.getMinutes() + 20);
    slot.setMinutes(slot.getMinutes() > 30 ? 60 : 30, 0, 0);

    const endOfDay = new Date(slot);
    endOfDay.setHours(23, 30, 0, 0);

    while (slot <= endOfDay) {
      const label = slot.toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' });
      options.push({ value: label, label });
      slot.setMinutes(slot.getMinutes() + 30);
    }

    return options;
  }

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
      NAME_PATTERN.test(user.fullName) &&
      !!user.phoneNumber?.trim() &&
      EGYPT_MOBILE_PATTERN.test(user.phoneNumber)
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
    const cleaned = normalizeEgyptMobile(input.value);

    if (cleaned !== input.value) {
      input.value = cleaned;
      // Written back through the control, not just the DOM, so the form's validity and
      // what is on screen never disagree.
      this.form.controls.customerPhone.setValue(cleaned);
    }
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

  // Nothing is delivered on a pickup order, so nothing is charged for delivery. The
  // server applies the same rule independently (OrderService.CreateAsync zeroes it) -
  // this is what keeps the summary the customer reads agreeing with what they are
  // actually charged.
  readonly effectiveDeliveryFee = computed(() => (this.isPickup() ? 0 : this.cart.deliveryFee()));

  readonly discountAmount = computed(() => this.promoResult()?.discountAmount ?? 0);
  readonly deliveryDiscountAmount = computed(() => this.promoResult()?.deliveryDiscountAmount ?? 0);

  readonly finalTotal = computed(() => {
    const promo = this.promoResult();
    if (promo) {
      return promo.total;
    }
    return this.cart.subtotal() + this.taxAmount() + this.effectiveDeliveryFee();
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
        deliveryFee: this.effectiveDeliveryFee(),
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
    // Both steps now live inside one <form>, so pressing Enter in any field on the review
    // step - the promo code box, the special request - would otherwise submit the whole
    // order. For a signed-in customer with a saved address the form can already be valid
    // at that point, so the order would go through before they had seen the delivery step
    // at all. The Place order button only exists on step 2; this makes that the rule
    // rather than a coincidence of layout.
    if (this.step() !== 2) {
      return;
    }

    const method = this.paymentMethod();
    if (this.form.invalid || this.cart.lines().length === 0 || !method) {
      this.form.markAllAsTouched();
      return;
    }

    this.submitOrder(this.buildAddressLine(), method);
  }

  // The backend has exactly one free-text address column, so both modes have to say what
  // they need inside it - see CreateOrderRequest.cs. Truncated to that column's own
  // [MaxLength(500)] in both branches, since each control's maxLength validates its own
  // part and not the concatenation.
  private buildAddressLine(): string {
    const raw = this.form.getRawValue();

    // On a pickup order this is not an address at all: it is the only place the branch
    // will see WHICH branch and WHEN, because the order has no pickup-time field of its
    // own. Prefixed so nobody reads it as somewhere to drive to.
    if (this.isPickup()) {
      const when = raw.pickupTime === 'asap' ? 'as soon as possible' : `at ${raw.pickupTime}`;
      const where = this.pickupLocation() ?? 'the branch';
      return `STORE PICKUP - ${where} - ready ${when}`.slice(0, 500);
    }

    const notes = raw.deliveryNotes.trim();
    return (notes ? `${raw.deliveryAddress} (${notes})` : raw.deliveryAddress).slice(0, 500);
  }

  private submitOrder(deliveryAddress: string, method: PaymentMethod): void {
    this.submitting.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
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
      deliveryFee: this.effectiveDeliveryFee(),
      isPickup: this.isPickup(),
      // Stored in Order.Notes, which the admin order-details dialog already renders.
      notes: raw.specialRequest.trim() || null
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
