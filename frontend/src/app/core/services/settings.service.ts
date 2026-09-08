import { DOCUMENT } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Inject, Injectable, inject, signal } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  RestaurantSettings,
  UpdateRestaurantSettingsRequest
} from '../models/restaurant-settings.model';

const DEFAULT_TAB_TITLE = 'Restaurant Delivery';

// Maps a favicon file's extension to the <link rel="icon"> tag's own `type` attribute -
// browsers are lenient about this and mostly sniff the real content anyway, but setting
// it correctly (rather than always leaving the build-time "image/x-icon" default) avoids
// a stale/misleading type once an admin uploads a PNG or SVG instead of an .ico.
const FAVICON_MIME_TYPES_BY_EXTENSION: Record<string, string> = {
  ico: 'image/x-icon',
  png: 'image/png',
  jpg: 'image/jpeg',
  jpeg: 'image/jpeg',
  svg: 'image/svg+xml'
};

const DEFAULT_SETTINGS: RestaurantSettings = {
  restaurantName: 'Restaurant Delivery',
  logoUrl: null,
  primaryColor: '#3f51b5',
  accentColor: '#ff4081',
  headerColor: '#3f51b5',
  bodyColor: '#fafafa',
  backgroundImageUrl: null,
  centerLogoUrl: null,
  address: null,
  phone: null,
  email: null,
  footerAbout: null,
  faviconUrl: null,
  tabTitle: null,
  taxPercentage: 0,
  isCashEnabled: true,
  isVisaEnabled: false,
  visaFawryUrl: null,
  isInstapayEnabled: false,
  instapayAccount: null,
  baseDeliveryFee: 4.99,
  managerWhatsApp1: null,
  managerWhatsApp2: null,
  managerWhatsApp3: null
};

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/settings`;
  private readonly titleService = inject(Title);

  constructor(@Inject(DOCUMENT) private readonly document: Document) {}

  private readonly _settings = signal<RestaurantSettings>(DEFAULT_SETTINGS);
  readonly settings = this._settings.asReadonly();

  load(): Observable<RestaurantSettings> {
    return this.http.get<RestaurantSettings>(this.baseUrl).pipe(
      tap((settings) => {
        this._settings.set(settings);
        this.applyTheme(settings);
      })
    );
  }

  update(request: UpdateRestaurantSettingsRequest): Observable<RestaurantSettings> {
    return this.http.put<RestaurantSettings>(this.baseUrl, request).pipe(
      tap((settings) => {
        this._settings.set(settings);
        this.applyTheme(settings);
      })
    );
  }

  uploadLogo(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/upload-logo`, formData);
  }

  uploadBackgroundImage(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/upload-background-image`, formData);
  }

  uploadCenterLogo(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/upload-center-logo`, formData);
  }

  uploadFavicon(file: File): Observable<{ url: string }> {
    const formData = new FormData();
    formData.append('file', file);
    return this.http.post<{ url: string }>(`${this.baseUrl}/upload-favicon`, formData);
  }

  private applyTheme(settings: RestaurantSettings): void {
    const root = document.documentElement;
    root.style.setProperty('--app-primary-color', settings.primaryColor);
    root.style.setProperty('--app-accent-color', settings.accentColor);
    root.style.setProperty('--app-header-color', settings.headerColor);
    root.style.setProperty('--app-body-color', settings.bodyColor);
    root.style.setProperty(
      '--app-body-image',
      settings.backgroundImageUrl ? `url('${settings.backgroundImageUrl}')` : 'none'
    );

    // Browser tab text - via Angular's Title service rather than a raw `document.title =`
    // write, so it goes through the same abstraction the rest of Angular (e.g. route
    // title strategies, if this app ever adds one) would use.
    this.titleService.setTitle(settings.tabTitle || DEFAULT_TAB_TITLE);

    this.updateFavicon(settings.faviconUrl ?? null);
  }

  // Locates the <link rel="icon"> tag already in index.html's <head> (see that file for
  // the build-time default) and repoints it at the admin-uploaded favicon - falls back
  // to leaving the existing/default tag alone when no custom favicon is set, rather than
  // clearing href to an empty string and breaking the tab icon entirely.
  private updateFavicon(faviconUrl: string | null): void {
    if (!faviconUrl) {
      return;
    }

    let iconLink = this.document.querySelector<HTMLLinkElement>('link[rel="icon"]');
    if (!iconLink) {
      iconLink = this.document.createElement('link');
      iconLink.rel = 'icon';
      this.document.head.appendChild(iconLink);
    }

    const extension = faviconUrl.split('.').pop()?.toLowerCase().split('?')[0] ?? '';
    iconLink.type = FAVICON_MIME_TYPES_BY_EXTENSION[extension] ?? 'image/x-icon';
    iconLink.href = faviconUrl;
  }
}
