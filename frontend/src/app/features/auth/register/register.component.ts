import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';

@Component({
  selector: 'app-register',
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
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  // Kept identical to the [RegularExpression] patterns on RegisterRequest
  // (backend/.../DTOs/Auth/RegisterRequest.cs) — client-side validation is only a
  // fast-feedback convenience, the backend re-checks the same rule regardless.
  static readonly NAME_PATTERN = /^[A-Za-z ]+$/;
  static readonly PHONE_PATTERN = /^[0-9]+$/;
  static readonly EMAIL_PATTERN = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    fullName: [
      '',
      [Validators.required, Validators.maxLength(200), Validators.pattern(RegisterComponent.NAME_PATTERN)]
    ],
    email: ['', [Validators.required, Validators.pattern(RegisterComponent.EMAIL_PATTERN)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    // No Validators.required here — phone stays optional; Validators.pattern already
    // skips empty values on its own, so a blank field passes and a filled-in one must match.
    phoneNumber: ['', [Validators.pattern(RegisterComponent.PHONE_PATTERN)]],
    address: ['']
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    const raw = this.form.getRawValue();
    this.authService
      .register({
        fullName: raw.fullName,
        email: raw.email,
        password: raw.password,
        phoneNumber: raw.phoneNumber || undefined,
        address: raw.address || undefined
      })
      .subscribe({
        next: () => {
          this.loading.set(false);
          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
          this.router.navigateByUrl(returnUrl ?? '/');
        },
        error: (err) => {
          this.loading.set(false);
          this.errorMessage.set(err.error?.errors?.[0] ?? 'Registration failed. Please try again.');
        }
      });
  }
}
