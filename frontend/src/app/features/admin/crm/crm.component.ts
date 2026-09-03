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
import { MatChipsModule } from '@angular/material/chips';
import { CrmService } from '../../../core/services/crm.service';
import { CustomerCrm } from '../../../core/models/customer-crm.model';

@Component({
  selector: 'app-crm',
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
    MatToolbarModule,
    MatChipsModule
  ],
  templateUrl: './crm.component.html',
  styleUrl: './crm.component.scss'
})
export class CrmComponent {
  private readonly crmService = inject(CrmService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly customers = signal<CustomerCrm[]>([]);
  readonly searchTerm = signal('');

  readonly displayedColumns = ['fullName', 'phoneNumber', 'totalOrders', 'averageOrderValue', 'membershipTier', 'currentPoints'];

  readonly filteredCustomers = computed(() => {
    const term = this.searchTerm().trim().toLowerCase();
    if (!term) {
      return this.customers();
    }
    return this.customers().filter(
      (c) => c.fullName.toLowerCase().includes(term) || (c.phoneNumber ?? '').toLowerCase().includes(term)
    );
  });

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.crmService.getCustomers().subscribe({
      next: (customers) => {
        this.customers.set(customers);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load customers.');
      }
    });
  }
}
