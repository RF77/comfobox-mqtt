import { inject, Injectable } from '@angular/core';

import { ApiClient } from './api-client';

/** Der Zustand der Web-Sitzung (aus `/api/auth/session`). */
export interface SessionDto {
  /** Ist der Benutzer angemeldet? */
  readonly authenticated: boolean;
  /** Der Benutzername des angemeldeten Benutzers (oder `null`). */
  readonly userName: string | null;
  /** Ist die Ersteinrichtung abgeschlossen? */
  readonly setupCompleted: boolean;
  /** Müssen alle Anfragen authentifiziert werden? */
  readonly requireAuthentication: boolean;
  /** Der Benutzername des Administrators (oder `null`). */
  readonly adminUsername: string | null;
}

/** Der Zugriff auf die Anmelde-Endpunkte. */
@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly api = inject(ApiClient);

  /** Liest den Zustand der Sitzung. */
  async get(): Promise<SessionDto> {
    return this.api.get<SessionDto>('/auth/session');
  }

  /**
   * Meldet sich an.
   *
   * <p>Das Passwort wird **einmal** im Klartext gesendet - der Server hasht
   * es (PBKDF2) und gibt ein HttpOnly-Cookie zurück. Es wird nirgends
   * gespeichert.
   */
  async login(username: string, password: string): Promise<SessionDto> {
    return this.api.post<SessionDto>('/auth/login', { username, password });
  }

  /** Meldet ab (die Sitzung wird serverseitig beendet). */
  async logout(): Promise<void> {
    await this.api.post<void>('/auth/logout');
  }
}
