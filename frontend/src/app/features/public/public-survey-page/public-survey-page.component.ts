import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { SettingsService } from '../../../core/services/settings.service';
import { SurveyFormComponent } from '../../../shared/survey-form/survey-form.component';

// The fully public, chrome-free per-order review page - reached via the WhatsApp link
// WhatsAppNotificationService.SendPostDeliveryPointsNotificationAsync sends
// ("/customer-review/:orderId"). Owns only the page shell (brand header, background,
// card) and the route-param parsing; the actual survey fetch/render/submit logic lives
// in the shared SurveyFormComponent, which this just wraps and feeds an orderId. See
// SurveyFormComponent for why the split - the same component is also embedded directly
// in the admin's "Live Preview" tab (CustomerReviewsComponent), which needs none of this
// page's chrome. Deliberately standalone with no admin/storefront layout - see
// AppComponent's isBarePage check.
@Component({
  selector: 'app-public-survey-page',
  standalone: true,
  imports: [CommonModule, SurveyFormComponent],
  host: { dir: 'rtl', lang: 'ar' },
  templateUrl: './public-survey-page.component.html',
  styleUrl: './public-survey-page.component.scss'
})
export class PublicSurveyPageComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  protected readonly settingsService = inject(SettingsService);

  protected readonly orderId = signal<number | null>(null);
  protected readonly invalidLink = signal(false);

  ngOnInit(): void {
    const param = this.route.snapshot.paramMap.get('orderId');
    const parsed = param ? Number(param) : NaN;
    if (!Number.isFinite(parsed)) {
      this.invalidLink.set(true);
      return;
    }
    this.orderId.set(parsed);
  }
}
