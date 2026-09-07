import { Component, DestroyRef, ElementRef, HostListener, OnInit, ViewChild, computed, effect, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormArray, FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { startWith } from 'rxjs';
import { saveAs } from 'file-saver';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { OrderService } from '../../../core/services/order.service';
import { MenuItemService } from '../../../core/services/menu-item.service';
import { DgteraSyncService } from '../../../core/services/dgtera-sync.service';
import { OrderRealtimeService } from '../../../core/services/order-realtime.service';
import { AuthService } from '../../../core/services/auth.service';
import { SettingsService } from '../../../core/services/settings.service';
import { CheckoutService } from '../../../core/services/checkout.service';
import { MenuItem } from '../../../core/models/menu-item.model';
import { CreateOrderRequest, CustomerLookup, ORDER_STATUSES, Order, OrderStatus } from '../../../core/models/order.model';
import { ValidatePromoResponse } from '../../../core/models/checkout.model';
import { OrderDetailsDialogComponent } from '../order-details-dialog/order-details-dialog.component';

interface ReviewLine {
  menuItemId: number;
  name: string;
  quantity: number;
  lineTotal: number;
}

type DashboardTab = 'create' | 'all' | 'active' | 'reports';
type CustomerMode = 'new' | 'registered';

// Maps each tab to the granular sub-permission that gates it (see the "Manage Roles"
// nested checkboxes under the Orders module) - checked via AuthService.hasPermission's
// "no recorded restriction = full access" rule, so a role with the Orders module but no
// narrowed sub-permissions still sees every tab.
const TAB_PERMISSIONS: Record<DashboardTab, string> = {
  create: 'Orders.Create',
  all: 'Orders.AllOrders',
  active: 'Orders.ActiveStatus',
  reports: 'Orders.Reports'
};
const TAB_ORDER: DashboardTab[] = ['create', 'all', 'active', 'reports'];

function firstAccessibleTab(authService: AuthService): DashboardTab {
  return TAB_ORDER.find((tab) => authService.hasPermission(TAB_PERMISSIONS[tab])) ?? 'active';
}

// How often the Active Status board's "time elapsed" labels refresh themselves without
// any user interaction - short enough to feel live on an ops screen, long enough not to
// force pointless change-detection churn on a page that's often just sitting open.
const TIME_ELAPSED_TICK_MS = 30_000;

@Component({
  selector: 'app-admin-dashboard',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatProgressSpinnerModule,
    MatToolbarModule
  ],
  templateUrl: './admin-dashboard.component.html',
  styleUrl: './admin-dashboard.component.scss'
})
export class AdminDashboardComponent implements OnInit {
  private readonly orderService = inject(OrderService);
  private readonly menuItemService = inject(MenuItemService);
  private readonly dgteraSyncService = inject(DgteraSyncService);
  private readonly orderRealtimeService = inject(OrderRealtimeService);
  private readonly authService = inject(AuthService);
  private readonly settingsService = inject(SettingsService);
  private readonly checkoutService = inject(CheckoutService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly destroyRef = inject(DestroyRef);
  private readonly fb = inject(FormBuilder);

  @ViewChild('fileInput') private readonly fileInput?: ElementRef<HTMLInputElement>;

  readonly activeTab = signal<DashboardTab>(firstAccessibleTab(this.authService));
  // The Active Status board shows all 5 statuses side by side (Delivered/Cancelled
  // included) so an admin can see an order's full lifecycle at a glance - the All Orders
  // tab covers the same data as a searchable flat history table instead.
  readonly statuses = ORDER_STATUSES;

  // Ticks forward on its own (see the constructor) purely to keep columnOrders' cards'
  // "time elapsed" labels fresh - nothing else reads this signal.
  readonly now = signal(Date.now());

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

  // --- Tab 1: Create Order (admin POS) ---

  readonly menuItems = signal<MenuItem[]>([]);
  readonly customerMode = signal<CustomerMode>('new');
  readonly customerSearchPhone = signal('');
  readonly searchingCustomer = signal(false);
  readonly customerSearchError = signal<string | null>(null);
  readonly selectedCustomer = signal<CustomerLookup | null>(null);
  readonly savingOrder = signal(false);
  readonly createOrderError = signal<string | null>(null);

  // Manual staff-entered orders (phone-in, walk-in) skip delivery entirely - a single
  // shared constant instead of a magic literal wherever the fee is read or submitted.
  private static readonly ADMIN_ORDER_DELIVERY_FEE = 0;

  // Recalculated from scratch on every FormArray change (add/remove/quantity/item-select
  // all funnel through this one valueChanges subscription - see the constructor) rather
  // than trusting the array's rendered DOM state, which is what let the two states drift
  // out of sync in the first place (see the @for track fix on createItems.controls below).
  readonly createItemsSubtotal = signal(0);

  readonly reviewing = signal(false);
  readonly promoCodeInput = signal('');
  readonly promoResult = signal<ValidatePromoResponse | null>(null);
  readonly promoError = signal<string | null>(null);
  readonly applyingPromo = signal(false);

  readonly createForm = this.fb.nonNullable.group({
    customerName: ['', [Validators.required, Validators.maxLength(200)]],
    customerPhone: ['', [Validators.required, Validators.maxLength(30)]],
    deliveryAddress: ['', [Validators.required, Validators.maxLength(500)]],
    items: this.fb.array<ReturnType<typeof this.buildCreateItemGroup>>([])
  });

  get createItems(): FormArray {
    return this.createForm.controls.items;
  }

  constructor() {
    this.loadOrders();
    this.menuItemService.getAll().subscribe({ next: (items) => this.menuItems.set(items) });
    this.createItems.push(this.buildCreateItemGroup());

    // Fixes the stale-total bug: FormArray.valueChanges fires on push()/removeAt() and on
    // every keystroke in a quantity/item field alike, so recomputing here - strictly from
    // the array's own current value, never from anything the view happens to be showing -
    // is what actually guarantees the total is never one step behind a deletion.
    this.createItems.valueChanges
      .pipe(startWith(this.createItems.value), takeUntilDestroyed(this.destroyRef))
      .subscribe((items: { menuItemId: number | null; quantity: number }[]) => {
        const total = items.reduce((sum, item) => sum + this.createItemPrice(item.menuItemId) * (item.quantity || 0), 0);
        this.createItemsSubtotal.set(Math.round(total * 100) / 100);
      });

    // Resumes the alarm the instant audio unlocks, if orders were already waiting -
    // covers the case where a new order arrives before the cashier's first click.
    effect(() => {
      if (this.audioUnlocked() && this.unacknowledgedOrderIds().size > 0) {
        this.alarmAudio.play().catch(() => {});
      }
    });

    const timeElapsedTick = setInterval(() => this.now.set(Date.now()), TIME_ELAPSED_TICK_MS);
    this.destroyRef.onDestroy(() => clearInterval(timeElapsedTick));
  }

  ngOnInit(): void {
    this.alarmAudio.loop = true;

    // Foreground-recovery signal from OrderRealtimeService - fires when the cashier
    // unlocks the phone or switches back to this tab after the connection may have been
    // dropped by mobile OS background limits. Refetches instead of trusting SignalR alone
    // to have delivered every event that happened while backgrounded.
    this.orderRealtimeService.connectionRestored.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.loadOrders();
    });

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

