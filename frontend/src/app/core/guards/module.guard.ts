import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { AdminModuleName } from '../models/role.model';

// Fixed priority order used both to pick a redirect target when a module guard denies
// access, and as the /admin index redirect - guarantees no redirect loop, since the
// module that just failed is by construction excluded from what this can return.
//
// An entry's `module` can be a single name or an array - an array means "either grants
// access," matching the backend's Module.CustomerInsights policy (which OR's Crm and
// Customers) for the merged Customer Insights page, so a role with only one of those two
// pre-existing, independently-grantable permissions doesn't lose the route.
const ADMIN_MODULE_PATHS: { module: AdminModuleName | AdminModuleName[]; path: string }[] = [
  { module: 'Orders', path: 'orders' },
  { module: 'MenuItems', path: 'menu' },
  { module: 'Settings', path: 'settings' },
  { module: 'Staff', path: 'staff' },
  { module: ['Customers', 'Crm'], path: 'customer-insights' },
  { module: 'Campaigns', path: 'campaigns' },
  { module: 'Scanner', path: 'scanner' },
  { module: 'PromoCodes', path: 'promo-codes' }
];

function hasAnyModule(authService: AuthService, module: AdminModuleName | AdminModuleName[]): boolean {
  const modules = Array.isArray(module) ? module : [module];
  return modules.some((m) => authService.hasModule(m));
}

export function resolveFirstAccessibleAdminPath(authService: AuthService): string {
  const match = ADMIN_MODULE_PATHS.find((entry) => hasAnyModule(authService, entry.module));
  return match ? `/admin/${match.path}` : '/';
}

export function moduleGuard(module: AdminModuleName | AdminModuleName[]): CanActivateFn {
  return () => {
    const authService = inject(AuthService);
    const router = inject(Router);

    if (hasAnyModule(authService, module)) {
      return true;
    }

    return router.parseUrl(resolveFirstAccessibleAdminPath(authService));
  };
}
