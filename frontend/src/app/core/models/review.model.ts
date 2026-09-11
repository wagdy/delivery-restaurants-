export type SurveyQuestionType = 'StarRating' | 'ShortText' | 'LongText' | 'SingleChoice';

export interface SurveyQuestion {
  id: number;
  text: string;
  type: SurveyQuestionType;
  // Only meaningful (and only ever populated) when type is 'SingleChoice'.
  options: string[] | null;
  // Only meaningful (and only ever populated) when type is 'StarRating' - which managed
  // SurveyMatrixSection this question is grouped under (e.g. "Service", "Food"). Null
  // falls back to a generic bucket - see SurveyFormComponent's starRatingCategories.
  matrixSectionId: number | null;
  // Resolved alongside matrixSectionId by the backend for display/grouping, so this page
  // and the public survey never need a second lookup just to show the section's name.
  matrixSectionName: string | null;
  isActive: boolean;
  displayOrder: number;
}

export interface SurveyQuestionRequest {
  text: string;
  type: SurveyQuestionType;
  options: string[] | null;
  matrixSectionId: number | null;
  isActive: boolean;
  displayOrder: number;
}

// A managed, named grouping for StarRating questions (e.g. "Service", "Food") - see
// backend SurveyMatrixSection. Replaces the old free-text Category string with a
// dropdown sourced from this list, managed via the "Manage Sections" dialog.
export interface SurveyMatrixSection {
  id: number;
  name: string;
}

export interface SurveyMatrixSectionRequest {
  name: string;
}

// The public survey's per-question rating matrix scale (StarRating questions only) - a
// deliberate 4-point scale, distinct from the Overall Rating block's own 1-5 stars, which
// stays a separate, unchanged widget. Ordered best-to-worst so it reads naturally
// right-to-left in the dir="rtl" survey/admin views (ممتاز sits first/rightmost).
// Exported so SurveyFormComponent (rendering) and ReviewDetailsDialogComponent (reading
// back a past answer) can never define two independently-drifting copies of this mapping.
export const RATING_SCALE: { value: number; label: string }[] = [
  { value: 4, label: 'ممتاز / Excellent' },
  { value: 3, label: 'جيد / Good' },
  { value: 2, label: 'مقبول / Fair' },
  { value: 1, label: 'ضعيف / Poor' }
];

// A past answer stored under the old 5-point scale (value 4 = "جيد جداً", 5 = "ممتاز")
// collapses onto this new 4-point scale by clamping to 4 - both old top-end values read
// as the new top label, and 1-3 map through unchanged. This is the one place that mapping
// happens, so a historical review's stars/labels never look "off the scale" post-redesign.
export function ratingScaleLabel(rawValue: number): string {
  const clamped = Math.min(Math.max(Math.round(rawValue), 1), 4);
  return RATING_SCALE.find((r) => r.value === clamped)?.label ?? '';
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
