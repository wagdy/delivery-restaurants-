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
import { CustomerService } from '../../../core/services/customer.service';
import { CustomerInsight } from '../../../core/models/customer-insight.model';

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
  private readonly customerService = inject(CustomerService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly customers = signal<CustomerInsight[]>([]);
  readonly searchTerm = signal('');

  readonly displayedColumns = ['customerName', 'phoneNumber', 'totalOrders', 'averageOrderValue'];

  readonly filteredCustomers = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) {
      return this.customers();
    }
    return this.customers().filter(
      (c) =>
        c.customerName.toLowerCase().includes(term) ||
        (c.phoneNumber ?? '').toLowerCase().includes(term)
    );
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.customerService.getInsights().subscribe({
      next: (customers) => {
        this.customers.set(customers);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load customer insights.');
      }
    });
  }
}
