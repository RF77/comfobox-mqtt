import { type Routes } from '@angular/router';

import { authGuard } from './core/guards/auth.guard';
import { setupGuard } from './core/guards/setup.guard';

/**
 * Die Routen der Oberfläche.
 *
 * <ul>
 * <li><c>/setup</c> - der Getting-Started-Assistent (lazy).</li>
 * <li><c>/login</c> - die Anmeldung (lazy).</li>
 * <li><c>/help</c> - die Hilfe (lazy, **ohne** Anmeldung).</li>
 * <li><c>/</c> - die Anwendung (lazy, mit Setup- und Anmeldeprüfung).</li>
 * </ul>
 */
export const routes: Routes = [
  {
    path: 'setup',
    loadChildren: () => import('./features/setup/setup.routes').then((m) => m.SETUP_ROUTES),
  },
  {
    path: 'login',
    loadComponent: () => import('./features/login/login').then((m) => m.LoginComponent),
  },
  {
    path: 'help',
    loadChildren: () => import('./features/help/help.routes').then((m) => m.HELP_ROUTES),
  },
  {
    path: '',
    canActivate: [setupGuard, authGuard],
    loadComponent: () => import('./layout/shell').then((m) => m.ShellComponent),
    children: [
      {
        path: '',
        pathMatch: 'full',
        loadComponent: () =>
          import('./features/dashboard/dashboard').then((m) => m.DashboardComponent),
      },
      {
        path: 'items',
        loadComponent: () => import('./features/items/items').then((m) => m.ItemsComponent),
      },
      {
        path: 'config',
        loadComponent: () => import('./features/config/config').then((m) => m.ConfigComponent),
      },
      {
        path: 'diagnostics',
        loadComponent: () =>
          import('./features/diagnostics/diagnostics').then((m) => m.DiagnosticsComponent),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
