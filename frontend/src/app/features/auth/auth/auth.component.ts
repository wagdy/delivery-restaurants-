import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, ActivatedRoute, RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatTabsModule } from '@angular/material/tabs';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { AuthService } from '../../../core/services/auth.service';
import { AuthShellComponent } from '../auth-shell/auth-shell.component';
import {
  EGYPT_MOBILE_PATTERN,
  NAME_PATTERN,
  normalizeEgyptMobile
} from '../../../shared/utils/validation-patterns.util';

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
    RouterLink,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatCheckboxModule,
    MatTabsModule,
    MatIconModule,
    MatProgressSpinnerModule,
    AuthShellComponent
  ],
  templateUrl: './auth.component.html',
  styleUrl: './auth.component.scss'
})
export class AuthComponent {
  // The name and phone rules were written out inline here as well as in the checkout
  // form; both now come from shared/utils/validation-patterns.util.ts so the two cannot
  // drift apart. The phone rule used to be spelled /^01[0125][0-9]{8}$/, which matches
  // exactly the same numbers as the shared /^(010|011|012|015)\d{8}$/ - no account that
  // could register yesterday is rejected today.
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

  // Briefly true to play a CSS shake on the card when a submit is rejected (either
  // client-side validation or a server error) - toggled back to false on the shake
  // animation's own (animationend), so a second failed attempt in a row still replays it
  // (re-adding the same class name wouldn't restart a CSS animation on its own).
  readonly loginShake = signal(false);
  readonly registerShake = signal(false);

  readonly loginForm = this.fb.nonNullable.group({
    identifier: ['', [Validators.required]],
    password: ['', [Validators.required]],
    rememberMe: [false]
  });

  readonly registerLoading = signal(false);
  readonly registerError = signal<string | null>(null);

  readonly registerForm = this.fb.nonNullable.group({
    fullName: ['', [Validators.required, Validators.maxLength(200), Validators.pattern(NAME_PATTERN)]],
    phoneNumber: ['', [Validators.required, Validators.pattern(EGYPT_MOBILE_PATTERN)]],
    password: ['', [Validators.required, Validators.minLength(8)]],
    address: ['']
  });

  // Same treatment as the checkout form: non-digits are stripped as they are typed and
  // "+20 101 234 5678" pasted from a WhatsApp contact becomes 01012345678, rather than
  // being reported as "not an Egyptian mobile" when it plainly is one.
  protected onPhoneInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    const cleaned = normalizeEgyptMobile(input.value);

    if (cleaned !== input.value) {
      input.value = cleaned;
      // Written back through the control, not just the DOM, so the form's validity and
      // what is on screen never disagree.
      this.registerForm.controls.phoneNumber.setValue(cleaned);
    }
  }

  submitLogin(): void {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      this.loginShake.set(true);
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
        this.loginShake.set(true);
      }
    });
  }

  submitRegister(): void {
    if (this.registerForm.invalid) {
      this.registerForm.markAllAsTouched();
      this.registerShake.set(true);
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
          this.registerShake.set(true);
        }
      });
  }
}
