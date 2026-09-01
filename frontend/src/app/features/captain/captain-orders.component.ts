import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { OrderService } from '../../core/services/order.service';
import { Order } from '../../core/models/order.model';
import { OrderDetailsDialogComponent } from '../admin/order-details-dialog/order-details-dialog.component';
import { PushNotificationService } from '../../core/services/push-notification.service';
import { IosInstallPromptComponent } from '../../shared/ios-install-prompt/ios-install-prompt.component';

// Orders that still need a captain's attention (accept or deliver) sort to the top;
// finished/cancelled orders sink to the bottom — a driver's queue, not a flat log.
const STATUS_PRIORITY: Record<Order['status'], number> = {
  Preparing: 0,
  Pending: 1,
  OutForDelivery: 2,
  Delivered: 3,
  Cancelled: 4
};

@Component({
  selector: 'app-captain-orders',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatToolbarModule,
    IosInstallPromptComponent
  ],
  templateUrl: './captain-orders.component.html',
  styleUrl: './captain-orders.component.scss'
})
export class CaptainOrdersComponent {
  private readonly orderService = inject(OrderService);
  private readonly dialog = inject(MatDialog);
  private readonly pushNotificationService = inject(PushNotificationService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly orders = signal<Order[]>([]);
  readonly searchTerm = signal('');
  readonly updatingOrderId = signal<number | null>(null);

  readonly filteredOrders = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const all = this.orders();
    const matching = !term
      ? all
      : all.filter(
          (o) =>
            o.customerName.toLowerCase().includes(term) ||
            o.customerPhone.toLowerCase().includes(term) ||
            String(o.id).includes(term)
        );

    return [...matching].sort((a, b) => {
      const priorityDiff = STATUS_PRIORITY[a.status] - STATUS_PRIORITY[b.status];
      if (priorityDiff !== 0) {
        return priorityDiff;
      }
      return new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime();
    });
  });

  constructor() {
    this.loadOrders();
    // Request notification permission + register the subscription. Safe to call on every
    // visit: it's a no-op once the browser has already decided permission and we're subscribed.
    void this.pushNotificationService.subscribe();
  }

  loadOrders(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.orderService.getAll(null, 1, 200).subscribe({
      next: (result) => {
        this.orders.set(result.items);
        this.loading.set(false);
        this.openOrderFromQueryParamIfPresent();
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load orders.');
      }
    });
  }

  // Supports the "click a push notification" deep link: the backend sends
  // /captain?orderId=123, and Angular's service worker navigates here directly
  // even if the app wasn't already open.
  private openOrderFromQueryParamIfPresent(): void {
    const orderIdParam = this.route.snapshot.queryParamMap.get('orderId');
    if (!orderIdParam) {
      return;
    }

    const orderId = Number(orderIdParam);
    const order = this.orders().find((o) => o.id === orderId);

    // Clear the query param so a manual refresh doesn't reopen the dialog.
    this.router.navigate([], { queryParams: {}, replaceUrl: true });

    if (order) {
      this.openDetails(order);
    }
  }

  canAccept(order: Order): boolean {
    return order.status === 'Pending' || order.status === 'Preparing';
  }

  canMarkDelivered(order: Order): boolean {
    return order.status === 'OutForDelivery';
  }

  acceptOrder(order: Order, event: Event): void {
    event.stopPropagation();
    this.setStatus(order, 'OutForDelivery');
  }

  markDelivered(order: Order, event: Event): void {
    event.stopPropagation();
    this.setStatus(order, 'Delivered');
  }

  private setStatus(order: Order, status: Order['status']): void {
    this.updatingOrderId.set(order.id);
    this.orderService.updateStatus(order.id, status).subscribe({
      next: (updated) => {
        this.updatingOrderId.set(null);
        this.orders.set(this.orders().map((o) => (o.id === updated.id ? updated : o)));
      },
      error: () => {
        this.updatingOrderId.set(null);
        this.errorMessage.set('Failed to update order status.');
      }
    });
  }

  openDetails(order: Order): void {
    const dialogRef = this.dialog.open(OrderDetailsDialogComponent, {
      width: '640px',
      data: order
    });

    dialogRef.afterClosed().subscribe((mutated: boolean | undefined) => {
      if (mutated) {
        this.loadOrders();
      }
    });
  }
}
