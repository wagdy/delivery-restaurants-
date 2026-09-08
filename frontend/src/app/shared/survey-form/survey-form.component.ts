import { Component, OnInit, computed, inject, input, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReviewService } from '../../core/services/review.service';
import { SubmitReviewAnswer, SurveyQuestion } from '../../core/models/review.model';

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

  protected readonly loading = signal(true);
  protected readonly loadError = signal<string | null>(null);
  protected readonly questions = signal<SurveyQuestion[]>([]);

  protected readonly overallRating = signal(0);
  // questionId -> raw answer value. A StarRating answer is stored as a plain digit
  // string (e.g. "4"), matching the convention already established for ReviewAnswer -
  // parsed back to a number only for rendering (see isQuestionStarFilled).
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

  isQuestionStarFilled(questionId: number, position: number): boolean {
    return position <= Number(this.answers()[questionId] ?? 0);
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
