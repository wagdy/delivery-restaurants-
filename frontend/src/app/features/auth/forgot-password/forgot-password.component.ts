import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';

// Placeholder page for the "Forgot / Change Password?" link on AuthComponent's Login tab
// (see auth.component.html). No OTP/reset-link logic yet - that's a separate, explicit
// follow-up once the delivery channel (WhatsApp via Green API, most likely, matching every
// other customer notification in this app) is decided.
@Component({
  selector: 'app-forgot-password',
  standalone: true,
  imports: [RouterLink, MatCardModule, MatButtonModule, MatIconModule],
  templateUrl: './forgot-password.component.html',
  styleUrl: './forgot-password.component.scss'
})
export class ForgotPasswordComponent {}
