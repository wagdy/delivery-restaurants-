import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReviewService } from '../../core/services/review.service';
import { RATING_SCALE, SubmitReviewAnswer, SurveyQuestion } from '../../core/models/review.model';

// Fallback bucket label for a StarRating question with no matrix section assigned yet
// (existing questions from before this field existed, or one an admin just hasn't
// grouped) - so the matrix always has a real section header instead of a blank bar.
const UNCATEGORIZED_LABEL = 'التقييم العام';

interface RatingCategoryGroup {
  category: string;
  questions: SurveyQuestion[];
}

// The actual survey UI - fetching active questions, rendering the right control per
// QuestionType, and submitting - with no opinion about the page it's dropped into. Used
// two places: embedded directly in the admin's "Live Preview" tab
// (CustomerReviewsComponent, orderId omitted) and wrapped by PublicSurveyPageComponent at
// the real "/rate/store" customer-facing route (orderId bound from the optional
// ?orderId= query param, present for a per-order review, absent for a general store-wide
// one). Both hosts get identical fetch/render/submit/thank-you behavior - only the
// surrounding page chrome (brand header, background, card shell) differs, and that lives
// in each host, not here. Arabic throughout (dir="rtl" on the host) regardless of which
// host embeds it, since the copy itself doesn't change between contexts.

// 1-5 -> the Arabic word describing that rating, shown under the stars once a value is
// picked. Applies to both the Overall Rating block and any per-question StarRating -
// same 5-point scale, same meaning either way.
const RATING_LABELS: Record<number, string> = {
  1: 'ضعيف',
  2: 'مقبول',
  3: 'جيد',
  4: 'جيد جداً',
  5: 'ممتاز'
};

@Component({
  selector: 'app-survey-form',
  standalone: true,
  imports: [CommonModule],
  host: { dir: 'rtl', lang: 'ar' },
  templateUrl: './survey-form.component.html',
  styleUrl: './survey-form.component.scss'
})
export class SurveyFormComponent implements OnInit {
  private readonly reviewService = inject(ReviewService);

  // Null when there's no specific order to attach the review to - the admin's "Live
  // Preview" tab has no order context at all, and PublicSurveyPageComponent leaves it
  // null too when its ?orderId= query param is absent (a general store-wide review).
  readonly orderId = input<number | null>(null);

  protected readonly title = computed(() =>
    this.orderId() !== null ? 'شاركنا رأيك في طلبك' : 'شاركنا رأيك معنا'
  );

  protected readonly starPositions = [1, 2, 3, 4, 5];
  protected readonly ratingScale = RATING_SCALE;

  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly questions = signal<SurveyQuestion[]>([]);

  // StarRating questions only, grouped into the ratings matrix's category sections - in
  // first-occurrence order, so an admin controls section order the same way they already
  // control in-section question order: via each question's own DisplayOrder (the array
  // this groups over is already sorted that way by GetQuestionsAsync).
  protected readonly starRatingCategories = computed<RatingCategoryGroup[]>(() => {
    const groups: RatingCategoryGroup[] = [];
    for (const question of this.questions()) {
      if (question.type !== 'StarRating') {
        continue;
      }
      const category = question.matrixSectionName?.trim() || UNCATEGORIZED_LABEL;
      let group = groups.find((g) => g.category === category);
      if (!group) {
        group = { category, questions: [] };
        groups.push(group);
      }
      group.questions.push(question);
    }
    return groups;
  });

  // Every other question type - rendered individually, exactly as before, either side of
  // the new matrix.
  protected readonly otherQuestions = computed(() => this.questions().filter((q) => q.type !== 'StarRating'));

  protected readonly overallRating = signal(0);
  // questionId -> raw answer value. A StarRating answer is stored as a plain digit
  // string (e.g. "4"), matching the convention already established for ReviewAnswer -
  // parsed back to a number only for rendering (see getQuestionRating).
  protected readonly answers = signal<Record<number, string>>({});

  protected readonly submitting = signal(false);
  protected readonly submitError = signal<string | null>(null);

  // Named exactly isSubmitted (not "submitted") per this component's own spec - flips
  // the template from the question form to the Arabic thank-you state once the API call
  // succeeds. A plain field (not a signal) is enough since it's only ever set from inside
  // an HTTP subscribe callback, which already runs inside Angular's zone.
  isSubmitted = false;

  ngOnInit(): void {
    this.reviewService.getPublicQuestions().subscribe({
      next: (questions) => {
        this.questions.set(questions);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set('عذراً، تعذر تحميل نموذج التقييم. يرجى المحاولة مرة أخرى لاحقاً.');
        this.loading.set(false);
      }
    });
  }

  setOverallRating(value: number): void {
    this.overallRating.set(value);
  }

  isFilledStar(position: number): boolean {
    return position <= this.overallRating();
  }

  setQuestionRating(questionId: number, value: number): void {
    this.answers.update((current) => ({ ...current, [questionId]: String(value) }));
  }

  getQuestionRating(questionId: number): number {
    return Number(this.answers()[questionId] ?? 0);
  }

  // Overall Rating block only - its own separate 1-5 star scale, unrelated to the
  // per-question ratings matrix's 4-point scale below.
  ratingLabel(rating: number): string {
    return RATING_LABELS[rating] ?? '';
  }

  setAnswerText(questionId: number, value: string): void {
    this.answers.update((current) => ({ ...current, [questionId]: value }));
  }

  // Record<number, string> types indexed access as plain `string` (no
  // noUncheckedIndexedAccess), which would otherwise trip Angular's "unnecessary ??"
  // template diagnostic despite genuinely being undefined for an unanswered question at
  // runtime - routed through an explicitly-typed helper instead.
  getAnswerText(questionId: number): string {
    return this.answers()[questionId] ?? '';
  }

  selectOption(questionId: number, option: string): void {
    this.answers.update((current) => ({ ...current, [questionId]: option }));
  }

  isOptionSelected(questionId: number, option: string): boolean {
    return this.answers()[questionId] === option;
  }

  submit(): void {
    if (this.overallRating() < 1) {
      this.submitError.set('يرجى اختيار تقييم عام قبل الإرسال.');
      return;
    }

    // Blank/unanswered questions are simply omitted rather than sent as empty strings -
    // the backend already treats a missing answer as "skipped", and an empty AnswerValue
    // would fail its own [Required] validation anyway.
    const answerPayload: SubmitReviewAnswer[] = Object.entries(this.answers())
      .filter(([, value]) => value.trim().length > 0)
      .map(([questionId, value]) => ({ surveyQuestionId: Number(questionId), answerValue: value.trim() }));

    this.submitting.set(true);
    this.submitError.set(null);

    this.reviewService
      .submit({
        orderId: this.orderId(),
        overallRating: this.overallRating(),
        answers: answerPayload
      })
      .subscribe({
        next: () => {
          this.submitting.set(false);
          this.isSubmitted = true;
        },
        error: (err) => {
          this.submitting.set(false);
          this.submitError.set(
            err.error?.errors?.[0] ?? 'حدث خطأ أثناء إرسال تقييمك. يرجى المحاولة مرة أخرى.'
          );
        }
      });
  }
}
