import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatTabsModule } from '@angular/material/tabs';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';

// Unified customer-facing sign-in/sign-up page - replaces the old separate
// LoginComponent (phone-only) and RegisterComponent at the same /login and /register
// routes (see app.routes.ts's `initialTab` route data for which tab each one opens to).
// Deliberately has no "sign in as cashier/admin" link anywhere - staff who need the
// email-based flow still have /email-login, it's just no longer surfaced here.
@Component({
  selector: 'app-auth',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatTabsModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './auth.component.html',
  styleUrl: './auth.component.scss'
})
export class AuthComponent {
  static readonly NAME_PATTERN = /^[A-Za-z ]+$/;
  static readonly PHONE_PATTERN = /^[0-9]+$/;

  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly snackBar = inject(MatSnackBar);

  // 0 = Login, 1 = Register - which tab opens first depends on which route led here
  // (/login vs /register), see app.routes.ts.
  readonly activeTabIndex = signal(this.route.snapshot.data['initialTab'] === 'register' ? 1 : 0);

  readonly loginLoading = signal(false);
  readonly loginError = signal<string | null>(null);

  readonly loginForm = this.fb.nonNullable.group({
    identifier: ['', [Validators.required]],
    password: ['', [Validators.required]]
  });

  readonly registerLoading = signal(false);
  readonly registerError = signal<string | null>(null);

  readonly registerForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(AuthComponent.NAME_PATTERN)]],
    phoneNumber: ['', [Validators.required, Validators.pattern(AuthComponent.PHONE_PATTERN)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    address: ['']
  });

  submitLogin(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.loginLoading.set(true);
    this.loginError.set(null);

    this.authService.login(this.loginForm.getRawValue()).subscribe({
      next: (res) => {
        this.loginLoading.set(false);
        const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');

        if (res.user.role === 'CaptainOrder') {
          this.router.navigateByUrl('/captain');
        } else if (res.user.role === 'Admin') {
          this.router.navigateByUrl(returnUrl ?? '/admin');
        } else {
          this.router.navigateByUrl(returnUrl ?? '/');
        }
      },
      error: (err) => {
        this.loginLoading.set(false);
        this.loginError.set(err.error?.errors?.[0] ?? 'Login failed. Please try again.');
      }
    });
  }

  submitRegister(): void {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      return;
    }

    this.registerLoading.set(true);
    this.registerError.set(null);

    const raw = this.registerForm.getRawValue();
    this.authService
      .register({
        fullName: raw.fullName,
        phoneNumber: raw.phoneNumber,
        password: raw.password,
        address: raw.address || undefined
      })
      .subscribe({
        next: () => {
          this.registerLoading.set(false);

          // The account is already fully signed in at this point - AuthService.register()
          // applies the returned token/session the same way login() does - so this is
          // purely a celebratory notice, not a "please verify" or "please sign in" prompt.
          this.snackBar.open('Welcome! You have received 100 bonus points to get started!', 'Dismiss', {
            duration: 6000
          });

          const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
          this.router.navigateByUrl(returnUrl ?? '/');
        },
        error: (err) => {
          this.registerLoading.set(false);
          this.registerError.set(err.error?.errors?.[0] ?? 'Registration failed. Please try again.');
        }
      });
  }
}
