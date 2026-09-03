import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { QRCodeComponent } from 'angularx-qrcode';
import { LoyaltyService } from '../../../core/services/loyalty.service';
import { CampaignService } from '../../../core/services/campaign.service';
import { AuthService } from '../../../core/services/auth.service';
import { LoyaltyMe, MembershipTier } from '../../../core/models/loyalty.model';
import { CustomerCampaignProgress } from '../../../core/models/campaign.model';

const TIER_THRESHOLDS: Record<MembershipTier, number | null> = {
  Bronze: 500,
  Silver: 1000,
  Gold: 2500,
  VIP: null
};

const NEXT_TIER: Record<MembershipTier, MembershipTier | null> = {
  Bronze: 'Silver',
  Silver: 'Gold',
  Gold: 'VIP',
  VIP: null
};

@Component({
  selector: 'app-loyalty-rewards',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule, MatProgressSpinnerModule, QRCodeComponent],
  templateUrl: './loyalty-rewards.component.html',
  styleUrl: './loyalty-rewards.component.scss'
})
export class LoyaltyRewardsComponent {
  private readonly loyaltyService = inject(LoyaltyService);
  private readonly campaignService = inject(CampaignService);
  protected readonly authService = inject(AuthService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly walletError = signal<string | null>(null);
  readonly addingToAppleWallet = signal(false);
  readonly addingToGoogleWallet = signal(false);
  readonly me = signal<LoyaltyMe | null>(null);

  // Loaded independently of `me` - a failure here shouldn't block the points card from
  // showing, so it's just an empty list rather than a shared error state.
  readonly campaigns = signal<CustomerCampaignProgress[]>([]);

  readonly nextTier = computed(() => {
    const me = this.me();
    return me ? NEXT_TIER[me.membershipTier] : null;
  });

  // Progress toward the next tier, 0-100. VIP (no next tier) always reads as complete.
  readonly tierProgressPercent = computed(() => {
    const me = this.me();
    if (!me) {
      return 0;
    }

    const threshold = TIER_THRESHOLDS[me.membershipTier];
    if (threshold === null) {
      return 100;
    }

    return Math.min(100, Math.round((me.totalLifetimePoints / threshold) * 100));
  });

  readonly pointsToNextTier = computed(() => {
    const me = this.me();
    if (!me) {
      return 0;
    }

    const threshold = TIER_THRESHOLDS[me.membershipTier];
    return threshold === null ? 0 : Math.max(0, threshold - me.totalLifetimePoints);
  });

  constructor() {
    this.loyaltyService.getMe().subscribe({
      next: (me) => {
        this.me.set(me);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load your rewards. Please try again later.');
      }
    });

    this.campaignService.getMyProgress().subscribe({
      next: (campaigns) => this.campaigns.set(campaigns)
    });
  }

  // A boolean per stamp slot - filled for slots already punched, empty for the rest.
  stampsFor(campaign: CustomerCampaignProgress): boolean[] {
    return Array.from({ length: campaign.targetPunches }, (_, i) => i < campaign.currentPunches);
  }

  addToAppleWallet(): void {
    this.walletError.set(null);
    this.addingToAppleWallet.set(true);

    this.loyaltyService.getAppleWalletPassBlob().subscribe({
      next: (blob) => {
        this.addingToAppleWallet.set(false);
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        document.body.appendChild(link);
        link.click();
        link.remove();
        setTimeout(() => URL.revokeObjectURL(url), 10_000);
      },
      error: () => {
        this.addingToAppleWallet.set(false);
        this.walletError.set('Apple Wallet is not available right now.');
      }
    });
  }

  addToGoogleWallet(): void {
    this.walletError.set(null);
    this.addingToGoogleWallet.set(true);

    this.loyaltyService.getGoogleWalletSaveLink().subscribe({
      next: ({ saveUrl }) => {
        this.addingToGoogleWallet.set(false);
        window.open(saveUrl, '_blank', 'noopener');
      },
      error: () => {
        this.addingToGoogleWallet.set(false);
        this.walletError.set('Google Wallet is not available right now.');
      }
    });
  }
}
