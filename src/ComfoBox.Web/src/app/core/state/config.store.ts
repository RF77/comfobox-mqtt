import { computed, inject, Injectable, signal } from '@angular/core';

import { toApiError } from '../api/api-client';
import { ConfigService } from '../api/config.service';
import type { ConfigDto, ConfigUpdateRequest } from '../models/config.model';

/**
 * Der Zustand der Konfiguration.
 *
 * <p>Die Konfiguration wird **einmal** geladen und danach lokal geändert -
 * die Änderungen werden mit `PUT /api/config` persistiert.
 */
@Injectable({ providedIn: 'root' })
export class ConfigStore {
  private readonly api = inject(ConfigService);

  /** Die aktuelle Konfiguration (oder `null`). */
  readonly config = signal<ConfigDto | null>(null);

  /** Wird gerade geladen? */
  readonly loading = signal(false);

  /** Der letzte Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Wird gerade gespeichert? */
  readonly saving = signal(false);

  /** Ist der Experten-Modus aktiv? */
  readonly expertMode = computed(() => this.config()?.gateway.expertMode ?? false);

  /** Die Felder, die einen Neustart erfordern. */
  readonly restartRequired = computed(() => {
    const config = this.config();
    if (!config) {
      return false;
    }

    return (
      config.bacnet.port.length > 0 &&
      config.mqtt.brokers.some((broker) => broker.host.length > 0)
    );
  });

  /** Lädt die Konfiguration. */
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.config.set(await this.api.get());
    } catch (error) {
      this.error.set(toApiError(error).message);
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Ändert die Konfiguration.
   *
   * <p>Die Änderungen werden serverseitig in die `config.json` geschrieben
   * und sind nach einem Neustart wirksam.
   */
  async update(request: ConfigUpdateRequest): Promise<boolean> {
    this.saving.set(true);
    this.error.set(null);

    try {
      await this.api.update(request);
      await this.load();
      return true;
    } catch (error) {
      this.error.set(toApiError(error).message);
      return false;
    } finally {
      this.saving.set(false);
    }
  }
}
