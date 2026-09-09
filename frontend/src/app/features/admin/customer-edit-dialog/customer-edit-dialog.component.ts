import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CustomerAnalyticsService } from '../../../core/services/customer-analytics.service';
import { CustomerAnalytics } from '../../../core/models/customer-analytics.model';

export interface CustomerEditDialogData {
  customer: CustomerAnalytics;
}

@Component({
  selector: 'app-customer-edit-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './customer-edit-dialog.component.html',
  styleUrl: './customer-edit-dialog.component.scss'
})
export class CustomerEditDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly customerAnalyticsService = inject(CustomerAnalyticsService);
  private readonly ref = inject(MatDialogRef<CustomerEditDialogComponent>);
  readonly data: CustomerEditDialogData = inject(MAT_DIALOG_DATA);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    fullName: [this.data.customer.fullName, [Validators.required, Validators.maxLength(200)]],
    phoneNumber: [this.data.customer.phoneNumber ?? '', [Validators.required, Validators.maxLength(20)]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    this.customerAnalyticsService.update(this.data.customer.id, raw).subscribe({
      next: (updated) => {
        this.saving.set(false);
        this.ref.close(updated);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to update customer.');
      }
    });
  }

  cancel(): void {
    this.ref.close();
  }
}
