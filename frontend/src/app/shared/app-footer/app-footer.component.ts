import {
  AfterViewInit,
  Component,
  ElementRef,
  OnDestroy,
  ViewChild,
  inject
} from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatIconModule } from '@angular/material/icon';
import { SettingsService } from '../../core/services/settings.service';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [CommonModule, MatIconModule],
  templateUrl: './app-footer.component.html',
  styleUrl: './app-footer.component.scss'
})
export class AppFooterComponent implements AfterViewInit, OnDestroy {
  protected readonly settingsService = inject(SettingsService);
  protected readonly currentYear = new Date().getFullYear();

  @ViewChild('footerEl') private readonly footerEl?: ElementRef<HTMLElement>;
  private resizeObserver?: ResizeObserver;

  ngAfterViewInit(): void {
    const el = this.footerEl?.nativeElement;
    if (!el) {
      return;
    }

    // The footer is position: fixed (see .app-footer), so page content needs
    // matching bottom padding to avoid rendering underneath it. Its height
    // varies with viewport width (contact info wraps) and which contact
    // fields are set, so it's measured live rather than hardcoded.
    this.resizeObserver = new ResizeObserver(([entry]) => {
      document.documentElement.style.setProperty('--footer-height', `${entry.target.clientHeight}px`);
    });
    this.resizeObserver.observe(el);
  }

  ngOnDestroy(): void {
    this.resizeObserver?.disconnect();
  }
}
