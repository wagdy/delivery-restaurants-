import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SettingsService } from '../../../core/services/settings.service';
import { AuthService } from '../../../core/services/auth.service';

const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif', 'image/svg+xml'];
// Deliberately narrower than ALLOWED_IMAGE_TYPES above (no WEBP/GIF/SVG) - a favicon
// needs to be a format every browser's tab bar renders reliably at 16-32px, which is
// exactly .ico/.png/.jpg, matching this input's own [accept] attribute in the template.
const ALLOWED_FAVICON_TYPES = ['image/png', 'image/x-icon', 'image/jpeg'];
const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;
const HEX_COLOR_PATTERN = /^#[0-9A-Fa-f]{6}$/;

type SettingsCategory = 'branding' | 'contact' | 'checkout' | 'payment';

// Maps each settings category to the granular sub-permission that gates it (see the
// "Manage Roles" nested checkboxes under the Settings module) - checked via
// AuthService.hasPermission's "no recorded restriction = full access" rule, so a role
// with the Settings module but no narrowed sub-permissions still sees every category.
const CATEGORY_PERMISSIONS: Record<SettingsCategory, string> = {
  branding: 'Settings.Branding',
  contact: 'Settings.Contact',
  checkout: 'Settings.Checkout',
  payment: 'Settings.Payment'
};
const CATEGORY_ORDER: SettingsCategory[] = ['branding', 'contact', 'checkout', 'payment'];

function firstAccessibleCategory(authService: AuthService): SettingsCategory {
  return CATEGORY_ORDER.find((category) => authService.hasPermission(CATEGORY_PERMISSIONS[category])) ?? 'branding';
}

