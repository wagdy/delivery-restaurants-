import { Injectable, effect, signal } from '@angular/core';

export type Language = 'en' | 'ar';

const STORAGE_KEY = 'rd_language';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  // A signal rather than a BehaviorSubject, matching every other piece of shared state in
  // this app (AuthService.currentUser, SettingsService.settings, CartService.lines). It
  // is not only convention: the storefront components run OnPush, and reading a signal in
  // a template marks them dirty on change by itself, where a BehaviorSubject would need
  // an async pipe or a manual subscribe + markForCheck in each one. It also lets
  // LocalNamePipe stay a pure pipe - see the note there.
  private readonly _language = signal<Language>(LanguageService.restore());
  readonly language = this._language.asReadonly();

  // Convenience for templates that only ever ask the yes/no question.
  readonly isArabic = () => this._language() === 'ar';

  constructor() {
    // Runs once on construction as well as on every change, so a reload restores the
    // direction before first paint rather than flashing LTR and flipping.
    effect(() => this.applyToDocument(this._language()));
  }

  set(language: Language): void {
    this._language.set(language);
    try {
      localStorage.setItem(STORAGE_KEY, language);
    } catch {
      // Private browsing and "block site data" both make setItem throw. Losing the
      // preference on reload is a far better outcome than breaking the toggle.
    }
  }

  toggle(): void {
    this.set(this._language() === 'ar' ? 'en' : 'ar');
  }

  private applyToDocument(language: Language): void {
    const isArabic = language === 'ar';
    const root = document.documentElement;

    root.dir = isArabic ? 'rtl' : 'ltr';
    // Separate from dir: screen readers use lang to pick a voice, and it is what
    // :lang() selectors and font stacks key off.
    root.lang = language;

    // Angular Material's Directionality reads the dir attribute, but plain component SCSS
    // cannot - this class is the hook for the handful of rules that need to flip and are
    // written with physical properties (see app.component.scss).
    document.body.classList.toggle('lang-ar', isArabic);
    document.body.classList.toggle('lang-en', !isArabic);
  }

  private static restore(): Language {
    try {
      return localStorage.getItem(STORAGE_KEY) === 'ar' ? 'ar' : 'en';
    } catch {
      // Same storage-unavailable case as set() above; English is the default anyway.
      return 'en';
    }
  }
}
