import { inject, Injectable } from '@angular/core';

import { ApiClient } from './api-client';

/** Der Zustand der Zugriffssicherheit (aus `/api/security/settings`). */
export interface SecuritySettingsDto {
  /** Ist die Ersteinrichtung abgeschlossen? */
  readonly setupCompleted: boolean;
  /** Ist ein Administrator eingerichtet? */
  readonly hasAdmin: boolean;
  /** Der Benutzername des Administrators (oder `null`). */
  readonly adminUsername: string | null;
  /** Muss jede Anfrage authentifiziert sein? */
  readonly requireAuthentication: boolean;
}

/** Der Zugriff auf die Sicherheits-Einstellungen. */
@Injectable({ providedIn: 'root' })
export class SecurityService {
  private readonly api = inject(ApiClient);

  /** Liest die Einstellungen (nur als Administrator). */
  async get(): Promise<SecuritySettingsDto> {
    return this.api.get<SecuritySettingsDto>('/security/settings');
  }

  /** Schaltet die Anmelde-Pflicht ein oder aus. */
  async setRequireAuthentication(required: boolean): Promise<void> {
    await this.api.put<void>('/security/settings', { requireAuthentication: required });
  }

  /**
   * Ändert Benutzername und/oder Passwort des Administrators.
   *
   * <p>Das aktuelle Passwort ist Pflicht, sobald ein Administrator existiert -
   * ein gestohlenes Sitzungs-Cookie genügt damit nicht.</p>
   */
  async updateAdmin(
    currentPassword: string | null,
    username: string | null,
    newPassword: string | null,
  ): Promise<void> {
    await this.api.put<void>('/security/admin', {
      currentPassword,
      username,
      newPassword,
    });
  }
}
