import { inject, Injectable } from '@angular/core';

import type { ConfigDto, ConfigUpdateRequest } from '../models/config.model';
import { ApiClient } from './api-client';

/** Der Zugriff auf die Konfigurations-Endpunkte. */
@Injectable({ providedIn: 'root' })
export class ConfigService {
  private readonly api = inject(ApiClient);

  /** Lädt die aktuelle Konfiguration (ohne Secrets). */
  async get(): Promise<ConfigDto> {
    return this.api.get<ConfigDto>('/config');
  }

  /**
   * Ändert die zur Laufzeit änderbaren Konfigurationswerte.
   *
   * <p>Die Werte werden in die `config.json` geschrieben und sind nach einem
   * Neustart wirksam.
   */
  async update(request: ConfigUpdateRequest): Promise<void> {
    await this.api.put<void>('/config', request);
  }
}
