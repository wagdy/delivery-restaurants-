import { Component, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogModule, MatDialogRef } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { ReviewService } from '../../../../core/services/review.service';
import { OrderReviewDetail } from '../../../../core/models/review.model';

export interface ReviewDetailsDialogData {
  reviewId: number;
}

// Receives only the id (not the full row already shown in the table) since the table's
// OrderReview shape deliberately excludes answers - this dialog fetches the full detail
// itself rather than the parent pre-loading every row's answers just in case one gets
// opened.
@Component({
  selector: 'app-review-details-dialog',
  standalone: true,
  imports: [CommonModule, MatDialogModule, MatButtonModule, MatIconModule, MatProgressSpinnerModule],
  templateUrl: './review-details-dialog.component.html',
  styleUrl: './review-details-dialog.component.scss'
})
export class ReviewDetailsDialogComponent {
  private readonly reviewService = inject(ReviewService);
  private readonly data = inject<ReviewDetailsDialogData>(MAT_DIALOG_DATA);
  private readonly ref = inject(MatDialogRef<ReviewDetailsDialogComponent>);

  readonly loading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly review = signal<OrderReviewDetail | null>(null);

  readonly stars = [1, 2, 3, 4, 5];

  constructor() {
    this.reviewService.getById(this.data.reviewId).subscribe({
      next: (review) => {
        this.review.set(review);
        this.loading.set(false);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Failed to load this review.');
      }
    });
  }

  close(): void {
    this.ref.close();
  }

  isFilledStar(rating: number, position: number): boolean {
    return position <= rating;
  }

  // A StarRating question's AnswerValue is stored as a plain digit string (e.g. "4") -
  // parsed back to a number just for rendering it the same way as the overall rating.
  answerAsRating(answerValue: string): number {
    return Number(answerValue) || 0;
  }
}
