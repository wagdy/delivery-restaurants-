import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatDialog } from '@angular/material/dialog';
import { MatSnackBar } from '@angular/material/snack-bar';
import { saveAs } from 'file-saver';
import { CustomerAnalyticsService } from '../../../core/services/customer-analytics.service';
import { CustomerAnalytics, CustomerStatus } from '../../../core/models/customer-analytics.model';
import { tierStyleClass } from '../../../shared/utils/tier-style.util';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { CustomerEditDialogComponent } from '../customer-edit-dialog/customer-edit-dialog.component';
import { RegisterPastCustomerDialogComponent } from '../register-past-customer-dialog/register-past-customer-dialog.component';
import { FindOrCreateCustomerResult } from '../../../core/models/customer-analytics.model';

const PAGE_SIZE = 10;
// A customer is "At Risk" once this many days pass with no order - a plain, documented
// assumption (not derived from any existing business rule) since none was specified.
const AT_RISK_DAYS = 30;
// "High average check" for the VIP classification and the Average Check badge's own
// color - same threshold reused for both, in L.E. Also a documented assumption.
const HIGH_AVERAGE_CHECK = 30;
const MID_AVERAGE_CHECK = 15;
const HIGH_LIFETIME_VALUE = 500;
const MID_LIFETIME_VALUE = 150;

@Component({
  selector: 'app-customer-insights',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatFormFieldModule,
    MatInputModule,
    MatIconModule,
    MatButtonModule,
    MatProgressSpinnerModule,
    MatToolbarModule
  ],
  templateUrl: './customer-insights.component.html',
  styleUrl: './customer-insights.component.scss'
})
export class CustomerInsightsComponent {
  private readonly customerAnalyticsService = inject(CustomerAnalyticsService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly dialog = inject(MatDialog);

  readonly loading = signal(true);
  readonly exporting = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly customers = signal<CustomerAnalytics[]>([]);
  readonly searchTerm = signal('');
  readonly page = signal(1);
  readonly totalCount = signal(0);

  readonly displayedColumns = [
    'fullName',
    'contactInfo',
    'totalPoints',
    'totalOrders',
    'averageCheck',
    'totalLifetimeValue',
    'punchCard',
    'status',
    'actions'
  ];

  readonly totalPages = computed(() => Math.max(1, Math.ceil(this.totalCount() / PAGE_SIZE)));

  // A 5-wide window of page-number buttons centered on the current page, rather than
  // one button per page - renders sanely even with dozens of pages of customers.
  readonly visiblePages = computed(() => {
    const total = this.totalPages();
    const current = this.page();
    const windowSize = Math.min(5, total);
    let start = Math.max(1, current - Math.floor(windowSize / 2));
    const end = Math.min(total, start + windowSize - 1);
    start = Math.max(1, end - windowSize + 1);
    return Array.from({ length: end - start + 1 }, (_, i) => start + i);
  });

  private searchDebounce?: ReturnType<typeof setTimeout>;

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.customerAnalyticsService.getPaged(this.page(), PAGE_SIZE, this.searchTerm().trim() || null).subscribe({
      next: (result) => {
        this.customers.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load customer insights.');
      }
    });
  }

