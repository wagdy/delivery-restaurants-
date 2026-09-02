import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-staff-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './staff-login.component.html',
  styleUrl: './staff-login.component.scss'
})
export class StaffLoginComponent {
  static readonly PHONE_PATTERN = /^[0-9]+$/;

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    phoneNumber: ['', [Validators.required, Validators.pattern(StaffLoginComponent.PHONE_PATTERN)]],
    password: ['', [Validators.required]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.loginStaff(this.form.getRawValue()).subscribe({
      next: (res) => {
        this.loading.set(false);
        this.router.navigateByUrl(res.user.role === 'CaptainOrder' ? '/captain' : '/admin');
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Login failed. Please try again.');
      }
    });
  }
}
