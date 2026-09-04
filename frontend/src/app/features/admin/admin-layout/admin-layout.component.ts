import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { MatTabsModule } from '@angular/material/tabs';
import { AuthService } from '../../../core/services/auth.service';
import { OrderRealtimeService } from '../../../core/services/order-realtime.service';

@Component({
  selector: 'app-admin-layout',
  standalone: true,
  imports: [RouterLink, RouterLinkActive, RouterOutlet, MatTabsModule],
  templateUrl: './admin-layout.component.html',
  styleUrl: './admin-layout.component.scss'
})
export class AdminLayoutComponent {
  readonly authService = inject(AuthService);

  // Eagerly instantiates the singleton so the order-notifications socket connects once
  // and persists across admin tab navigation, not just while Orders is the active tab.
  private readonly orderRealtimeService = inject(OrderRealtimeService);
}
