import { inject, Injectable } from '@angular/core';

import type { LogEntryDto } from '../models/log.model';
import { ApiClient } from './api-client';

/** Die Filter des Log-Viewers. */
export interface LogFilter {
  /** Der minimale Level (als Serilog-Level, z. B. `Information`). */
  readonly level?: string | null;
  /** Die Quelle (Teilstring, z. B. `Bacnet`). */
  readonly source?: string | null;
  /** Der Volltext (Meldung, Exception, Properties). */
  readonly search?: string | null;
  /** Der Zeitpunkt, ab dem gefiltert wird. */
  readonly since?: string | null;
  /** Die Höchstzahl der Einträge. */
  readonly limit?: number;
  /** Die Sortierung (`asc` oder `desc`). */
  readonly order?: 'asc' | 'desc';
}

/** Der Zugriff auf die Log-Endpunkte (Diagnose → Reiter „Protokoll"). */
@Injectable({ providedIn: 'root' })
export class LogsService {
  private readonly api = inject(ApiClient);

  /** Die URL des Live-Tails (für den `EventSource`). */
  get streamUrl(): string {
    return this.api.url('/diagnostics/logs/stream');
  }

  /** Lädt die Historie mit den gesetzten Filtern (neueste zuerst). */
  async getHistory(filter: LogFilter = {}): Promise<LogEntryDto[]> {
    const params: Record<string, string | number> = {
      limit: filter.limit ?? 500,
      order: 'desc',
    };

    if (filter.level) {
      params['level'] = filter.level;
    }

    if (filter.source) {
      params['source'] = filter.source;
    }

    if (filter.search) {
      params['search'] = filter.search;
    }

    if (filter.since) {
      params['since'] = filter.since;
    }

    return this.api.get<LogEntryDto[]>('/diagnostics/logs', params);
  }

  /** Leert den Ringpuffer des Gateways. */
  async clear(): Promise<void> {
    await this.api.delete<void>('/diagnostics/logs');
  }
}
