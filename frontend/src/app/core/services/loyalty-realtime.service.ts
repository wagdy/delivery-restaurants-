import { Injectable, effect, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
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

  constructor() {
    effect(() => {
      if (this.authService.token()) {
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

    const hubUrl = `${environment.apiUrl.replace(/\/api$/, '')}/hubs/loyalty`;

    const connection = new HubConnectionBuilder()
      .withUrl(hubUrl, { accessTokenFactory: () => this.authService.token() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
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
