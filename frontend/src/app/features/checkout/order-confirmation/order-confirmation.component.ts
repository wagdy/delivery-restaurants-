import { Component, computed, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { SettingsService } from '../../../core/services/settings.service';
import { Order } from '../../../core/models/order.model';
import { AddOnNamesPipe } from '../../../shared/pipes/add-on-names.pipe';
import { estimatedDeliveryLabel as formatDeliveryLabel } from '../../../shared/utils/delivery-time.util';
import { OrderStatusLabelPipe } from '../../../shared/pipes/order-status-label.pipe';

@Component({
  selector: 'app-order-confirmation',
  standalone: true,
  imports: [CommonModule, RouterLink, MatCardModule, MatButtonModule, MatIconModule, AddOnNamesPipe, OrderStatusLabelPipe],
  templateUrl: './order-confirmation.component.html',
  styleUrl: './order-confirmation.component.scss'
})
export class OrderConfirmationComponent {
  private readonly settingsService = inject(SettingsService);

  readonly order = signal<Order | null>((history.state?.order as Order) ?? null);
  readonly estimatedDeliveryLabel = computed(() => formatDeliveryLabel(this.settingsService.settings()));

  // The admin-set pickup window, shown in place of the delivery ETA on a pickup
  // order - see the banner in this component's template.
  readonly pickupDuration = computed(() => this.settingsService.settings().pickupDuration?.trim() || null);
}
