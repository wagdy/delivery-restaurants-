import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { AuthService } from '../../../core/services/auth.service';

// Email-based login, kept for the two pre-existing seeded staff accounts
// (admin@restaurant.com / captain@restaurant.com) and any legacy account that predates
// the switch to phone-based login — see LoginComponent (route: /login) for the primary,
// phone-based flow that customers and newly-created staff use instead.
@Component({
  selector: 'app-email-login',
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
  templateUrl: './email-login.component.html',
  styleUrl: './email-login.component.scss'
})
export class EmailLoginComponent {
  // Kept identical to the [RegularExpression] pattern on LoginRequest
  // (backend/.../DTOs/Auth/LoginRequest.cs) — client-side validation is only a fast-feedback
  // convenience, the backend re-checks the same rule regardless. Stricter than
  // Validators.email, which allows domains with no TLD (e.g. "user@localhost").
  static readonly EMAIL_PATTERN = /^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$/;

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    email: ['', [Validators.required, Validators.pattern(EmailLoginComponent.EMAIL_PATTERN)]],
    password: ['', [Validators.required]]
  });

  submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
        this.router.navigateByUrl(returnUrl ?? '/');
      },
      error: (err) => {
        this.loading.set(false);
        this.errorMessage.set(err.error?.errors?.[0] ?? 'Login failed. Please try again.');
      }
    });
  }
}
