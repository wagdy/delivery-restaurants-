import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { QRCodeComponent } from 'angularx-qrcode';
import { LoyaltyService } from '../../../core/services/loyalty.service';
import { CampaignService } from '../../../core/services/campaign.service';
import { TierService } from '../../../core/services/tier.service';
import { AuthService } from '../../../core/services/auth.service';
import { CustomerCampaignProgress } from '../../../core/models/campaign.model';
import { Tier } from '../../../core/models/tier.model';
import { tierStyleClass } from '../../../shared/utils/tier-style.util';

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
  private readonly tierService = inject(TierService);
  protected readonly authService = inject(AuthService);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly walletError = signal<string | null>(null);
  readonly addingToAppleWallet = signal(false);
  readonly addingToGoogleWallet = signal(false);

  // Shared state owned by the services (not local signals) - LoyaltyRealtimeService
  // pushes live updates into these same signals from anywhere in the app, so this
  // component picks them up automatically with no extra wiring here.
  readonly me = this.loyaltyService.me;
  readonly campaigns = this.campaignService.myProgress;

  // Admin-configurable now (see Campaign Manager's "Loyalty Card Tiers" panel) instead
  // of the old fixed Bronze/Silver/Gold/VIP thresholds - fetched once here rather than
  // through a shared service signal, since (unlike points) nothing else in the app needs
  // to react to a tier list edited by an admin elsewhere while this page is open.
  private readonly tiers = signal<Tier[]>([]);
  private readonly sortedTiers = computed(() => [...this.tiers()].sort((a, b) => a.minPoints - b.minPoints));

  // Resolved the same way the backend does (range lookup against totalLifetimePoints),
  // not by matching membershipTier's name string - stays correct even if that snapshot
  // string is momentarily stale, and needs no special-casing for "no tier yet".
  private readonly currentTierIndex = computed(() => {
    const points = this.me()?.totalLifetimePoints ?? 0;
    const tiers = this.sortedTiers();
    let index = -1;
    for (let i = 0; i < tiers.length; i++) {
      const tier = tiers[i];
      if (tier.minPoints <= points && (tier.maxPoints === null || points <= tier.maxPoints)) {
        index = i;
      }
    }
    return index;
  });

  readonly nextTier = computed<Tier | null>(() => {
    const tiers = this.sortedTiers();
    const index = this.currentTierIndex();
    return index >= 0 && index + 1 < tiers.length ? tiers[index + 1] : null;
  });

  // Progress toward the next tier, 0-100. No next tier (top tier, or no tiers configured
  // above the current one) always reads as complete.
  readonly tierProgressPercent = computed(() => {
    const me = this.me();
    const next = this.nextTier();
    if (!me || !next) {
      return 100;
    }

    const index = this.currentTierIndex();
    const rangeStart = index >= 0 ? this.sortedTiers()[index].minPoints : 0;
    const span = next.minPoints - rangeStart;
    if (span <= 0) {
      return 100;
    }

    return Math.min(100, Math.max(0, Math.round(((me.totalLifetimePoints - rangeStart) / span) * 100)));
  });

  readonly pointsToNextTier = computed(() => {
    const me = this.me();
    const next = this.nextTier();
    return me && next ? Math.max(0, next.minPoints - me.totalLifetimePoints) : 0;
  });

  constructor() {
    // The service's own tap() already populates the shared `me`/`campaigns` signals
    // above - this subscribe is only here to drive this component's own loading/error
    // state for the initial fetch.
    this.loyaltyService.getMe().subscribe({
      next: () => this.loading.set(false),
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load your rewards. Please try again later.');
      }
    });

    this.campaignService.getMyProgress().subscribe();

    // Best-effort: if this fails, nextTier()/tierProgressPercent() just read as "top
    // tier reached" rather than the whole page erroring out.
    this.tierService.getAll().subscribe({
      next: (tiers) => this.tiers.set(tiers)
    });
  }

  tierClass(tierName: string): string {
    return tierStyleClass(tierName);
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
