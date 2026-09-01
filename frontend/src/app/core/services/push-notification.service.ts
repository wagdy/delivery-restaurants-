import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { SwPush } from '@angular/service-worker';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

interface VapidPublicKeyResponse {
  publicKey: string;
}

@Injectable({ providedIn: 'root' })
export class PushNotificationService {
  private readonly http = inject(HttpClient);
  private readonly swPush = inject(SwPush);
  private readonly router = inject(Router);
  private readonly baseUrl = `${environment.apiUrl}/push`;

  private listeningForClicks = false;

  get isSupported(): boolean {
    return this.swPush.isEnabled;
  }

  /**
   * Requests notification permission (if not already granted/denied) and registers the
   * subscription with the backend. Safe to call repeatedly — it's a no-op once subscribed
   * and permission has already been decided, so callers don't need to track state themselves.
   */
  async subscribe(): Promise<void> {
    if (!this.isSupported) {
      return;
    }

    this.listenForNotificationClicksOnce();

    try {
      const { publicKey } = await firstValueFrom(
        this.http.get<VapidPublicKeyResponse>(`${this.baseUrl}/vapid-public-key`)
      );

      const subscription = await this.swPush.requestSubscription({ serverPublicKey: publicKey });

      await firstValueFrom(this.http.post(`${this.baseUrl}/subscribe`, subscription.toJSON()));
    } catch (err) {
      // Permission denied, unsupported browser, or a network hiccup — none of this should
      // ever block the captain from using the rest of the app, just log for diagnosis.
      console.warn('Push subscription failed:', err);
    }
  }

  async unsubscribe(): Promise<void> {
    if (!this.isSupported) {
      return;
    }

    const subscription = await firstValueFrom(this.swPush.subscription);
    if (!subscription) {
      return;
    }

    const endpoint = subscription.endpoint;
    await this.swPush.unsubscribe();
    await firstValueFrom(this.http.post(`${this.baseUrl}/unsubscribe`, { endpoint }));
  }

  /**
   * While the app is open in the foreground, Angular's service worker still fires
   * notificationClicks — handle it the same way the backend's onActionClick payload
   * tells a closed app to behave, so behavior is consistent either way.
   */
  private listenForNotificationClicksOnce(): void {
    if (this.listeningForClicks) {
      return;
    }
    this.listeningForClicks = true;

    this.swPush.notificationClicks.subscribe(({ notification }) => {
      const orderId = (notification.data as { orderId?: number } | undefined)?.orderId;
      this.router.navigate(['/captain'], orderId ? { queryParams: { orderId } } : {});
    });
  }
}
