import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatIconModule } from '@angular/material/icon';
import { SettingsService } from '../../../core/services/settings.service';

// Shared premium layout for every customer-facing auth screen (AuthComponent,
// ForgotPasswordComponent - deliberately NOT EmailLoginComponent, the separate
// intentionally lower-key staff/legacy flow this redesign doesn't touch). Split-screen
// on desktop (a warm brand panel beside the form), single-column on mobile (the brand
// panel simply doesn't render - see the SCSS). Projects the actual form markup via
// <ng-content> so each consuming page keeps its own form/validation logic untouched.
//
// The routes this wraps are registered as "bare" pages in AppComponent.isBareUrl(), i.e.
// rendered with no app-wide toolbar/sidenav above them - this component's own brand link
// is what replaces the toolbar's usual "back to the storefront" affordance.
@Component({
  selector: 'app-auth-shell',
  standalone: true,
  imports: [RouterLink, MatIconModule],
  templateUrl: './auth-shell.component.html',
  styleUrl: './auth-shell.component.scss'
})
export class AuthShellComponent {
  protected readonly settingsService = inject(SettingsService);
}
