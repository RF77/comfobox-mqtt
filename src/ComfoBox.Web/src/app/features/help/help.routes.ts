import { type Routes } from '@angular/router';

import { HelpIndexComponent } from './help-index';
import { HelpPageComponent } from './help-page';

/** Die Routen des Hilfe-Bereichs (ohne Anmeldung erreichbar). */
export const HELP_ROUTES: Routes = [
  {
    path: '',
    component: HelpIndexComponent,
  },
  {
    path: ':slug',
    component: HelpPageComponent,
  },
];
