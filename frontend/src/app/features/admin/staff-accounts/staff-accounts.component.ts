import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';
import { UserRole } from '../../../core/models/user.model';

@Component({
  selector: 'app-staff-accounts',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './staff-accounts.component.html',
  styleUrl: './staff-accounts.component.scss'
})
export class StaffAccountsComponent {
  // Kept identical to the [RegularExpression] patterns on CreateStaffUserRequest
  // (backend/.../DTOs/Auth/CreateStaffUserRequest.cs) — client-side validation is only a
  // fast-feedback convenience, the backend re-checks the same rule regardless.
  static readonly NAME_PATTERN = /^[A-Za-z ]+$/;
  static readonly PHONE_PATTERN = /^[0-9]+$/;
  static readonly EMAIL_PATTERN = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly snackBar = inject(MatSnackBar);

  readonly saving = signal(false);
  readonly errorMessage = signal<string | null>(null);

  // Customer is deliberately excluded — that role is always self-registered via the
  // public storefront, never provisioned by an admin.
  readonly roleOptions: { value: UserRole; label: string }[] = [
    { value: 'Admin', label: 'Admin' },
    { value: 'CaptainOrder', label: 'Captain Order (Delivery Driver)' }
  ];

  readonly form = this.fb.nonNullable.group({
    fullName: [
      '',
      [Validators.required, Validators.maxLength(200), Validators.pattern(StaffAccountsComponent.NAME_PATTERN)]
    ],
    email: ['', [Validators.required, Validators.pattern(StaffAccountsComponent.EMAIL_PATTERN)]],
    // No Validators.required here — phone stays optional; Validators.pattern already
    // skips empty values on its own, so a blank field passes and a filled-in one must match.
    phoneNumber: ['', [Validators.pattern(StaffAccountsComponent.PHONE_PATTERN)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    role: ['CaptainOrder' as UserRole, [Validators.required]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();

    this.authService
      .createStaffUser({
        fullName: raw.fullName,
        email: raw.email,
        phoneNumber: raw.phoneNumber || undefined,
        password: raw.password,
        role: raw.role
      })
      .subscribe({
        next: (user) => {
          this.saving.set(false);
          this.snackBar.open(`${user.fullName} created as ${user.role}.`, 'Dismiss', { duration: 4000 });
          this.form.reset({ role: 'CaptainOrder' as UserRole });
        },
        error: (err) => {
          this.saving.set(false);
          this.errorMessage.set(err.error?.errors?.[0] ?? 'Failed to create account.');
        }
      });
  }
}
