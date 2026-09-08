import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ReviewService } from '../../../core/services/review.service';
import { SettingsService } from '../../../core/services/settings.service';
import { SubmitReviewAnswer, SurveyQuestion } from '../../../core/models/review.model';

// The fully public, chrome-free per-order review page - reached via the WhatsApp link
// WhatsAppNotificationService.SendPostDeliveryPointsNotificationAsync sends
// ("/customer-review/:orderId"). Unlike CustomerSurveyComponent (the general store-wide
// "/rate/store" page, which has no order context and stays in English matching this
// app's existing storefront copy), this route is always order-specific and the Arabic
// UI text was specified directly for it - so this component's own copy is Arabic
// throughout (dir="rtl" on the host), not just the one button/thank-you line. Deliberately
// standalone with no admin/storefront layout - see AppComponent's isBarePage check.
@Component({
  selector: 'app-customer-review',
  standalone: true,
  imports: [CommonModule],
  host: { dir: 'rtl', lang: 'ar' },
  templateUrl: './customer-review.component.html',
  styleUrl: './customer-review.component.scss'
})
export class CustomerReviewComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly reviewService = inject(ReviewService);
  protected readonly settingsService = inject(SettingsService);

  protected readonly starPositions = [1, 2, 3, 4, 5];

  protected readonly orderId = signal<number | null>(null);
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

  // Named exactly isSubmitted (not "submitted") per this page's own spec - flips the
  // template from the question form to the Arabic thank-you state once the API call
  // succeeds.
  isSubmitted = false;

  ngOnInit(): void {
    const param = this.route.snapshot.paramMap.get('orderId');
    const parsed = param ? Number(param) : NaN;
    if (!Number.isFinite(parsed)) {
      this.loadError.set('رابط التقييم غير صالح.');
      this.loading.set(false);
      return;
    }
    this.orderId.set(parsed);

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
