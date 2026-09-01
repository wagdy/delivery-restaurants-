import { Component, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { MatButtonModule } from '@angular/material/button';

const DISMISS_KEY = 'ios_install_prompt_dismissed';

function isIosDevice(): boolean {
  // iPadOS 13+ reports a "MacIntel" platform identical to a real Mac — the only reliable
  // way to tell them apart is that a Mac has no touch points and an iPad does.
  const ua = navigator.userAgent;
  return /iPad|iPhone|iPod/.test(ua) || (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1);
}

function isStandalone(): boolean {
  return (
    (navigator as unknown as { standalone?: boolean }).standalone === true ||
    window.matchMedia('(display-mode: standalone)').matches
  );
}

@Component({
  selector: 'app-ios-install-prompt',
  standalone: true,
  imports: [CommonModule, MatIconModule, MatButtonModule],
  templateUrl: './ios-install-prompt.component.html',
  styleUrl: './ios-install-prompt.component.scss'
})
export class IosInstallPromptComponent {
  // iOS Safari only grants Web Push permission to a PWA that's been added to the Home
  // Screen (running in standalone display mode) — this is an Apple platform requirement,
  // not something any amount of frontend code can work around, so the UI has to ask first.
  readonly shouldShow = signal(isIosDevice() && !isStandalone() && sessionStorage.getItem(DISMISS_KEY) !== 'true');

  dismiss(): void {
    sessionStorage.setItem(DISMISS_KEY, 'true');
    this.shouldShow.set(false);
  }
}
