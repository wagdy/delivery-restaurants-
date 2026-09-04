import { Injectable, effect, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { NewOrderNotification } from '../models/order.model';

// Admin/cashier-only counterpart to LoyaltyRealtimeService: pushes newly-created orders
// to the admin Orders tab the instant they land, instead of staff having to manually
// refresh. Kept alive app-wide (see AdminLayoutComponent, which injects this once to
// instantiate the singleton eagerly) so the connection persists across admin tab
// navigation, not just while the Orders tab itself is active. Exposes a plain
// Observable rather than mutating shared state directly - the admin dashboard component
// owns the array-prepend/toast/sound behavior itself.
@Injectable({ providedIn: 'root' })
export class OrderRealtimeService {
  private readonly authService = inject(AuthService);

  private readonly newOrderReceivedSubject = new Subject<NewOrderNotification>();
  readonly newOrderReceived = this.newOrderReceivedSubject.asObservable();

  private connection: HubConnection | null = null;

  constructor() {
    effect(() => {
      // Defensive admin-only check on top of AdminLayoutComponent being the only
      // injection point - OrderHub itself also enforces "OrdersAccess" server-side, so
      // a non-admin connection attempt would fail there regardless.
      if (this.authService.token() && this.authService.isAdmin()) {
        this.connect();
      } else {
        this.disconnect();
      }
    });
  }

  private connect(): void {
    if (this.connection) {
      return;
    }

    const hubUrl = `${environment.apiUrl.replace(/\/api$/, '')}/hubs/orders`;

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.authService.token() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('NewOrderReceived', (payload: NewOrderNotification) =>
      this.newOrderReceivedSubject.next(payload)
    );

    connection.start().catch((err) => console.error('Order realtime connection failed to start.', err));
    this.connection = connection;
  }

  private disconnect(): void {
    this.connection?.stop();
    this.connection = null;
  }
}
