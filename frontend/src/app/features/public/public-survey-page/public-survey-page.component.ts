import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute } from '@angular/router';
import { SettingsService } from '../../../core/services/settings.service';
import { SurveyFormComponent } from '../../../shared/survey-form/survey-form.component';

// The single fully public, chrome-free rating page - mounted at "/rate/store" and used
// by both WhatsApp touchpoints: SendLoyaltyWalletUpdateAsync's general store-wide link
// (no orderId at all) and SendPostDeliveryPointsNotificationAsync's per-order link
// (?orderId=<id> query param - a query param rather than a path segment specifically so
// this one route/component serves both cases). The legacy "/customer-review/:orderId"
// path redirects here (see app.routes.ts) for any already-sent WhatsApp message using
// the old link shape. Owns only the page shell (brand header, background, card) and the
// query-param parsing; the actual survey fetch/render/submit logic lives in the shared
// SurveyFormComponent, which this just wraps and feeds an orderId. See
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
    // Absent entirely -> a general store-wide review, valid on its own (matches
    // SendLoyaltyWalletUpdateAsync's link, which never carries this param). Only a
    // *present but unparseable* value (a tampered/garbled URL) counts as invalid.
    const param = this.route.snapshot.queryParamMap.get('orderId');
    if (param === null) {
      return;
    }

    const parsed = Number(param);
    if (!Number.isFinite(parsed)) {
      this.invalidLink.set(true);
      return;
    }
    this.orderId.set(parsed);
  }
}
