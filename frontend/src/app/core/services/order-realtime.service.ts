import { Injectable, effect, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
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

  // Fires after a foreground-recovery check (see the visibilitychange listener below)
  // confirms/restores the connection - the admin dashboard listens to this to refetch
  // orders it may have missed while backgrounded, without the cashier pressing refresh.
  // Deliberately does NOT fire on the very first connect (the auth effect below) since
  // every consumer already does its own initial load; it's specifically the
  // foreground-recovery signal called out by this feature's own requirements.
  private readonly connectionRestoredSubject = new Subject<void>();
  readonly connectionRestored = this.connectionRestoredSubject.asObservable();

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

    // Mobile OS background limits can fully kill the WebSocket (and freeze
    // withAutomaticReconnect's own retry timers along with it) while the tab is hidden -
    // screen-locked or backgrounded - in a way that isn't guaranteed to self-heal the
    // instant the cashier looks at the screen again. Checking explicitly on every
    // foreground transition, rather than trusting automatic reconnect alone, is what
    // actually guarantees no order gets missed.
    document.addEventListener('visibilitychange', () => {
      if (document.visibilityState === 'visible') {
        void this.ensureConnected();
      }
    });
  }

  // Reconnects if the connection was dropped, then always signals connectionRestored -
  // even when the socket reports itself as already "Connected", a background tab's
  // messages can still have been silently dropped by the OS, so the safest contract is
  // "foreground recovery always re-syncs," not "only when a reconnect was needed."
  private async ensureConnected(): Promise<void> {
    if (!this.authService.token() || !this.authService.isAdmin()) {
      return;
    }

    if (!this.connection) {
      this.connect();
      return;
    }

    if (this.connection.state === HubConnectionState.Disconnected) {
      try {
        await this.connection.start();
      } catch (err) {
        console.error('Order realtime reconnect failed on foreground recovery.', err);
        return;
      }
    }

    this.connectionRestoredSubject.next();
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
