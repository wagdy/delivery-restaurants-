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
  orderId: number;
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
