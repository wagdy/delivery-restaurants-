import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ZXingScannerModule } from '@zxing/ngx-scanner';
import { BarcodeFormat } from '@zxing/library';
import { LoyaltyService } from '../../../core/services/loyalty.service';
import { CampaignService } from '../../../core/services/campaign.service';
import { CustomerService } from '../../../core/services/customer.service';
import { ScannerCustomer } from '../../../core/models/loyalty.model';

type ScannerTab = 'find' | 'new';

@Component({
  selector: 'app-qr-scanner',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    ReactiveFormsModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    ZXingScannerModule
  ],
  templateUrl: './qr-scanner.component.html',
  styleUrl: './qr-scanner.component.scss'
})
export class QrScannerComponent {
  private readonly loyaltyService = inject(LoyaltyService);
  private readonly campaignService = inject(CampaignService);
  private readonly customerService = inject(CustomerService);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  readonly allowedFormats = [BarcodeFormat.QR_CODE];

  readonly activeTab = signal<ScannerTab>('find');

  switchTab(tab: ScannerTab): void {
    this.activeTab.set(tab);
  }

  // --- Tab 2: New Customer ---

  readonly registeringCustomer = signal(false);

  readonly newCustomerForm = this.fb.nonNullable.group({
    customerName: ['', [Validators.required, Validators.maxLength(200)]],
    phone: ['', [Validators.required, Validators.maxLength(20)]]
  });

  registerCustomer(): void {
    if (this.newCustomerForm.invalid) {
      this.newCustomerForm.markAllAsTouched();
      return;
    }

    this.registeringCustomer.set(true);
    const raw = this.newCustomerForm.getRawValue();

    this.customerService.registerCustomer(raw).subscribe({
      next: (result) => {
        this.registeringCustomer.set(false);
        this.newCustomerForm.reset({ customerName: '', phone: '' });

        this.snackBar.open(
          result.isNewCustomer
            ? 'Customer registered - 100 welcome points awarded.'
            : 'This phone number is already registered to an existing customer.',
          'Dismiss',
          { duration: 5000 }
        );

        // Jump straight to their loyalty card, on either branch - confirms the bonus
        // (or shows the existing balance) without staff needing a second lookup.
        this.activeTab.set('find');
        this.scanningEnabled.set(false);
        this.loadCustomer(result.customerId);
      },
      error: (err) => {
        this.registeringCustomer.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to register customer.', 'Dismiss', { duration: 6000 });
      }
    });
  }

  // Scanning is paused (enable = false) once a customer is loaded, so the camera stops
  // decoding frames while staff is reviewing/acting on the result.
  readonly scanningEnabled = signal(true);
  readonly cameraAvailable = signal(true);
  readonly permissionDenied = signal(false);

  readonly loadingCustomer = signal(false);
  readonly customer = signal<ScannerCustomer | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly phoneSearch = signal('');
  readonly searchingByPhone = signal(false);

  readonly checkAmount = signal<number | null>(null);
  readonly earning = signal(false);
  readonly pointsToRedeem = signal<number | null>(null);
  readonly redeeming = signal(false);
  readonly actingCampaignId = signal<string | null>(null);

  onScanSuccess(customerId: string): void {
    if (!this.scanningEnabled()) {
      return;
    }

    this.scanningEnabled.set(false);
    this.loadCustomer(customerId);
  }

  onCamerasFound(devices: MediaDeviceInfo[]): void {
    this.cameraAvailable.set(devices.length > 0);
  }

  onPermissionResponse(granted: boolean): void {
    this.permissionDenied.set(!granted);
  }

  loadCustomer(customerId: string): void {
    this.loadingCustomer.set(true);
    this.errorMessage.set(null);

    this.loyaltyService.getScannerCustomer(customerId).subscribe({
      next: (customer) => {
        this.customer.set(customer);
        this.loadingCustomer.set(false);
      },
      error: () => {
        this.loadingCustomer.set(false);
        this.errorMessage.set('Could not identify a customer from that code.');
      }
    });
  }

