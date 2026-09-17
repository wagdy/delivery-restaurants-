import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatExpansionModule } from '@angular/material/expansion';
import { OrderService } from '../../core/services/order.service';
import { Order } from '../../core/models/order.model';
import { AddOnNamesPipe } from '../../shared/pipes/add-on-names.pipe';
import { OrderStatusLabelPipe } from '../../shared/pipes/order-status-label.pipe';

@Component({
  selector: 'app-my-orders',
  standalone: true,
  imports: [
    CommonModule,
    RouterLink,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatExpansionModule,
    AddOnNamesPipe
  ,
    OrderStatusLabelPipe],
  // OnPush: every piece of state this component renders is a signal, so Angular
  // can skip it entirely unless one of them actually changed. Without it, a paginated accordion of orders
  // was re-checked on every unrelated async event anywhere in the app.
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './my-orders.component.html',
  styleUrl: './my-orders.component.scss'
})
export class MyOrdersComponent {
  private readonly orderService = inject(OrderService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly orders = signal<Order[]>([]);

  // Client-side pagination - getMyOrders() below returns the customer's entire order
  // history in one response (unlike OrderService.getAll()'s server-side page params,
  // used by the admin dashboard), so paging just slices the array already in memory
  // instead of re-fetching per page.
  readonly pageSize = 5;
  readonly currentPage = signal(1);

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.orders().length / this.pageSize)));

  readonly pagedOrders = computed(() => {
    const start = (this.currentPage() - 1) * this.pageSize;
    return this.orders().slice(start, start + this.pageSize);
  });

  // The numbered page buttons at the bottom of the list, e.g. [1] [2] [3].
  readonly pageNumbers = computed(() => Array.from({ length: this.totalPages() }, (_, i) => i + 1));

  constructor() {
    this.orderService.getMyOrders().subscribe({
      next: (orders) => {
        this.orders.set(orders);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load your orders.');
      }
    });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) {
      return;
    }
    this.currentPage.set(page);
  }
}