      // An order an admin entered themselves via Create Order never rings the alarm or
      // needs acknowledgment, on ANY connected dashboard (not just the one that created
      // it) - see NewOrderNotification.isStaffCreated's own doc comment. It still gets
      // the instant list update above, everywhere.
      if (notification.isStaffCreated) {
        return;
      }

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

  // Reads this.now() (see the constructor's tick interval) so this keeps advancing on
  // its own while the board is left open, instead of freezing at whatever it read when
  // the order first arrived.
  timeElapsed(order: Order): string {
    const elapsedMs = this.now() - new Date(order.createdAt).getTime();
    const minutes = Math.floor(elapsedMs / 60_000);

    if (minutes < 1) {
      return 'Just now';
    }
    if (minutes < 60) {
      return `${minutes}m ago`;
    }

    const hours = Math.floor(minutes / 60);
    if (hours < 24) {
      return `${hours}h ${minutes % 60}m ago`;
    }

    const days = Math.floor(hours / 24);
    return `${days}d ago`;
  }

  // The Active Status board's quick-action dropdown - moves the card to its new column
  // the instant the API confirms the change, without waiting on a full loadOrders().
  updateOrderStatus(orderId: number, newStatus: OrderStatus): void {
    const current = this.orders().find((o) => o.id === orderId);
    if (!current || current.status === newStatus) {
      return;
    }

    this.orderService.updateStatus(orderId, newStatus).subscribe({
      next: (updated) => {
        this.orders.update((all) => all.map((o) => (o.id === updated.id ? updated : o)));
      },
      error: () => {
        this.snackBar.open('Failed to update order status.', 'Dismiss', { duration: 4000 });
      }
    });
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

  canAccessTab(tab: DashboardTab): boolean {
    return this.authService.hasPermission(TAB_PERMISSIONS[tab]);
  }

  // The tab buttons are already hidden via @if(canAccessTab(...)) in the template - this
  // is a defensive second check so a programmatic switch (e.g. submitCreateOrder jumping
  // to 'all' after saving) can't land on a tab this admin's role doesn't have permission
  // for.
  switchTab(tab: DashboardTab): void {
    if (this.canAccessTab(tab)) {
      this.activeTab.set(tab);
    }
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

  // --- Tab 1: Create Order methods ---

  setCustomerMode(mode: CustomerMode): void {
    this.customerMode.set(mode);
    this.selectedCustomer.set(null);
    this.customerSearchError.set(null);
    this.customerSearchPhone.set('');
    this.createForm.patchValue({ customerName: '', customerPhone: '', deliveryAddress: '' });
  }

  searchCustomerByPhone(): void {
    const phone = this.customerSearchPhone().trim();
    if (!phone) {
      return;
    }

    this.searchingCustomer.set(true);
    this.customerSearchError.set(null);
    this.selectedCustomer.set(null);

    this.orderService.lookupCustomerByPhone(phone).subscribe({
      next: (customer) => {
        this.searchingCustomer.set(false);
        this.selectedCustomer.set(customer);
        this.createForm.patchValue({
          customerName: customer.fullName,
          customerPhone: customer.phoneNumber,
          deliveryAddress: customer.address ?? ''
        });
      },
      error: (err) => {
        this.searchingCustomer.set(false);
        this.customerSearchError.set(
          err.status === 404 ? 'No registered customer found with that phone number.' : 'Failed to search for customer.'
        );
      }
    });
  }

  private buildCreateItemGroup(menuItemId: number | null = null, quantity = 1) {
    return this.fb.nonNullable.group({
      menuItemId: [menuItemId, [Validators.required]],
      quantity: [quantity, [Validators.required, Validators.min(1), Validators.max(100)]]
    });
  }

  addCreateItem(): void {
    this.createItems.push(this.buildCreateItemGroup());
  }

  removeCreateItem(index: number): void {
    this.createItems.removeAt(index);
  }

  createItemPrice(menuItemId: number | null): number {
    return this.menuItems().find((m) => m.id === menuItemId)?.price ?? 0;
  }

  // --- Review Order ---

  // A plain method, not a computed signal - the create form is a ReactiveForm (not
  // signal-backed), so this re-evaluates on Angular's normal change-detection cycle,
  // matching this file's own established pattern (columnOrders/timeElapsed above). Safe to
  // read live even while the review section is open, since nothing here is a one-time
  // snapshot - if the cashier edits an item while reviewing, this reflects it immediately.
  reviewLines(): ReviewLine[] {
    return this.createItems.controls
      .map((group) => group.getRawValue() as { menuItemId: number | null; quantity: number })
      .filter((value): value is { menuItemId: number; quantity: number } => value.menuItemId !== null)
      .map((value) => ({
        menuItemId: value.menuItemId,
        name: this.menuItems().find((m) => m.id === value.menuItemId)?.name ?? 'Unknown item',
        quantity: value.quantity,
        lineTotal: this.createItemPrice(value.menuItemId) * value.quantity
      }));
  }

  reviewDeliveryFee(): number {
    return AdminDashboardComponent.ADMIN_ORDER_DELIVERY_FEE;
  }

  // Falls back to a plain client-side estimate (no discount) until a promo is applied -
  // same convention as CheckoutComponent.taxAmount, so the summary always shows a real
  // number without waiting on a network round trip just to render the tax line.
  reviewTaxAmount(): number {
    const promo = this.promoResult();
    if (promo) {
      return promo.taxAmount;
    }
    return Math.round(this.createItemsSubtotal() * (this.settingsService.settings().taxPercentage / 100) * 100) / 100;
  }

  discountAmount(): number {
    return this.promoResult()?.discountAmount ?? 0;
  }

  reviewFinalTotal(): number {
    const promo = this.promoResult();
    if (promo) {
      return promo.total;
    }
    return this.createItemsSubtotal() + this.reviewTaxAmount() + this.reviewDeliveryFee();
  }

  // Validates the form exactly like submitCreateOrder used to on its own submit button -
  // Review Order is now the gate, Place Order (inside the review section) trusts it was
  // already checked here and re-checks defensively rather than redundantly.
  openReview(): void {
    if (this.reviewing()) {
      return;
    }

    if (this.createForm.invalid || this.createItems.length === 0) {
      this.createForm.markAllAsTouched();
      if (this.createItems.length === 0) {
        this.createOrderError.set('Add at least one item.');
      }
      return;
    }

    this.createOrderError.set(null);
    this.reviewing.set(true);
  }

  closeReview(): void {
    this.reviewing.set(false);
  }

  applyPromoCode(): void {
    const codeText = this.promoCodeInput().trim();
    const lines = this.reviewLines();
    if (!codeText || lines.length === 0) {
      return;
    }

    this.applyingPromo.set(true);
    this.promoError.set(null);

    this.checkoutService
      .validatePromo({
        codeText,
        deliveryFee: this.reviewDeliveryFee(),
        items: lines.map((l) => ({ menuItemId: l.menuItemId, quantity: l.quantity, addOnIds: [] }))
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

  submitCreateOrder(): void {
    if (this.createForm.invalid || this.createItems.length === 0) {
      this.createForm.markAllAsTouched();
      if (this.createItems.length === 0) {
        this.createOrderError.set('Add at least one item.');
      }
      return;
    }

    this.savingOrder.set(true);
    this.createOrderError.set(null);

    const raw = this.createForm.getRawValue();
    // Manual staff-entered orders (phone-in, walk-in) skip the customer checkout flow's
    // payment-method selection - Cash, matching this dashboard's pre-existing manual
    // order-creation behavior (see the now-retired OrderFormDialogComponent create mode).
    // Promo code text IS now forwarded, though - re-validated server-side the same way the
    // customer flow's is, never trusted at face value for the discount amount.
    const request: CreateOrderRequest = {
      customerName: raw.customerName,
      customerPhone: raw.customerPhone,
      deliveryAddress: raw.deliveryAddress,
      items: raw.items.map((i) => ({ menuItemId: i.menuItemId!, quantity: i.quantity, addOnIds: [] })),
      paymentMethod: 'Cash',
      promoCodeText: this.promoResult() ? this.promoCodeInput().trim() : null,
      deliveryFee: AdminDashboardComponent.ADMIN_ORDER_DELIVERY_FEE,
      customerId: this.customerMode() === 'registered' ? (this.selectedCustomer()?.id ?? null) : null
    };

    this.orderService.create(request).subscribe({
      next: (order) => {
        this.savingOrder.set(false);
        this.snackBar.open(`Order #${order.id} created.`, 'Dismiss', { duration: 4000 });
        this.resetCreateForm();
        this.loadOrders();
        this.switchTab('all');
      },
      error: (err) => {
        this.savingOrder.set(false);
        this.createOrderError.set(err.error?.errors?.[0] ?? 'Failed to create order.');
      }
    });
  }

  private resetCreateForm(): void {
    this.createForm.reset({ customerName: '', customerPhone: '', deliveryAddress: '' });
    this.createItems.clear();
    this.createItems.push(this.buildCreateItemGroup());
    this.selectedCustomer.set(null);
    this.customerSearchPhone.set('');
    this.customerSearchError.set(null);
    this.customerMode.set('new');
    this.reviewing.set(false);
    this.removePromoCode();
  }
}
