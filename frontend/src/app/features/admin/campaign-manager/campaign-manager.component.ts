import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatSnackBar } from '@angular/material/snack-bar';
import { CampaignService } from '../../../core/services/campaign.service';
import { CategoryService } from '../../../core/services/category.service';
import { LoyaltyService } from '../../../core/services/loyalty.service';
import { TierService } from '../../../core/services/tier.service';
import { Campaign } from '../../../core/models/campaign.model';
import { Category } from '../../../core/models/category.model';
import { Tier } from '../../../core/models/tier.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';

@Component({
  selector: 'app-campaign-manager',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatToolbarModule
  ],
  templateUrl: './campaign-manager.component.html',
  styleUrl: './campaign-manager.component.scss'
})
export class CampaignManagerComponent implements OnInit {
  private readonly campaignService = inject(CampaignService);
  private readonly categoryService = inject(CategoryService);
  private readonly loyaltyService = inject(LoyaltyService);
  private readonly tierService = inject(TierService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);

  readonly loading = signal(true);
  readonly campaigns = signal<Campaign[]>([]);
  readonly categories = signal<Category[]>([]);

  readonly newTitle = signal('');
  readonly newDescription = signal('');
  readonly newCategoryName = signal<string | null>(null);
  readonly newTargetPunches = signal(5);
  readonly newEndDate = signal('');
  readonly creating = signal(false);

  // Global points ratios, shown in the Points Configuration card next to the campaign form.
  readonly pointsPerCurrencyUnit = signal(0);
  readonly redemptionValuePer100Points = signal(0);
  readonly loadingSettings = signal(true);
  readonly savingSettings = signal(false);

  // Loyalty Card Tiers - dynamic, admin-defined membership tiers (see LoyaltyTier on the
  // backend). Kept as plain signals + FormsModule ngModel bindings rather than
  // ReactiveFormsModule, matching this component's own campaign-creation and
  // points-configuration forms above rather than introducing a second form paradigm.
  readonly tiers = signal<Tier[]>([]);
  readonly loadingTiers = signal(true);
  readonly savingTier = signal(false);
  readonly editingTierId = signal<number | null>(null);
  readonly tierName = signal('');
  readonly tierMinPoints = signal(0);
  readonly tierMaxPoints = signal<number | null>(null);

  readonly sortedTiers = computed(() => [...this.tiers()].sort((a, b) => a.minPoints - b.minPoints));

  readonly tierFormError = computed(() => {
    const max = this.tierMaxPoints();
    return max !== null && max <= this.tierMinPoints() ? 'Maximum points must be greater than minimum points.' : null;
  });

  // Client-side mirror of the backend's HasOverlappingRangeAsync check - shown live as
  // the admin types, so an overlapping range is visually rejected before they even hit
  // Save, per the requirement to "visually prevent... overlapping ranges".
  readonly tierOverlapWarning = computed(() => {
    const min = this.tierMinPoints();
    const max = this.tierMaxPoints() ?? Number.MAX_SAFE_INTEGER;
    const editingId = this.editingTierId();

    const conflict = this.tiers().find((t) => {
      if (t.id === editingId) {
        return false;
      }
      const tMax = t.maxPoints ?? Number.MAX_SAFE_INTEGER;
      return t.minPoints <= max && min <= tMax;
    });

    return conflict ? `Overlaps with "${conflict.name}" (${this.formatTierRange(conflict)}).` : null;
  });

  readonly canSaveTier = computed(() => this.tierName().trim().length > 0 && !this.tierFormError() && !this.tierOverlapWarning());

  constructor() {
    this.load();
    this.categoryService.getAll().subscribe({ next: (categories) => this.categories.set(categories) });
    this.loadTiers();
  }

  ngOnInit(): void {
    this.loadSettings();
  }

