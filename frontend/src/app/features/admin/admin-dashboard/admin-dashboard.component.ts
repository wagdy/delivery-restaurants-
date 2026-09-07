import { Component, DestroyRef, ElementRef, HostListener, OnInit, ViewChild, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { saveAs } from 'file-saver';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { OrderService } from '../../../core/services/order.service';
import { DgteraSyncService } from '../../../core/services/dgtera-sync.service';
import { OrderRealtimeService } from '../../../core/services/order-realtime.service';
import { ORDER_STATUSES, Order, OrderStatus } from '../../../core/models/order.model';
import { OrderDetailsDialogComponent } from '../order-details-dialog/order-details-dialog.component';
import { OrderFormDialogComponent } from '../order-form-dialog/order-form-dialog.component';

@Component({
  selector: 'app-admin-dashboard',
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
    MatToolbarModule
  ],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss'
})
export class AdminDashboardComponent implements OnInit {
  private readonly orderService = inject(OrderService);
  private readonly dgteraSyncService = inject(DgteraSyncService);
  private readonly orderRealtimeService = inject(OrderRealtimeService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);

  @ViewChild('fileInput') private readonly fileInput?: ElementRef<HTMLInputElement>;

  readonly statuses = ORDER_STATUSES;
  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly orders = signal<Order[]>([]);
  readonly searchTerm = signal('');
  readonly syncing = signal(false);
  readonly downloadingTemplate = signal(false);
  readonly uploading = signal(false);

  // Orders whose new-order alarm hasn't been dismissed yet this session (populated only
  // from live SignalR pushes below - an order's own persisted isAcknowledged flag is
  // what the backend tracks, but this set is what actually drives the alarm/button UI,
  // so a page refresh doesn't resurrect an alarm for an order that arrived before the
  // reload, per this component's own established "duplicate guard" pattern).
  readonly unacknowledgedOrderIds = signal<Set<number>>(new Set());
  // Browsers block Audio.play() before the page has seen a user gesture - this tracks
  // whether that gesture has happened yet, so the "click anywhere to enable alerts"
  // banner knows when to hide itself.
  readonly audioUnlocked = signal(false);

  // A real bundled asset (public/assets/sounds/alarm.wav - .wav rather than .mp3 since
  // that's what's actually encoded; browsers play it identically either way), not a
  // runtime-synthesized tone - loop = true means it keeps ringing until every pending
  // order is acknowledged, not just once.
  private readonly alarmAudio = new Audio('assets/sounds/alarm.wav');