@Component({
  selector: 'app-site-settings',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule
  ],
  templateUrl: './site-settings.component.html',
  styleUrl: './site-settings.component.scss'
})
export class SiteSettingsComponent {
  protected readonly settingsService = inject(SettingsService);
  private readonly authService = inject(AuthService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly activeCategory = signal<SettingsCategory>(firstAccessibleCategory(this.authService));

  readonly uploadingLogo = signal(false);
  readonly uploadingBackgroundImage = signal(false);
  readonly uploadingCenterLogo = signal(false);
  readonly uploadingFavicon = signal(false);
  readonly uploadError = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    restaurantName: ['', [Validators.maxLength(200)]],
    logoUrl: [''],
    primaryColor: ['#3f51b5', [Validators.required, Validators.pattern(HEX_COLOR_PATTERN)]],
    accentColor: ['#ff4081', [Validators.required, Validators.pattern(HEX_COLOR_PATTERN)]],
    headerColor: ['#3f51b5', [Validators.required, Validators.pattern(HEX_COLOR_PATTERN)]],
    bodyColor: ['#fafafa', [Validators.required, Validators.pattern(HEX_COLOR_PATTERN)]],
    backgroundImageUrl: [''],
    centerLogoUrl: [''],
    address: [''],
    phone: [''],
    email: ['', [Validators.email]],
    footerAbout: [''],
    faviconUrl: [''],
    tabTitle: ['', [Validators.maxLength(100)]],
    taxPercentage: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
    baseDeliveryFee: [0, [Validators.required, Validators.min(0)]],
    managerPhoneNumber: ['', [Validators.maxLength(30)]],
    isCashEnabled: [true],
    isVisaEnabled: [false],
    visaFawryUrl: [''],
    isInstapayEnabled: [false],
    instapayAccount: ['']
  });

  constructor() {
    // isVisaEnabled/isInstapayEnabled toggling on or off flips whether their paired
    // text field is required - re-evaluated live rather than only at submit time, so
    // the mat-error appears/disappears the instant an admin flips the switch.
    this.form.controls.isVisaEnabled.valueChanges.subscribe(() => this.updateConditionalValidators());
    this.form.controls.isInstapayEnabled.valueChanges.subscribe(() => this.updateConditionalValidators());

    this.settingsService.load().subscribe({
      next: (settings) => {
        this.form.patchValue({
          restaurantName: settings.restaurantName ?? '',
          logoUrl: settings.logoUrl ?? '',
          primaryColor: settings.primaryColor,
          accentColor: settings.accentColor,
          headerColor: settings.headerColor,
          bodyColor: settings.bodyColor,
          backgroundImageUrl: settings.backgroundImageUrl ?? '',
          centerLogoUrl: settings.centerLogoUrl ?? '',
          address: settings.address ?? '',
          phone: settings.phone ?? '',
          email: settings.email ?? '',
          footerAbout: settings.footerAbout ?? '',
          faviconUrl: settings.faviconUrl ?? '',
          tabTitle: settings.tabTitle ?? '',
          taxPercentage: settings.taxPercentage,
          baseDeliveryFee: settings.baseDeliveryFee,
          managerPhoneNumber: settings.managerPhoneNumber ?? '',
          isCashEnabled: settings.isCashEnabled,
          isVisaEnabled: settings.isVisaEnabled,
          visaFawryUrl: settings.visaFawryUrl ?? '',
          isInstapayEnabled: settings.isInstapayEnabled,
          instapayAccount: settings.instapayAccount ?? ''
        });
        this.updateConditionalValidators();
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load settings.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  canAccessCategory(category: SettingsCategory): boolean {
    return this.authService.hasPermission(CATEGORY_PERMISSIONS[category]);
  }

  // The nav buttons are already hidden via @if(canAccessCategory(...)) in the template -
  // this is a defensive second check against any future programmatic switch.
  switchCategory(category: SettingsCategory): void {
    if (this.canAccessCategory(category)) {
      this.activeCategory.set(category);
    }
  }

  // A single [Required] attribute can't express "required only while the matching
  // toggle is on" the way TierRequest/PromoCodeRequest's IValidatableObject does
  // server-side - toggled here client-side instead, kept as the single source of truth
  // for both fields rather than duplicating the condition at every call site.
  private updateConditionalValidators(): void {
    const visaUrl = this.form.controls.visaFawryUrl;
    if (this.form.controls.isVisaEnabled.value) {
      visaUrl.setValidators([Validators.required]);
    } else {
      visaUrl.clearValidators();
    }
    visaUrl.updateValueAndValidity({ emitEvent: false });

    const instapayAccount = this.form.controls.instapayAccount;
    if (this.form.controls.isInstapayEnabled.value) {
      instapayAccount.setValidators([Validators.required]);
    } else {
      instapayAccount.clearValidators();
    }
    instapayAccount.updateValueAndValidity({ emitEvent: false });
  }

  // Shared by every image upload below - returns an error message, or null if the file
  // is acceptable to send to the server. allowedTypes defaults to the general branding
  // image set; the favicon upload passes its own narrower list (see onFaviconSelected).
  private validateImageFile(file: File, allowedTypes: string[] = ALLOWED_IMAGE_TYPES): string | null {
    if (!allowedTypes.includes(file.type)) {
      return allowedTypes === ALLOWED_FAVICON_TYPES
        ? 'Only PNG, ICO, and JPG images are allowed for the favicon.'
        : 'Only JPG, PNG, WEBP, GIF, and SVG images are allowed.';
    }

    if (file.size > MAX_IMAGE_SIZE_BYTES) {
      return 'Image must be 5 MB or smaller.';
    }

    return null;
  }

  onLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file) {
      return;
    }

    this.uploadError.set(null);
    const validationError = this.validateImageFile(file);
    if (validationError) {
      this.uploadError.set(validationError);
      return;
    }

    this.uploadingLogo.set(true);
    this.settingsService.uploadLogo(file).subscribe({
      next: (res) => {
        this.uploadingLogo.set(false);
        this.form.controls.logoUrl.setValue(res.url);
      },
      error: (err) => {
        this.uploadingLogo.set(false);
        this.uploadError.set(err.error?.errors?.[0] ?? 'Failed to upload logo.');
      }
    });
  }

  removeLogo(): void {
    this.form.controls.logoUrl.setValue('');
  }

  onBackgroundImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file) {
      return;
    }

    this.uploadError.set(null);
    const validationError = this.validateImageFile(file);
    if (validationError) {
      this.uploadError.set(validationError);
      return;
    }

    this.uploadingBackgroundImage.set(true);
    this.settingsService.uploadBackgroundImage(file).subscribe({
      next: (res) => {
        this.uploadingBackgroundImage.set(false);
        this.form.controls.backgroundImageUrl.setValue(res.url);
      },
      error: (err) => {
        this.uploadingBackgroundImage.set(false);
        this.uploadError.set(err.error?.errors?.[0] ?? 'Failed to upload background image.');
      }
    });
  }

  removeBackgroundImage(): void {
    this.form.controls.backgroundImageUrl.setValue('');
  }

  onCenterLogoSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file) {
      return;
    }

    this.uploadError.set(null);
    const validationError = this.validateImageFile(file);
    if (validationError) {
      this.uploadError.set(validationError);
      return;
    }

    this.uploadingCenterLogo.set(true);
    this.settingsService.uploadCenterLogo(file).subscribe({
      next: (res) => {
        this.uploadingCenterLogo.set(false);
        this.form.controls.centerLogoUrl.setValue(res.url);
      },
      error: (err) => {
        this.uploadingCenterLogo.set(false);
        this.uploadError.set(err.error?.errors?.[0] ?? 'Failed to upload center logo.');
      }
    });
  }

  removeCenterLogo(): void {
    this.form.controls.centerLogoUrl.setValue('');
  }

  onFaviconSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file) {
      return;
    }

    this.uploadError.set(null);
    const validationError = this.validateImageFile(file, ALLOWED_FAVICON_TYPES);
    if (validationError) {
      this.uploadError.set(validationError);
      return;
    }

    this.uploadingFavicon.set(true);
    this.settingsService.uploadFavicon(file).subscribe({
      next: (res) => {
        this.uploadingFavicon.set(false);
        this.form.controls.faviconUrl.setValue(res.url);
      },
      error: (err) => {
        this.uploadingFavicon.set(false);
        this.uploadError.set(err.error?.errors?.[0] ?? 'Failed to upload favicon.');
      }
    });
  }

  removeFavicon(): void {
    this.form.controls.faviconUrl.setValue('');
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();

    this.settingsService
      .update({
        restaurantName: raw.restaurantName || null,
        logoUrl: raw.logoUrl || null,
        primaryColor: raw.primaryColor,
        accentColor: raw.accentColor,
        headerColor: raw.headerColor,
        bodyColor: raw.bodyColor,
        backgroundImageUrl: raw.backgroundImageUrl || null,
        centerLogoUrl: raw.centerLogoUrl || null,
        address: raw.address || null,
        phone: raw.phone || null,
        email: raw.email || null,
        footerAbout: raw.footerAbout || null,
        faviconUrl: raw.faviconUrl || null,
        tabTitle: raw.tabTitle || null,
        taxPercentage: raw.taxPercentage,
        baseDeliveryFee: raw.baseDeliveryFee,
        managerPhoneNumber: raw.managerPhoneNumber || null,
        isCashEnabled: raw.isCashEnabled,
        isVisaEnabled: raw.isVisaEnabled,
        visaFawryUrl: raw.visaFawryUrl || null,
        isInstapayEnabled: raw.isInstapayEnabled,
        instapayAccount: raw.instapayAccount || null
      })
      .subscribe({
        next: () => {
          this.saving.set(false);
          this.snackBar.open('Settings saved.', 'Dismiss', { duration: 3000 });
        },
        error: (err) => {
          this.saving.set(false);
          const message = err.error?.errors?.[0] ?? 'Failed to save settings.';
          this.snackBar.open(message, 'Dismiss', { duration: 5000 });
        }
      });
  }
}
