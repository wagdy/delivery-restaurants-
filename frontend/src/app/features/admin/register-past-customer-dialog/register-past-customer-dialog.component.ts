import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { CustomerAnalyticsService } from '../../../core/services/customer-analytics.service';
import { EGYPT_MOBILE_PATTERN, NAME_PATTERN } from '../../../shared/utils/validation-patterns.util';
import { FindOrCreateCustomerResult } from '../../../core/models/customer-analytics.model';

// A branch customer who visited in person but never placed a delivery order - registers
// them into the loyalty program from Customer Insights (see CrmController.RegisterPastCustomer),
// distinct from the Scanner's own "New Customer" tab which serves a different admin module.
@Component({
  selector: 'app-register-past-customer-dialog',
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
  templateUrl: './register-past-customer-dialog.component.html',
  styleUrl: './register-past-customer-dialog.component.scss'
})
export class RegisterPastCustomerDialogComponent {
  // Same rules as the self-service registration form, imported from
  // shared/utils/validation-patterns.util.ts rather than written out again - see the note
  // there on why Arabic + English letters and the 010/011/012/015 Egyptian mobile
  // prefixes are the accepted shapes. The phone rule was previously spelled
  // /^01[0125][0-9]{8}$/ here, which matches exactly the same numbers.

  private readonly fb = inject(FormBuilder);
  private readonly customerAnalyticsService = inject(CustomerAnalyticsService);
  private readonly ref = inject(MatDialogRef<RegisterPastCustomerDialogComponent>);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    customerName: [
      '',
      [Validators.required, Validators.maxLength(200), Validators.pattern(NAME_PATTERN)]
    ],
    phone: ['', [Validators.required, Validators.pattern(EGYPT_MOBILE_PATTERN)]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    this.customerAnalyticsService.registerPastCustomer(this.form.getRawValue()).subscribe({
      next: (result: FindOrCreateCustomerResult) => {
        this.saving.set(false);
        this.ref.close(result);
      },
      error: (err) => {
        this.saving.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to register customer.');
      }
    });
  }

  cancel(): void {
    this.ref.close();
  }
}
