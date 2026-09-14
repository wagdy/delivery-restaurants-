import { Injectable, effect, inject } from '@angular/core';
// `import type` only - the runtime pieces are pulled in by the dynamic import inside
// connect(). A value import here would put all 55 kB of @microsoft/signalr in the initial
// bundle, downloaded by every anonymous visitor who never signs in and so never opens a
// hub connection at all.
import type { HubConnection } from '@microsoft/signalr';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import { LoyaltyService } from './loyalty.service';
import { CampaignService } from './campaign.service';
import { PointsUpdatedEvent } from '../models/loyalty.model';
import { PunchUpdatedEvent } from '../models/campaign.model';

// Kept alive app-wide (see AppComponent, which injects this once to instantiate the
// singleton eagerly) rather than scoped to the Rewards tab component, so the shared
// LoyaltyService/CampaignService signals stay fresh even while the customer is browsing
// the Menu tab, not just while Rewards is the active tab.
@Injectable({ providedIn: 'root' })
export class LoyaltyRealtimeService {
  private readonly authService = inject(AuthService);
  private readonly loyaltyService = inject(LoyaltyService);
  private readonly campaignService = inject(CampaignService);

  private connection: HubConnection | null = null;

  // Guards against a second connect() starting while the first is still awaiting the
  // dynamic import - the effect below can fire again in that window.
  private connecting = false;

  constructor() {
    effect(() => {
      if (this.authService.token()) {
        void this.connect();
      } else {
        this.disconnect();
      }
    });
  }

  // Async because the SignalR client is fetched on demand. The guard below is re-checked
  // after the await: a customer who signs out while the chunk is still downloading would
  // otherwise end up with a connection nobody asked for, opened with a token that no
  // longer exists.
  private async connect(): Promise<void> {
    if (this.connection || this.connecting) {
      return;
    }

    this.connecting = true;

    let signalR: typeof import('@microsoft/signalr');
    try {
      signalR = await import('@microsoft/signalr');
    } catch (err) {
      this.connecting = false;
      console.error('Loyalty realtime client failed to load.', err);
      return;
    }

    this.connecting = false;

    if (this.connection || !this.authService.token()) {
      return;
    }

    const hubUrl = `${environment.apiUrl.replace(/\/api$/, '')}/hubs/loyalty`;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.authService.token() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(signalR.LogLevel.Warning)
      .build();

    connection.on('PointsUpdated', (event: PointsUpdatedEvent) => this.loyaltyService.applyPointsUpdate(event));
    connection.on('PunchUpdated', (event: PunchUpdatedEvent) => this.campaignService.applyPunchUpdate(event));

    // Self-healing for anything missed while disconnected (a network blip) - push
    // delivery alone can't cover a gap the client wasn't connected for.
    connection.onreconnected(() => {
      this.loyaltyService.getMe().subscribe();
      this.campaignService.getMyProgress().subscribe();
    });

    connection.start().catch((err) => console.error('Loyalty realtime connection failed to start.', err));
    this.connection = connection;
  }

  private disconnect(): void {
    this.connection?.stop();
    this.connection = null;
  }
}
