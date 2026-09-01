import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CartService } from '../../core/services/cart.service';
import { AuthService } from '../../core/services/auth.service';
import { OrderService } from '../../core/services/order.service';
import { CreateOrderRequest } from '../../core/models/order.model';
import { AddOnNamesPipe } from '../../shared/pipes/add-on-names.pipe';

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    AddOnNamesPipe,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule
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
  private readonly orderService = inject(OrderService);
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

  placeOrder(): void {
    if (this.form.invalid || this.cart.lines().length === 0) {
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
      }))
    };

    this.orderService.create(request).subscribe({
      next: (order) => {
        this.submitting.set(false);
        this.cart.clear();
        this.router.navigate(['/order-confirmation'], { state: { order } });
      },
      error: (err) => {
        this.submitting.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to place order. Please try again.');
      }
    });
  }
}
