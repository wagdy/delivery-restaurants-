import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialog } from '@angular/material/dialog';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSnackBar } from '@angular/material/snack-bar';
import { ReviewService } from '../../../core/services/review.service';
import { OrderReview, SurveyQuestion, SurveyQuestionRequest, SurveyQuestionType } from '../../../core/models/review.model';
import { ConfirmDialogComponent } from '../../../shared/confirm-dialog/confirm-dialog.component';
import { SurveyFormComponent } from '../../../shared/survey-form/survey-form.component';
import { ReviewDetailsDialogComponent } from './review-details-dialog/review-details-dialog.component';

type CustomerReviewsTab = 'survey' | 'reviews' | 'preview';

const QUESTION_TYPE_OPTIONS: { value: SurveyQuestionType; label: string }[] = [
  { value: 'StarRating', label: 'Star Rating' },
  { value: 'ShortText', label: 'Short Text' },
  { value: 'LongText', label: 'Long Text' },
  // Display label only - the underlying value stays 'SingleChoice' to match the C#
  // SurveyQuestionType enum unchanged, so no backend/schema change is needed for this
  // rename.
  { value: 'SingleChoice', label: 'Choice' }
];

@Component({
  selector: 'app-customer-reviews',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatToolbarModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    SurveyFormComponent
  ],
  templateUrl: './customer-reviews.component.html',
  styleUrl: './customer-reviews.component.scss'
})
export class CustomerReviewsComponent implements OnInit {
  private readonly reviewService = inject(ReviewService);
  private readonly dialog = inject(MatDialog);
  private readonly snackBar = inject(MatSnackBar);
  private readonly fb = inject(FormBuilder);

  readonly activeTab = signal<CustomerReviewsTab>('survey');
  readonly questionTypeOptions = QUESTION_TYPE_OPTIONS;
  readonly starPositions = [1, 2, 3, 4, 5];

  // --- Tab 1: Survey Configuration ---

  readonly loadingQuestions = signal(true);
  readonly savingIndex = signal<number | null>(null);
  readonly deletingIndex = signal<number | null>(null);

  readonly surveyForm = this.fb.group({
    questions: this.fb.array<ReturnType<typeof this.buildQuestionGroup>>([])
  });

  get questions() {
    return this.surveyForm.controls.questions;
  }

  // --- Tab 2: Submitted Reviews ---

  readonly reviews = signal<OrderReview[]>([]);
  readonly loadingReviews = signal(true);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly pageSize = 20;

  ngOnInit(): void {
    this.loadQuestions();
    this.loadReviews();
  }

  switchTab(tab: CustomerReviewsTab): void {
    this.activeTab.set(tab);
  }

  // --- Tab 1 methods ---

