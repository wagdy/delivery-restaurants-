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
import { OrderStatusLabelPipe } from '../../shared/pipes/order-status-label.pipe';

// Orders that still need a captain's attention (accept or deliver) sort to the top;
// finished/cancelled orders sink to the bottom — a driver's queue, not a flat log.
const STATUS_PRIORITY: Record<Order['status'], number> = {
  Preparing: 0,
  Pending: 1,
  OutForDelivery: 2,
  Delivered: 3,
  // Collection orders are filtered out of this queue entirely (see filteredOrders), so
  // in normal operation these never sort anything. They are here because the Record is
  // exhaustive over OrderStatus - which is what caught this file the moment the two
  // statuses were added - and they mirror their delivery equivalents so the ordering
  // stays sane if the exclusion is ever relaxed.
  ReadyForCollection: 2,
  Collected: 3,
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
  ,
    OrderStatusLabelPipe],
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

    // Collection orders never reach a driver's queue - there is nothing to drive. The
    // branch prepares and hands them over, and admin keeps full status control over them
    // (see AdminDashboardComponent and OrderDetailsDialogComponent), so nothing becomes
    // unmanageable by being absent here. The matching backend change stops the captain
    // push firing for them too (OrderService.CreateAsync), so a driver is never paged
    // towards a list the order is not in.
    //
    // Filtered here rather than in loadOrders() on purpose: orders() keeps the full set,
    // so a deep link from a notification sent before this shipped
    // (/captain?orderId=123 - see openOrderFromQueryParamIfPresent) still resolves and
    // opens the details dialog, which says "Store pickup - no delivery" in plain words.
    // Dropping them at load time would make that link do nothing at all.
    const all = this.orders().filter((o) => !o.isPickup);

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
    return order.isPickup ? order.status === 'ReadyForCollection' : order.status === 'OutForDelivery';
  }

  // Pickup orders are excluded from this queue, so these two only ever run on
  // deliveries today. They still branch on isPickup so the card's own pickup markup -
  // kept as a backstop, see the template - would write the collection statuses rather
  // than silently marking a collection order "out for delivery" if the exclusion is
  // ever relaxed.
  acceptOrder(order: Order, event: Event): void {
    event.stopPropagation();
    this.setStatus(order, order.isPickup ? 'ReadyForCollection' : 'OutForDelivery');
  }

  markDelivered(order: Order, event: Event): void {
    event.stopPropagation();
    this.setStatus(order, order.isPickup ? 'Collected' : 'Delivered');
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