  // Manual fallback alongside the camera - e.g. the customer's phone is dead/QR unreadable.
  // Loads the exact same customer() state a successful scan would, so every downstream
  // action (earn/redeem/punch) works identically regardless of how the customer was found.
  searchByPhone(): void {
    const phone = this.phoneSearch().trim();
    if (!phone || this.searchingByPhone()) {
      return;
    }

    this.searchingByPhone.set(true);

    this.loyaltyService.getScannerCustomerByPhone(phone).subscribe({
      next: (customer) => {
        this.searchingByPhone.set(false);
        this.scanningEnabled.set(false);
        this.customer.set(customer);
        this.phoneSearch.set('');
      },
      error: () => {
        this.searchingByPhone.set(false);
        this.snackBar.open('Customer not found.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  scanAnother(): void {
    this.customer.set(null);
    this.errorMessage.set(null);
    this.checkAmount.set(null);
    this.pointsToRedeem.set(null);
    this.phoneSearch.set('');
    this.scanningEnabled.set(true);
  }

  earnPoints(): void {
    const customer = this.customer();
    const amount = this.checkAmount();
    if (!customer || !amount || amount <= 0) {
      return;
    }

    this.earning.set(true);
    this.loyaltyService.earn({ customerId: customer.appUserId, checkAmount: amount }).subscribe({
      next: (result) => {
        this.earning.set(false);
        this.checkAmount.set(null);
        this.customer.update((c) => (c ? { ...c, currentPoints: result.currentPoints, totalLifetimePoints: result.totalLifetimePoints, membershipTier: result.membershipTier } : c));
        this.snackBar.open(`Awarded ${result.pointsTransacted} points.`, 'Dismiss', { duration: 4000 });
      },
      error: (err) => {
        this.earning.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to award points.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  redeemPoints(): void {
    const customer = this.customer();
    const points = this.pointsToRedeem();
    if (!customer || !points || points <= 0) {
      return;
    }

    this.redeeming.set(true);
    this.loyaltyService.redeem({ customerId: customer.appUserId, pointsToRedeem: points }).subscribe({
      next: (result) => {
        this.redeeming.set(false);
        this.pointsToRedeem.set(null);
        this.customer.update((c) => (c ? { ...c, currentPoints: result.currentPoints } : c));
        this.snackBar.open(`Redeemed ${points} points for a L.E ${result.discountAmount.toFixed(2)} discount.`, 'Dismiss', { duration: 4000 });
      },
      error: (err) => {
        this.redeeming.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to redeem points.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  punchCampaign(campaignId: string): void {
    const customer = this.customer();
    if (!customer) {
      return;
    }

    this.actingCampaignId.set(campaignId);
    this.campaignService.punch({ customerId: customer.appUserId, campaignId }).subscribe({
      next: (result) => {
        this.actingCampaignId.set(null);
        this.customer.update((c) =>
          c
            ? {
                ...c,
                campaigns: c.campaigns.map((camp) =>
                  camp.campaignId === campaignId
                    ? { ...camp, currentPunches: result.currentPunches, rewardsEarned: result.rewardsEarned }
                    : camp
                )
              }
            : c
        );
        this.snackBar.open(
          result.rewardEarnedThisPunch ? '🎉 Reward earned! Card reset.' : 'Punch added.',
          'Dismiss',
          { duration: 4000 }
        );
      },
      error: (err) => {
        this.actingCampaignId.set(null);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to add punch.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  redeemCampaignReward(campaignId: string): void {
    const customer = this.customer();
    if (!customer) {
      return;
    }

    this.actingCampaignId.set(campaignId);
    this.campaignService.redeemReward({ customerId: customer.appUserId, campaignId }).subscribe({
      next: (result) => {
        this.actingCampaignId.set(null);
        this.customer.update((c) =>
          c
            ? { ...c, campaigns: c.campaigns.map((camp) => (camp.campaignId === campaignId ? { ...camp, rewardsEarned: result.rewardsEarned } : camp)) }
            : c
        );
        this.snackBar.open('Reward redeemed.', 'Dismiss', { duration: 4000 });
      },
      error: (err) => {
        this.actingCampaignId.set(null);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to redeem reward.', 'Dismiss', { duration: 4000 });
      }
    });
  }
}