  onSearchChange(term: string): void {
    this.searchTerm.set(term);
    this.page.set(1);
    clearTimeout(this.searchDebounce);
    this.searchDebounce = setTimeout(() => this.load(), 300);
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.page()) {
      return;
    }
    this.page.set(page);
    this.load();
  }

  previousPage(): void {
    this.goToPage(this.page() - 1);
  }

  nextPage(): void {
    this.goToPage(this.page() + 1);
  }

  tierClass(tierName: string): string {
    return tierStyleClass(tierName);
  }

  averageCheckClass(value: number): string {
    if (value >= HIGH_AVERAGE_CHECK) return 'badge-high';
    if (value >= MID_AVERAGE_CHECK) return 'badge-mid';
    return 'badge-low';
  }

  lifetimeValueClass(value: number): string {
    if (value >= HIGH_LIFETIME_VALUE) return 'badge-high';
    if (value >= MID_LIFETIME_VALUE) return 'badge-mid';
    return 'badge-low';
  }

  // VIP: a top-tier loyalty membership (reusing the same keyword-match convention as
  // tierStyleClass) OR a high average check on its own - either signal alone is enough.
  // At Risk: not VIP, and no order in the last AT_RISK_DAYS days (or never ordered).
  // Active: everything else.
  customerStatus(customer: CustomerAnalytics): CustomerStatus {
    const isVip = this.tierClass(customer.membershipTier) === 'tier-vip' || customer.averageCheck >= HIGH_AVERAGE_CHECK;
    if (isVip) {
      return 'VIP';
    }

    if (!customer.lastOrderDate) {
      return 'AtRisk';
    }

    const daysSinceLastOrder = (Date.now() - new Date(customer.lastOrderDate).getTime()) / 86_400_000;
    return daysSinceLastOrder > AT_RISK_DAYS ? 'AtRisk' : 'Active';
  }

  statusLabel(status: CustomerStatus): string {
    return status === 'AtRisk' ? 'At Risk' : status;
  }

  exportToExcel(): void {
    this.exporting.set(true);
    this.customerAnalyticsService.exportToExcel(this.searchTerm().trim() || null).subscribe({
      next: (blob) => {
        this.exporting.set(false);
        saveAs(blob, 'customer-insights.xlsx');
      },
      error: () => {
        this.exporting.set(false);
        this.snackBar.open('Failed to export customer insights.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  // Opens the "Register Past Customer" dialog for a branch visitor who never placed a
  // delivery order (see RegisterPastCustomerDialogComponent/CrmController.RegisterPastCustomer).
  // Reloads the table on success so the new/reactivated customer shows up immediately,
  // rather than trying to splice a full CustomerAnalytics row together client-side from
  // the dialog's minimal FindOrCreateCustomerResult response.
  registerPastCustomer(): void {
    const dialogRef = this.dialog.open(RegisterPastCustomerDialogComponent, {
      width: '420px'
    });

    dialogRef.afterClosed().subscribe((result: FindOrCreateCustomerResult | undefined) => {
      if (!result) {
        return;
      }

      this.load();
      this.snackBar.open('Customer registered and welcomed via WhatsApp.', 'Dismiss', { duration: 4000 });
    });
  }

  editCustomer(customer: CustomerAnalytics): void {
    const dialogRef = this.dialog.open(CustomerEditDialogComponent, {
      width: '420px',
      data: { customer }
    });

    dialogRef.afterClosed().subscribe((updated: CustomerAnalytics | undefined) => {
      if (!updated) {
        return;
      }

      this.customers.set(this.customers().map((c) => (c.id === updated.id ? updated : c)));
      this.snackBar.open('Customer updated.', 'Dismiss', { duration: 3000 });
    });
  }

  // Uses the app's own Material ConfirmDialogComponent (the same one every other delete
  // flow in this admin area already uses - Roles, Survey Questions) rather than a plain
  // browser window.confirm(), so this screen doesn't look out of place next to them.
  deleteCustomer(customer: CustomerAnalytics): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete customer',
        message: `Delete "${customer.fullName}"? Their orders, reviews and loyalty history are kept - they'll just no longer be able to sign in or show up here.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.customerAnalyticsService.delete(customer.id).subscribe({
        next: () => {
          this.customers.set(this.customers().filter((c) => c.id !== customer.id));
          this.totalCount.set(this.totalCount() - 1);
          this.snackBar.open('Customer deleted.', 'Dismiss', { duration: 3000 });
        },
        error: (err) => {
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete customer.', 'Dismiss', { duration: 6000 });
        }
      });
    });
  }
}
