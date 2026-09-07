import { Injectable } from '@angular/core';

// Keeps a dedicated POS/cashier device's screen from sleeping so its SignalR connection
// (see OrderRealtimeService) stays alive for as long as the admin session is open. The
// browser automatically releases a WakeLockSentinel the instant the document is hidden
// (tab switched away, screen locked) - `active` tracks whether the caller still *wants*
// it held, so the visibilitychange listener below knows to silently re-acquire it the
// moment the tab is visible again, without the caller having to re-request it itself.
@Injectable({ providedIn: 'root' })
export class WakeLockService {
  private sentinel: WakeLockSentinel | null = null;
  private active = false;

  get isSupported(): boolean {
    return 'wakeLock' in navigator;
  }

  constructor() {
    document.addEventListener('visibilitychange', () => {
      if (this.active && document.visibilityState === 'visible') {
        void this.acquire();
      }
    });
  }

  async request(): Promise<void> {
    this.active = true;
    await this.acquire();
  }

  release(): void {
    this.active = false;
    this.sentinel?.release().catch(() => {});
    this.sentinel = null;
  }

  private async acquire(): Promise<void> {
    if (!this.isSupported || this.sentinel) {
      return;
    }

    try {
      this.sentinel = await navigator.wakeLock.request('screen');
      this.sentinel.addEventListener('release', () => {
        this.sentinel = null;
      });
    } catch (err) {
      // Unsupported browser, battery saver mode, or no user-activation yet - the cashier
      // just has to keep the screen on manually; nothing else in the app depends on this
      // succeeding.
      console.warn('Screen Wake Lock request failed:', err);
    }
  }
}