  loadQuestions(): void {
    this.loadingQuestions.set(true);
    this.reviewService.getQuestions().subscribe({
      next: (questions) => {
        this.questions.clear();
        questions
          .slice()
          .sort((a, b) => a.displayOrder - b.displayOrder)
          .forEach((q) => this.questions.push(this.buildQuestionGroup(q)));
        this.loadingQuestions.set(false);
      },
      error: () => {
        this.loadingQuestions.set(false);
        this.snackBar.open('Failed to load survey questions.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  private buildQuestionGroup(question?: SurveyQuestion) {
    return this.fb.group({
      id: this.fb.control<number | null>(question?.id ?? null),
      text: this.fb.nonNullable.control(question?.text ?? '', [Validators.required, Validators.maxLength(500)]),
      type: this.fb.nonNullable.control<SurveyQuestionType>(question?.type ?? 'StarRating'),
      // The Choice builder's live list of options - SurveyQuestion.options is already a
      // string[] (the comma-separated <-> array conversion happens entirely server-side,
      // see SurveyQuestionResponse.Options / ReviewService.SerializeOptions), so this
      // just carries that array straight through with no join/split needed at this layer.
      options: this.fb.nonNullable.control<string[]>(question?.options ?? []),
      // Scratch input for "type a choice, click Add Choice" - never sent to the API
      // (excluded in toRequest below), just holds whatever the admin is currently typing
      // for this row.
      newOptionText: this.fb.nonNullable.control(''),
      // Only meaningful for StarRating - which ratings-matrix section (e.g. "Service",
      // "Food") this question is grouped under on the public survey.
      category: this.fb.nonNullable.control(question?.category ?? ''),
      isActive: this.fb.nonNullable.control(question?.isActive ?? true)
    });
  }

  addQuestion(): void {
    this.questions.push(this.buildQuestionGroup());
  }

  isSingleChoice(index: number): boolean {
    return this.questions.at(index).getRawValue().type === 'SingleChoice';
  }

  isStarRating(index: number): boolean {
    return this.questions.at(index).getRawValue().type === 'StarRating';
  }

  // Ignores an empty/whitespace-only entry and a duplicate of an option already added -
  // silently, rather than an error toast, since mis-clicking "Add Choice" on a blank or
  // repeated value isn't a mistake worth interrupting the admin over.
  addChoiceOption(index: number): void {
    const controls = this.questions.at(index).controls;
    const value = controls.newOptionText.value.trim();
    if (!value || controls.options.value.includes(value)) {
      controls.newOptionText.setValue('');
      return;
    }

    controls.options.setValue([...controls.options.value, value]);
    controls.newOptionText.setValue('');
  }

  removeChoiceOption(index: number, optionIndex: number): void {
    const controls = this.questions.at(index).controls;
    controls.options.setValue(controls.options.value.filter((_, i) => i !== optionIndex));
  }

  private toRequest(index: number): SurveyQuestionRequest {
    const raw = this.questions.at(index).getRawValue();

    return {
      text: raw.text.trim(),
      type: raw.type,
      options: raw.type === 'SingleChoice' ? raw.options : null,
      category: raw.type === 'StarRating' ? raw.category.trim() || null : null,
      isActive: raw.isActive,
      displayOrder: index
    };
  }

  saveQuestion(index: number): void {
    const group = this.questions.at(index);
    if (group.invalid) {
      group.markAllAsTouched();
      return;
    }

    const request = this.toRequest(index);
    if (request.type === 'SingleChoice' && (!request.options || request.options.length < 2)) {
      this.snackBar.open('A Choice question needs at least 2 options.', 'Dismiss', {
        duration: 4000
      });
      return;
    }

    this.savingIndex.set(index);
    const id = group.getRawValue().id;
    const request$ =
      id !== null ? this.reviewService.updateQuestion(id, request) : this.reviewService.createQuestion(request);

    request$.subscribe({
      next: (saved) => {
        this.savingIndex.set(null);
        group.patchValue({ id: saved.id }, { emitEvent: false });
        this.snackBar.open(id !== null ? 'Question updated.' : 'Question created.', 'Dismiss', { duration: 3000 });
      },
      error: (err) => {
        this.savingIndex.set(null);
        this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to save question.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  removeQuestion(index: number): void {
    const group = this.questions.at(index);
    const id = group.getRawValue().id;

    if (id === null) {
      // Never saved - just drop the row, nothing to ask the server to delete.
      this.questions.removeAt(index);
      return;
    }

    const confirmRef = this.dialog.open(ConfirmDialogComponent, {
      data: {
        title: 'Delete question',
        message:
          'Delete this survey question? It will disappear from this list and the customer survey - any past customer answers to it are kept for historical reviews.',
        confirmLabel: 'Delete',
        danger: true
      }
    });

    confirmRef.afterClosed().subscribe((confirmed: boolean) => {
      if (!confirmed) {
        return;
      }

      this.deletingIndex.set(index);
      this.reviewService.deleteQuestion(id).subscribe({
        next: () => {
          this.deletingIndex.set(null);
          this.questions.removeAt(index);
        },
        error: (err) => {
          this.deletingIndex.set(null);
          this.snackBar.open(err.error?.errors?.[0] ?? 'Failed to delete question.', 'Dismiss', { duration: 6000 });
        }
      });
    });
  }

  // --- Tab 2 methods ---

  loadReviews(): void {
    this.loadingReviews.set(true);
    this.reviewService.getAll(this.page(), this.pageSize).subscribe({
      next: (result) => {
        this.reviews.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loadingReviews.set(false);
      },
      error: () => {
        this.loadingReviews.set(false);
        this.snackBar.open('Failed to load submitted reviews.', 'Dismiss', { duration: 4000 });
      }
    });
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.totalCount() / this.pageSize));
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages()) {
      return;
    }
    this.page.set(page);
    this.loadReviews();
  }

  isFilledStar(rating: number, position: number): boolean {
    return position <= rating;
  }

  viewDetails(review: OrderReview): void {
    this.dialog.open(ReviewDetailsDialogComponent, {
      width: '520px',
      data: { reviewId: review.id }
    });
  }
}