  readonly filteredOrders = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    const all = this.orders();
    if (!term) {
      return all;
    }
    return all.filter(
      (o) =>
        o.customerName.toLowerCase().includes(term) ||
        o.customerPhone.toLowerCase().includes(term) ||
        String(o.id).includes(term)
    );
  });

  constructor() {
    this.loadOrders();

    // Resumes the alarm the instant audio unlocks, if orders were already waiting -
    // covers the case where a new order arrives before the cashier's first click.
    effect(() => {
      if (this.audioUnlocked() && this.unacknowledgedOrderIds().size > 0) {
        this.alarmAudio.play().catch(() => {});
      }
    });
  }

  ngOnInit(): void {
    this.alarmAudio.loop = true;

    this.orderRealtimeService.newOrderReceived.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((notification) => {
      // Duplicate guard: this same order could also arrive via a background
      // loadOrders() refresh (e.g. from syncDgteraOrders()) racing the socket push.
      if (this.orders().some((o) => o.id === notification.orderId)) {
        return;
      }

      this.orderService.getById(notification.orderId).subscribe({
        next: (order) => {
          // Re-check post-fetch in case the race above resolved while this HTTP call
          // was in flight.
          if (!this.orders().some((o) => o.id === order.id)) {
            this.orders.update((current) => [order, ...current]);
          }
        }
      });

      this.unacknowledgedOrderIds.update((ids) => new Set(ids).add(notification.orderId));
      this.startAlarmIfNeeded();

      this.snackBar
        .open(
          `New order received - ${notification.customerName} (${notification.totalAmount.toFixed(2)} EGP)`,
          'View',
          { duration: 8000 }
        )
        .onAction()
        .subscribe(() => {
          const order = this.orders().find((o) => o.id === notification.orderId);
          if (order) {
            this.openDetails(order);
          }
        });
    });
  }

  // First user gesture anywhere on the page "unlocks" the alarm's Audio element per
  // browser autoplay policy - a play()-then-immediately-pause() is the standard trick
  // (no audible glitch, since it's paused within the same microtask before any samples
  // are actually rendered to the speakers).
  @HostListener('document:click')
  unlockAudio(): void {
    if (this.audioUnlocked()) {
      return;
    }

    this.alarmAudio
      .play()
      .then(() => {
        this.alarmAudio.pause();
        this.alarmAudio.currentTime = 0;
        this.audioUnlocked.set(true);
      })
      .catch(() => {
        // Still blocked - the next click retries. No harm in leaving the banner up.
      });
  }

  private startAlarmIfNeeded(): void {
    if (this.unacknowledgedOrderIds().size === 0 || !this.audioUnlocked()) {
      return;
    }
    this.alarmAudio.play().catch((err) => console.error('Failed to play new-order alarm.', err));
  }

  private stopAlarmIfNoneLeft(): void {
    if (this.unacknowledgedOrderIds().size > 0) {
      return;
    }
    this.alarmAudio.pause();
    this.alarmAudio.currentTime = 0;
  }

  // Cashier clicked "Order Received" (تم استلام الطلب) on a specific card - persists the
  // acknowledgment server-side (so it survives a refresh/re-login) and, only once every
  // other pending order is also dismissed, stops the shared alarm.
  acknowledgeOrder(order: Order): void {
    this.orderService.acknowledge(order.id).subscribe({
      next: (updated) => {
        this.orders.update((current) => current.map((o) => (o.id === updated.id ? updated : o)));
        this.unacknowledgedOrderIds.update((ids) => {
          const next = new Set(ids);
          next.delete(order.id);
          return next;
        });
        this.stopAlarmIfNoneLeft();
      },
      error: () => {
        this.snackBar.open('Failed to acknowledge order.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  isUnacknowledged(order: Order): boolean {
    return this.unacknowledgedOrderIds().has(order.id);
  }

  columnOrders(status: OrderStatus): Order[] {
    return this.filteredOrders()
      .filter((o) => o.status === status)
      .sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
  }

  loadOrders(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.orderService.getAll(null, 1, 200).subscribe({
      next: (result) => {
        this.orders.set(result.items);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load orders.');
      }
    });
  }

  syncDgteraOrders(): void {
    if (this.syncing()) {
      return;
    }

    this.syncing.set(true);
    this.dgteraSyncService.syncOrders().subscribe({
      next: (result) => {
        this.syncing.set(false);

        if (result.errors.length > 0) {
          // Partial failures still land here with HTTP 200 (see SyncController) - a
          // sync that created/updated some orders but skipped others isn't a hard
          // error, so it gets a longer-lived warning toast instead of the error one.
          this.snackBar.open(
            `Synced with ${result.errors.length} issue${result.errors.length === 1 ? '' : 's'}: ${result.errors[0]}`,
            'Dismiss',
            { duration: 8000 }
          );
        } else {
          this.snackBar.open(
            `Dgtera sync complete: ${result.ordersCreated} created, ${result.ordersUpdated} updated.`,
            'Dismiss',
            { duration: 5000 }
          );
        }

        this.loadOrders();
      },
      error: (err) => {
        this.syncing.set(false);
        const message = err.error?.errors?.[0] ?? 'Failed to sync Dgtera orders.';
        this.snackBar.open(message, 'Dismiss', { duration: 6000 });
      }
    });
  }

  downloadTemplate(): void {
    if (this.downloadingTemplate()) {
      return;
    }

    this.downloadingTemplate.set(true);
    this.orderService.downloadExcelTemplate().subscribe({
      next: (blob) => {
        this.downloadingTemplate.set(false);
        saveAs(blob, 'bulk-order-template.xlsx');
      },
      error: () => {
        this.downloadingTemplate.set(false);
        this.snackBar.open('Failed to download the template.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  triggerFileInput(): void {
    this.fileInput?.nativeElement.click();
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];

    // Reset immediately so re-selecting the same file (e.g. after fixing it and
    // re-uploading) still fires this handler - the native input only emits "change"
    // when its value actually differs from before.
    input.value = '';

    if (!file) {
      return;
    }

    this.uploading.set(true);
    this.orderService.bulkUpload(file).subscribe({
      next: (result) => {
        this.uploading.set(false);

        if (result.errors.length > 0) {
          this.snackBar.open(
            `Imported ${result.ordersCreated} order${result.ordersCreated === 1 ? '' : 's'}, ` +
              `${result.rowsSkipped} row${result.rowsSkipped === 1 ? '' : 's'} skipped: ${result.errors[0]}`,
            'Dismiss',
            { duration: 8000 }
          );
        } else {
          this.snackBar.open(
            `Imported ${result.ordersCreated} order${result.ordersCreated === 1 ? '' : 's'} from ${result.rowsProcessed} row${result.rowsProcessed === 1 ? '' : 's'}.`,
            'Dismiss',
            { duration: 5000 }
          );
        }

        this.loadOrders();
      },
      error: (err) => {
        this.uploading.set(false);
        const message = err.error?.errors?.[0] ?? 'Failed to import the Excel file.';
        this.snackBar.open(message, 'Dismiss', { duration: 6000 });
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

  openNewOrder(): void {
    const dialogRef = this.dialog.open(OrderFormDialogComponent, {
      width: '560px',
      data: { mode: 'create' }
    });

    dialogRef.afterClosed().subscribe((created: Order | undefined) => {
      if (created) {
        this.loadOrders();
      }
    });
  }
}
