import { Component, inject, signal } from '@angular/core';
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
import { Campaign } from '../../../core/models/campaign.model';
import { Category } from '../../../core/models/category.model';
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
export class CampaignManagerComponent {
  private readonly campaignService = inject(CampaignService);
  private readonly categoryService = inject(CategoryService);
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

  constructor() {
    this.load();
    this.categoryService.getAll().subscribe({ next: (categories) => this.categories.set(categories) });
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
    if (!title || !description) {
      return;
    }

    this.creating.set(true);
    this.campaignService
      .create({
        title,
        description,
        categoryName: this.newCategoryName(),
        targetPunches: this.newTargetPunches(),
        endDate: this.newEndDate() || null
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
}
