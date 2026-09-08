import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { ReviewService } from '../../../core/services/review.service';
import { SettingsService } from '../../../core/services/settings.service';
import { SubmitReviewAnswer, SurveyQuestion } from '../../../core/models/review.model';

// The fully public, chrome-free customer survey page - reached via a WhatsApp rating
// link, either order-specific ("/rate/order/:orderId",
// WhatsAppNotificationService.SendPostDeliveryPointsNotificationAsync) or general
// store-wide ("/rate/store", SendLoyaltyWalletUpdateAsync). Both routes render this same
// component; the only difference is whether an orderId route param is present. Deliberately
// standalone with no admin/storefront layout - see AppComponent's isBarePage check, which
// hides the app-wide toolbar/sidenav specifically for "/rate/" routes.
@Component({
  selector: 'app-customer-survey',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './customer-survey.component.html',
  styleUrl: './customer-survey.component.scss'
})
export class CustomerSurveyComponent implements OnInit {
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
  protected readonly submitted = signal(false);

  ngOnInit(): void {
    const param = this.route.snapshot.paramMap.get('orderId');
    const parsed = param ? Number(param) : NaN;
    this.orderId.set(Number.isFinite(parsed) ? parsed : null);

    this.reviewService.getPublicQuestions().subscribe({
      next: (questions) => {
        this.questions.set(questions);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set('Sorry, we could not load the feedback form. Please try again later.');
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
  // noUncheckedIndexedAccess), which made a template-level `answers()[id] ?? ''` trip an
  // "unnecessary ??" compiler warning despite genuinely being undefined for an
  // unanswered question at runtime - routed through an explicitly-typed helper instead.
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
      this.submitError.set('Please choose an overall rating before submitting.');
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
          this.submitted.set(true);
        },
        error: (err) => {
          this.submitting.set(false);
          this.submitError.set(
            err.error?.errors?.[0] ?? 'Something went wrong submitting your feedback. Please try again.'
          );
        }
      });
  }
}
