import { Component, DestroyRef, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { AuthService } from '../../../core/services/auth.service';
import { OrderRealtimeService } from '../../../core/services/order-realtime.service';
import { WakeLockService } from '../../../core/services/wake-lock.service';
import { PushNotificationService } from '../../../core/services/push-notification.service';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, MatTabsModule],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss'
})
export class AdminLayoutComponent {
  readonly authService = inject(AuthService);
  private readonly wakeLockService = inject(WakeLockService);
  private readonly pushNotificationService = inject(PushNotificationService);
  private readonly destroyRef = inject(DestroyRef);

  // Eagerly instantiates the singleton so the order-notifications socket connects once
  // and persists across admin tab navigation, not just while Orders is the active tab.
  private readonly orderRealtimeService = inject(OrderRealtimeService);

  constructor() {
    // Only an account that can actually see the Orders tab needs its screen kept awake
    // or is a useful push-notification target - an Admin restricted to e.g. Menu Items
    // has no reason to be prompted for either.
    if (this.authService.hasModule('Orders')) {
      void this.wakeLockService.request();
      void this.pushNotificationService.subscribe();
      this.destroyRef.onDestroy(() => this.wakeLockService.release());
    }
  }
}
