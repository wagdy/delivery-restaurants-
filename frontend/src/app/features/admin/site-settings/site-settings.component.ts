import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SettingsService } from '../../../core/services/settings.service';

const ALLOWED_IMAGE_TYPES = ['image/jpeg', 'image/png', 'image/webp', 'image/gif', 'image/svg+xml'];
const MAX_IMAGE_SIZE_BYTES = 5 * 1024 * 1024;
const HEX_COLOR_PATTERN = /^#[0-9A-Fa-f]{6}$/;

type SettingsCategory = 'branding' | 'contact';

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
    MatProgressSpinnerModule
  ],
  templateUrl: './site-settings.component.html',
  styleUrl: './site-settings.component.scss'
})
export class SiteSettingsComponent {
  protected readonly settingsService = inject(SettingsService);
  private readonly fb = inject(FormBuilder);
  private readonly snackBar = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly saving = signal(false);
  readonly activeCategory = signal<SettingsCategory>('branding');

  readonly uploadingLogo = signal(false);
  readonly uploadingBackgroundImage = signal(false);
  readonly uploadingCenterLogo = signal(false);
  readonly uploadError = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    restaurantName: ['', [Validators.required, Validators.maxLength(200)]],
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
    footerAbout: ['']
  });

  constructor() {
    this.settingsService.load().subscribe({
      next: (settings) => {
        this.form.patchValue({
          restaurantName: settings.restaurantName,
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
          footerAbout: settings.footerAbout ?? ''
        });
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load settings.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  // Shared by every image upload below - returns an error message, or null if the file
  // is acceptable to send to the server.
  private validateImageFile(file: File): string | null {
    if (!ALLOWED_IMAGE_TYPES.includes(file.type)) {
      return 'Only JPG, PNG, WEBP, GIF, and SVG images are allowed.';
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

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    const raw = this.form.getRawValue();

    this.settingsService
      .update({
        restaurantName: raw.restaurantName,
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
        footerAbout: raw.footerAbout || null
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
