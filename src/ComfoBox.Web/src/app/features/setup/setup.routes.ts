import { type Routes } from '@angular/router';

import { SetupShellComponent } from './setup-shell';

/** Die Routen des Getting-Started-Assistenten. */
export const SETUP_ROUTES: Routes = [
  {
    path: '',
    component: SetupShellComponent,
    children: [
      { path: '', pathMatch: 'full', redirectTo: 'welcome' },
      { path: 'welcome', loadComponent: () => import('./steps/welcome').then((m) => m.WelcomeStep) },
      { path: 'serial', loadComponent: () => import('./steps/serial').then((m) => m.SerialStep) },
      { path: 'bacnet', loadComponent: () => import('./steps/bacnet').then((m) => m.BacnetStep) },
      { path: 'mqtt', loadComponent: () => import('./steps/mqtt').then((m) => m.MqttStep) },
      { path: 'security', loadComponent: () => import('./steps/security').then((m) => m.SecurityStep) },
      { path: 'done', loadComponent: () => import('./steps/done').then((m) => m.DoneStep) },
    ],
  },
];
