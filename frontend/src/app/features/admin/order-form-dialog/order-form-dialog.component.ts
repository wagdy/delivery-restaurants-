import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormArray, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { OrderService } from '../../../core/services/order.service';
import { MenuItemService } from '../../../core/services/menu-item.service';
import { MenuItem } from '../../../core/models/menu-item.model';
import { Order } from '../../../core/models/order.model';

export interface OrderFormDialogData {
  mode: 'create' | 'edit';
  order?: Order;
}

@Component({
  selector: 'app-order-form-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './order-form-dialog.component.html',
  styleUrl: './order-form-dialog.component.scss'
})
export class OrderFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly orderService = inject(OrderService);
  private readonly menuItemService = inject(MenuItemService);
  private readonly ref = inject(MatDialogRef<OrderFormDialogComponent>);
  readonly data: OrderFormDialogData = inject(MAT_DIALOG_DATA);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly menuItems = signal<MenuItem[]>([]);

  readonly form = this.fb.nonNullable.group({
    customerName: ['', [Validators.required, Validators.maxLength(200)]],
    customerPhone: ['', [Validators.required, Validators.maxLength(30)]],
    deliveryAddress: ['', [Validators.required, Validators.maxLength(500)]],
    items: this.fb.array<ReturnType<typeof this.buildItemGroup>>([])
  });

  constructor() {
    this.menuItemService.getAll().subscribe({
      next: (items) => {
        this.menuItems.set(items);
        this.loading.set(false);
        this.initializeForm();
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load menu items.');
      }
    });
  }

  get items(): FormArray {
    return this.form.controls.items;
  }

  private buildItemGroup(menuItemId: number | null = null, quantity = 1) {
    return this.fb.nonNullable.group({
      menuItemId: [menuItemId, [Validators.required]],
      quantity: [quantity, [Validators.required, Validators.min(1), Validators.max(100)]]
    });
  }

  private initializeForm(): void {
    if (this.data.mode === 'edit' && this.data.order) {
      const order = this.data.order;
      this.form.patchValue({
        customerName: order.customerName,
        customerPhone: order.customerPhone,
        deliveryAddress: order.deliveryAddress
      });
      order.items.forEach((item) => {
        this.items.push(this.buildItemGroup(item.menuItemId, item.quantity));
      });
    } else {
      this.items.push(this.buildItemGroup());
    }
  }

  addItem(): void {
    this.items.push(this.buildItemGroup());
  }

  removeItem(index: number): void {
    this.items.removeAt(index);
  }

  priceFor(menuItemId: number | null): number {
    return this.menuItems().find((m) => m.id === menuItemId)?.price ?? 0;
  }

  get estimatedTotal(): number {
    return this.items.controls.reduce((sum, group) => {
      const value = group.getRawValue() as { menuItemId: number | null; quantity: number };
      return sum + this.priceFor(value.menuItemId) * (value.quantity || 0);
    }, 0);
  }

  submit(): void {
    if (this.form.invalid || this.items.length === 0) {
      this.form.markAllAsTouched();
      if (this.items.length === 0) {
        this.errorMessage.set('Add at least one item.');
      }
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    const request = {
      customerName: raw.customerName,
      customerPhone: raw.customerPhone,
      deliveryAddress: raw.deliveryAddress,
      items: raw.items.map((i) => ({ menuItemId: i.menuItemId!, quantity: i.quantity, addOnIds: [] }))
    };

    const request$ =
      this.data.mode === 'create'
        ? this.orderService.create(request)
        : this.orderService.update(this.data.order!.id, request);

    request$.subscribe({
      next: (order) => {
        this.saving.set(false);
        this.ref.close(order);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to save order.');
      }
    });
  }

  cancel(): void {
    this.ref.close();
  }
}
