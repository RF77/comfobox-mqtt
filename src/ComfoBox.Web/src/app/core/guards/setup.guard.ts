import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';

import { SessionStore } from '../state/session.store';

/**
 * Leitet auf den Assistenten um, solange die Ersteinrichtung offen ist.
 *
 * <p>Ohne Administrator gibt es noch nichts zu bedienen - der Assistent
 * übernimmt die Einrichtung.
 *
 * <p>Der Zustand kommt aus `/api/auth/session` (nicht aus
 * `/api/setup/state`): der Setup-Endpunkt ist nach der Ersteinrichtung
 * geschützt und würde einen 401 liefern.
 */
export const setupGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.loadSession();

  if (session.setupCompleted()) {
    return true;
  }

  return router.createUrlTree(['/setup']);
};
