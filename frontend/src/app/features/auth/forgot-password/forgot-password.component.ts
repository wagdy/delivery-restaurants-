import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';
import { AuthShellComponent } from '../auth-shell/auth-shell.component';

// A two-step OTP reset flow reached from AuthComponent's Login tab. Step 1 requests a
// WhatsApp code for a phone number; Step 2 verifies it and sets a new password. Advancing
// to step 2 never depends on whether the phone number actually exists - the backend always
// returns the same generic response either way (see AuthService.forgotPassword), so this
// component can't be used to probe which phone numbers are registered.
@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AuthShellComponent
  ],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent {
  // Matches AuthComponent's own PHONE_PATTERN (and the backend's [RegularExpression] on
  // RegisterRequest.cs) - this field never had it, so an obviously-malformed number only
  // surfaced as a generic "failed to send the code" after a round trip. Fast client-side
  // feedback only; ForgotPasswordAsync itself never reveals whether a number is registered.
  static readonly PHONE_PATTERN = /^01[0125][0-9]{8}$/;

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly snackBar = inject(MatSnackBar);

  // 1 = request a code, 2 = enter the code + a new password.
  readonly step = signal<1 | 2>(1);

  readonly requestLoading = signal(false);
  readonly requestError = signal<string | null>(null);

  // Briefly true to play a CSS shake on the card when a submit is rejected - see
  // AuthComponent's identical loginShake/registerShake for why this needs a signal
  // round-trip via the template's (animationend) rather than a one-off class toggle.
  readonly requestShake = signal(false);
  readonly resetShake = signal(false);

  readonly requestForm = this.fb.nonNullable.group({
    phone: ['', [Validators.required, Validators.pattern(ForgotPasswordComponent.PHONE_PATTERN)]]
  });

  readonly resetLoading = signal(false);
  readonly resetError = signal<string | null>(null);

  readonly resetForm = this.fb.nonNullable.group({
    otp: ['', [Validators.required, Validators.pattern(/^\d{6}$/)]],
    newPassword: ['', [Validators.required, Validators.minLength(8)]]
  });

  submitRequest(): void {
    if (this.requestForm.invalid) {
      this.requestForm.markAllAsTouched();
      this.requestShake.set(true);
      return;
    }

    this.requestLoading.set(true);
    this.requestError.set(null);

    this.authService.forgotPassword({ phoneNumber: this.requestForm.getRawValue().phone }).subscribe({
      next: () => {
        this.requestLoading.set(false);
        this.step.set(2);
      },
      error: (err) => {
        this.requestLoading.set(false);
        this.requestError.set(err.error?.errors?.[0] ?? 'Failed to send the code. Please try again.');
        this.requestShake.set(true);
      }
    });
  }

  backToStep1(): void {
    this.step.set(1);
    this.resetError.set(null);
    this.resetForm.reset({ otp: '', newPassword: '' });
  }

  submitReset(): void {
    if (this.resetForm.invalid) {
      this.resetForm.markAllAsTouched();
      this.resetShake.set(true);
      return;
    }

    this.resetLoading.set(true);
    this.resetError.set(null);

    const raw = this.resetForm.getRawValue();
    this.authService
      .resetPassword({
        phoneNumber: this.requestForm.getRawValue().phone,
        otp: raw.otp,
        newPassword: raw.newPassword
      })
      .subscribe({
        next: () => {
          this.resetLoading.set(false);
          this.snackBar.open('Password reset successfully. Please sign in.', 'Dismiss', { duration: 5000 });
          this.router.navigateByUrl('/login');
        },
        error: (err) => {
          this.resetLoading.set(false);
          this.resetError.set(err.error?.errors?.[0] ?? 'Failed to reset your password. Please try again.');
          this.resetShake.set(true);
        }
      });
  }
}
