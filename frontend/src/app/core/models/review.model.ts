export type SurveyQuestionType = 'StarRating' | 'ShortText' | 'LongText' | 'SingleChoice';

export interface SurveyQuestion {
  id: number;
  text: string;
  type: SurveyQuestionType;
  // Only meaningful (and only ever populated) when type is 'SingleChoice'.
  options: string[] | null;
  isActive: boolean;
  displayOrder: number;
}

export interface SurveyQuestionRequest {
  text: string;
  type: SurveyQuestionType;
  options: string[] | null;
  isActive: boolean;
  displayOrder: number;
}

// The "Submitted Reviews" table row shape - deliberately light (no answers), matching
// OrderReviewResponse on the backend.
export interface OrderReview {
  id: number;
  // Null for a general store-wide review submitted via /rate/store (no specific order).
  orderId: number | null;
  customerName: string;
  overallRating: number;
  createdAt: string;
}

export interface ReviewAnswer {
  surveyQuestionId: number;
  questionText: string;
  questionType: SurveyQuestionType;
  answerValue: string;
}

export interface OrderReviewDetail extends OrderReview {
  answers: ReviewAnswer[];
}

// Public survey page payload - matches the backend's SubmitReviewRequest exactly. No
// customerId field: attribution comes only from the caller's own JWT when they happen to
// be signed in (see PublicReviewsController.Submit) - a client-supplied customer id would
// let anyone attribute a fabricated review to any real customer's account.
export interface SubmitReviewRequest {
  orderId: number | null;
  overallRating: number;
  answers: SubmitReviewAnswer[];
}

export interface SubmitReviewAnswer {
  surveyQuestionId: number;
  answerValue: string;
}
