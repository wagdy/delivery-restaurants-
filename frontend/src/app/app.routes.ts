import { inject } from '@angular/core';
import { Routes } from '@angular/router';
import { adminGuard } from './core/guards/admin.guard';
import { authGuard } from './core/guards/auth.guard';
import { captainGuard } from './core/guards/captain.guard';
import { redirectCaptainGuard } from './core/guards/redirect-captain.guard';
import { moduleGuard, resolveFirstAccessibleAdminPath } from './core/guards/module.guard';
import { AuthService } from './core/services/auth.service';

export const routes: Routes = [
  {
    path: '',
    canActivate: [redirectCaptainGuard],
    loadComponent: () =>
      import('./features/storefront/storefront.component').then((m) => m.StorefrontComponent)
  },
  {
    path: 'checkout',
    canActivate: [redirectCaptainGuard],
    loadComponent: () =>
      import('./features/checkout/checkout.component').then((m) => m.CheckoutComponent)
  },
  {
    path: 'order-confirmation',
    canActivate: [redirectCaptainGuard],
    loadComponent: () =>
      import('./features/checkout/order-confirmation/order-confirmation.component').then(
        (m) => m.OrderConfirmationComponent
      )
  },
  {
    path: 'my-orders',
    canActivate: [authGuard, redirectCaptainGuard],
    loadComponent: () =>
      import('./features/my-orders/my-orders.component').then((m) => m.MyOrdersComponent)
  },
  {
    path: 'login',
    canActivate: [redirectCaptainGuard],
    loadComponent: () => import('./features/auth/auth/auth.component').then((m) => m.AuthComponent)
  },
  {
    path: 'register',
    canActivate: [redirectCaptainGuard],
    data: { initialTab: 'register' },
    loadComponent: () => import('./features/auth/auth/auth.component').then((m) => m.AuthComponent)
  },
  {
    path: 'email-login',
    canActivate: [redirectCaptainGuard],
    loadComponent: () =>
      import('./features/auth/email-login/email-login.component').then((m) => m.EmailLoginComponent)
  },
  {
    path: 'admin',
    canActivate: [adminGuard],
    loadComponent: () =>
      import('./features/admin/admin-layout/admin-layout.component').then(
        (m) => m.AdminLayoutComponent
      ),
    children: [
      {
        path: '',
        pathMatch: 'full',
        redirectTo: () => resolveFirstAccessibleAdminPath(inject(AuthService))
      },
      {
        path: 'orders',
        canActivate: [moduleGuard('Orders')],
        loadComponent: () =>
          import('./features/admin/admin-dashboard/admin-dashboard.component').then(
            (m) => m.AdminDashboardComponent
          )
      },
      {
        path: 'menu',
        canActivate: [moduleGuard('MenuItems')],
        loadComponent: () =>
          import('./features/admin/menu-management/menu-management.component').then(
            (m) => m.MenuManagementComponent
          )
      },
      {
        path: 'settings',
        canActivate: [moduleGuard('Settings')],
        loadComponent: () =>
          import('./features/admin/site-settings/site-settings.component').then(
            (m) => m.SiteSettingsComponent
          )
      },
      {
        path: 'staff',
        canActivate: [moduleGuard('Staff')],
        loadComponent: () =>
          import('./features/admin/staff-accounts/staff-accounts.component').then(
            (m) => m.StaffAccountsComponent
          )
      },
      {
        // Merged "Customer Insights" dashboard (formerly separate Customers/CRM pages) -
        // reachable by either pre-existing module grant, see module.guard.ts.
        path: 'customer-insights',
        canActivate: [moduleGuard(['Customers', 'Crm'])],
        loadComponent: () =>
          import('./features/admin/customer-insights/customer-insights.component').then(
            (m) => m.CustomerInsightsComponent
          )
      },
      {
        path: 'campaigns',
        canActivate: [moduleGuard('Campaigns')],
        loadComponent: () =>
          import('./features/admin/campaign-manager/campaign-manager.component').then(
            (m) => m.CampaignManagerComponent
          )
      },
      {
        path: 'scanner',
        canActivate: [moduleGuard('Scanner')],
        loadComponent: () =>
          import('./features/admin/qr-scanner/qr-scanner.component').then((m) => m.QrScannerComponent)
      },
      {
        path: 'promo-codes',
        canActivate: [moduleGuard('PromoCodes')],
        loadComponent: () =>
          import('./features/admin/promo-codes/promo-codes.component').then((m) => m.PromoCodesComponent)
      }
    ]
  },
  {
    path: 'captain',
    canActivate: [captainGuard],
    loadComponent: () =>
      import('./features/captain/captain-orders.component').then((m) => m.CaptainOrdersComponent)
  },
  {
    path: '**',
    redirectTo: ''
  }
];
