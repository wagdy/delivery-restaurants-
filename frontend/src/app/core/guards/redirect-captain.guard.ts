import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';

// A "Captain Order" (delivery driver) account is restricted to the Orders section only —
// every other route (storefront, checkout, my-orders, admin, login/register) bounces them
// back to their dashboard. Applied alongside each route's own guard, not in place of it.
export const redirectCaptainGuard: CanActivateFn = () => {
  const authService = inject(AuthService);
  const router = inject(Router);

  if (authService.isCaptain()) {
    return router.createUrlTree(['/captain']);
  }

  return true;
};