  loadSettings(): void {
    this.loadingSettings.set(true);
    this.loyaltyService.getSettings().subscribe({
      next: (settings) => {
        this.pointsPerCurrencyUnit.set(settings.pointsPerCurrencyUnit);
        this.redemptionValuePer100Points.set(settings.redemptionValuePer100Points);
        this.loadingSettings.set(false);
      },
      error: () => {
        this.loadingSettings.set(false);
        this.snackBar.open('Failed to load points configuration.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  saveSettings(): void {
    this.savingSettings.set(true);
    this.loyaltyService
      .updateSettings({
        pointsPerCurrencyUnit: this.pointsPerCurrencyUnit(),
        redemptionValuePer100Points: this.redemptionValuePer100Points()
      })
      .subscribe({
        next: (settings) => {
          this.pointsPerCurrencyUnit.set(settings.pointsPerCurrencyUnit);
          this.redemptionValuePer100Points.set(settings.redemptionValuePer100Points);
          this.savingSettings.set(false);
          this.snackBar.open('Points configuration saved.', 'Dismiss', { duration: 3000 });
        },
        error: (err) => {
          this.savingSettings.set(false);
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to save points configuration.', 'Dismiss', { duration: 4000 });
        }
      });
  }

  load(): void {
    this.loading.set(true);
    this.campaignService.getAll().subscribe({
      next: (campaigns) => {
        this.campaigns.set(campaigns);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.snackBar.open('Failed to load campaigns.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  create(): void {
    const title = this.newTitle().trim();
    const description = this.newDescription().trim();
    const endDate = this.newEndDate();
    if (!title || !description || !endDate) {
      return;
    }

    this.creating.set(true);
    this.campaignService
      .create({
        title,
        description,
        categoryName: this.newCategoryName(),
        targetPunches: this.newTargetPunches(),
        endDate
      })
      .subscribe({
        next: () => {
          this.creating.set(false);
          this.newTitle.set('');
          this.newDescription.set('');
          this.newCategoryName.set(null);
          this.newTargetPunches.set(5);
          this.newEndDate.set('');
          this.load();
        },
        error: (err) => {
          this.creating.set(false);
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to create campaign.', 'Dismiss', { duration: 4000 });
        }
      });
  }

  toggleStatus(campaign: Campaign): void {
    this.campaignService.toggleStatus(campaign.id).subscribe({
      next: () => this.load(),
      error: () => this.snackBar.open('Failed to update campaign status.', 'Dismiss', { duration: 4000 })
    });
  }

  delete(campaign: Campaign): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete campaign',
        message: `Delete "${campaign.title}"? This only works if no customer has ever punched this campaign's card.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.campaignService.delete(campaign.id).subscribe({
        next: () => this.load(),
        error: (err) => {
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete campaign.', 'Dismiss', { duration: 6000 });
        }
      });
    });
  }

  formatTierRange(tier: Tier): string {
    return tier.maxPoints !== null ? `${tier.minPoints} - ${tier.maxPoints}` : `${tier.minPoints}+`;
  }

  private defaultMinPointsForNewTier(): number {
    const sorted = this.sortedTiers();
    if (sorted.length === 0) {
      return 0;
    }
    const last = sorted[sorted.length - 1];
    return last.maxPoints !== null ? last.maxPoints + 1 : last.minPoints;
  }

  resetTierForm(): void {
    this.editingTierId.set(null);
    this.tierName.set('');
    this.tierMinPoints.set(this.defaultMinPointsForNewTier());
    this.tierMaxPoints.set(null);
  }

  loadTiers(): void {
    this.loadingTiers.set(true);
    this.tierService.getAll().subscribe({
      next: (tiers) => {
        this.tiers.set(tiers);
        this.loadingTiers.set(false);
        if (this.editingTierId() === null) {
          this.resetTierForm();
        }
      },
      error: () => {
        this.loadingTiers.set(false);
        this.snackBar.open('Failed to load loyalty tiers.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  editTier(tier: Tier): void {
    this.editingTierId.set(tier.id);
    this.tierName.set(tier.name);
    this.tierMinPoints.set(tier.minPoints);
    this.tierMaxPoints.set(tier.maxPoints);
  }

  saveTier(): void {
    if (!this.canSaveTier()) {
      return;
    }

    const editingId = this.editingTierId();
    const request = {
      name: this.tierName().trim(),
      minPoints: this.tierMinPoints(),
      maxPoints: this.tierMaxPoints()
    };

    this.savingTier.set(true);
    const request$ = editingId !== null ? this.tierService.update(editingId, request) : this.tierService.create(request);

    request$.subscribe({
      next: () => {
        this.savingTier.set(false);
        this.snackBar.open(editingId !== null ? 'Tier updated.' : 'Tier created.', 'Dismiss', { duration: 3000 });
        this.editingTierId.set(null);
        this.loadTiers();
      },
      error: (err) => {
        this.savingTier.set(false);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to save tier.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  deleteTier(tier: Tier): void {
    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete tier',
        message: `Delete "${tier.name}"? Customers already recorded at this tier keep that value, but it won't be assigned to anyone new.`,
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.tierService.delete(tier.id).subscribe({
        next: () => {
          if (this.editingTierId() === tier.id) {
            this.editingTierId.set(null);
          }
          this.loadTiers();
        },
        error: (err) => {
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete tier.', 'Dismiss', { duration: 4000 });
        }
      });
    });
  }
}
