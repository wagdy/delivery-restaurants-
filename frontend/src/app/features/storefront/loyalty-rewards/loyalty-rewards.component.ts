import { Component } from '@angular/core';
import { MatIconModule } from '@angular/material/icon';

// Placeholder for the Rewards tab - the real loyalty point balance, tier progress, and
// promotional cards live in the separate otantik-loyalty project and aren't wired up
// here yet. Drop those components into this template once that integration happens.
@Component({
  selector: 'app-loyalty-rewards',
  standalone: true,
  imports: [MatIconModule],
  templateUrl: './loyalty-rewards.component.html',
  styleUrl: './loyalty-rewards.component.scss'
})
export class LoyaltyRewardsComponent {}
