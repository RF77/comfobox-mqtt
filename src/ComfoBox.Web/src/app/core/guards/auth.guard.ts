import { inject } from '@angular/core';
import { type CanActivateFn, Router } from '@angular/router';

import { SessionStore } from '../state/session.store';

/**
 * Leitet auf die Anmeldung um, wenn der Benutzer nicht angemeldet ist.
 *
 * <p>Die Sitzung ist ein serverseitiges Cookie - es gibt **kein** Token im
 * `localStorage` (→ ADR-012, Aufgabe 5.10).
 */
export const authGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.loadSession();

  // Im LAN-Modus verlangt der Gateway keine Anmeldung - dann genügt der
  // Abruf des Sitzungszustands.
  if (session.authenticated() || !session.authenticationRequired()) {
    return true;
  }

  return router.createUrlTree(['/login']);
};
