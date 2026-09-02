import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import {
  RestaurantSettings,
  UpdateRestaurantSettingsRequest
} from '../models/restaurant-settings.model';

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
  footerAbout: null
};

@Injectable({ providedIn: 'root' })
export class SettingsService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.apiUrl}/settings`;

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
    document.title = settings.restaurantName;
  }
}
