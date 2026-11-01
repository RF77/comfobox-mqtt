import { computed, inject, Injectable, signal } from '@angular/core';

import { toApiError } from '../api/api-client';
import { SessionService } from '../api/session.service';
import { SetupService } from '../api/setup.service';
import type { SetupStateDto } from '../models/setup.model';

/**
 * Der Zustand der Sitzung.
 *
 * <p>Die Oberfläche kennt zwei Zustände: die Ersteinrichtung ist offen
 * (<c>setupCompleted = false</c>) oder sie ist abgeschlossen. Im zweiten Fall
 * führt die Oberfläche den Login durch (Cookie-Sitzung).
 */
@Injectable({ providedIn: 'root' })
export class SessionStore {
  private readonly api = inject(SetupService);
  private readonly sessions = inject(SessionService);

  /** Der Zustand der Ersteinrichtung (oder `null`, wenn unbekannt). */
  readonly setup = signal<SetupStateDto | null>(null);

  /** Ist die Ersteinrichtung abgeschlossen? */
  readonly setupCompleted = computed(() => this.setup()?.setupCompleted ?? false);

  /** Ist der Benutzer angemeldet? */
  readonly authenticated = signal(false);

  /** Der Anzeigename des angemeldeten Benutzers (oder `null`). */
  readonly userName = signal<string | null>(null);

  /** Müssen alle Anfragen authentifiziert werden? */
  readonly authenticationRequired = signal(false);

  /** Der Benutzername des Administrators (oder `null`). */
  readonly adminUserName = signal<string | null>(null);

  /** Der letzte Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Wird gerade angemeldet? */
  readonly busy = signal(false);

  /** Wurde der Sitzungszustand schon geladen? */
  private _sessionLoaded = false;

  /** Lädt den Zustand der Ersteinrichtung. */
  async loadSetupState(): Promise<SetupStateDto | null> {
    try {
      const state = await this.api.getState();
      this.setup.set(state);
      return state;
    } catch (error) {
      this.error.set(toApiError(error).message);
      return null;
    }
  }

  /** Lädt den Zustand der Sitzung (aus `/api/auth/session`). */
  async loadSession(force = false): Promise<void> {
    if (!force && this._sessionLoaded) {
      return;
    }

    try {
      const session = await this.sessions.get();

      this._sessionLoaded = true;
      this.authenticated.set(session.authenticated);
      this.userName.set(session.userName);
      this.authenticationRequired.set(session.requireAuthentication);
      this.adminUserName.set(session.adminUsername);

      if (session.setupCompleted) {
        this.setup.update((current) =>
          current ?? { setupCompleted: true, steps: [], version: 1 },
        );
      } else {
        this.setup.set({ setupCompleted: false, steps: [], version: 1 });
      }
    } catch (error) {
      this.error.set(toApiError(error).message);
    }
  }

  /**
   * Meldet sich an.
   *
   * <p>Das Passwort wird nur für diesen einen Aufruf benutzt - es wird
   * **nicht** im Zustand gespeichert.
   */
  async login(username: string, password: string): Promise<boolean> {
    this.busy.set(true);
    this.error.set(null);

    try {
      const session = await this.sessions.login(username, password);

      this._sessionLoaded = true;
      this.authenticated.set(session.authenticated);
      this.userName.set(session.userName);
      this.authenticationRequired.set(session.requireAuthentication);
      this.adminUserName.set(session.adminUsername);

      if (session.setupCompleted) {
        this.setup.set({ setupCompleted: true, steps: [], version: 1 });
      }

      return session.authenticated;
    } catch (error) {
      this.error.set(toApiError(error).message);
      return false;
    } finally {
      this.busy.set(false);
    }
  }

  /** Meldet den Benutzer ab (die Sitzung wird serverseitig beendet). */
  async logout(): Promise<void> {
    this.busy.set(true);

    try {
      await this.sessions.logout();
    } catch (error) {
      this.error.set(toApiError(error).message);
    } finally {
      this._sessionLoaded = false;
      this.authenticated.set(false);
      this.userName.set(null);
      this.busy.set(false);
    }
  }
}
