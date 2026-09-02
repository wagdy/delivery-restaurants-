import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AdminModuleName } from '../models/role.model';

// Fixed priority order used both to pick a redirect target when a module guard denies
// access, and as the /admin index redirect - guarantees no redirect loop, since the
// module that just failed is by construction excluded from what this can return.
const ADMIN_MODULE_PATHS: { module: AdminModuleName; path: string }[] = [
  { module: 'Orders', path: 'orders' },
  { module: 'MenuItems', path: 'menu' },
  { module: 'Settings', path: 'settings' },
  { module: 'Staff', path: 'staff' },
  { module: 'Customers', path: 'customers' }
];

export function resolveFirstAccessibleAdminPath(authService: AuthService): string {
  const match = ADMIN_MODULE_PATHS.find((entry) => authService.hasModule(entry.module));
  return match ? `/admin/${match.path}` : '/';
}

export function moduleGuard(module: AdminModuleName): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (authService.hasModule(module)) {
      return true;
    }

    return router.parseUrl(resolveFirstAccessibleAdminPath(authService));
  };
}
