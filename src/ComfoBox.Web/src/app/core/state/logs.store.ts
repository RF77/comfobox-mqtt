import { computed, inject, Injectable, signal } from '@angular/core';

import { toApiError } from '../api/api-client';
import { LogsService } from '../api/logs.service';
import type { LogEntryDto } from '../models/log.model';

/** Der Zeitbereich des Log-Viewers. */
export type LogRange = 'all' | '5m' | '15m' | '1h';

/** Die Filter des Log-Viewers. */
export interface LogFilters {
  /** Die gewählten Level (leer = alle). */
  readonly levels: readonly string[];
  /** Die Quelle (leer = alle). */
  readonly source: string | null;
  /** Der Volltext (leer = alle). */
  readonly search: string;
  /** Der Zeitbereich. */
  readonly range: LogRange;
}

/**
 * Der Zustand des Log-Viewers.
 *
 * <p>Die Historie kommt aus dem Ringpuffer des Gateways, die Live-Einträge
 * per SSE. Es gibt **kein** Polling.
 */
@Injectable({ providedIn: 'root' })
export class LogsStore {
  private readonly api = inject(LogsService);

  /** Die Einträge (neueste zuerst). */
  readonly entries = signal<LogEntryDto[]>([]);

  /** Die Filter. */
  readonly filters = signal<LogFilters>({
    levels: [],
    source: null,
    search: '',
    range: 'all',
  });

  /** Ist die Historie geladen? */
  readonly loading = signal(false);

  /** Der letzte Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Läuft der Live-Tail? */
  readonly live = signal(false);

  /** Folgt der Auto-Scroll dem Ende? */
  readonly autoScroll = signal(true);

  /** Die bekannten Quellen (für die Quellen-Auswahl). */
  readonly sources = computed<string[]>(() => {
    const seen = new Set<string>();

    for (const entry of this.entries()) {
      seen.add(entry.source);
    }

    return [...seen].sort((a, b) => a.localeCompare(b, 'de'));
  });

  /** Die gefilterten Einträge (die Filter wirken **kombiniert**). */
  readonly visible = computed<LogEntryDto[]>(() => {
    const filters = this.filters();

    return this.entries().filter((entry) => {
      if (filters.levels.length > 0 && !filters.levels.includes(entry.level)) {
        return false;
      }

      if (filters.source && entry.source !== filters.source) {
        return false;
      }

      if (filters.search) {
        const term = filters.search.toLowerCase();

        const inMessage = entry.message.toLowerCase().includes(term);
        const inException = entry.exception?.toLowerCase().includes(term) ?? false;
        const inProperties = Object.entries(entry.properties).some(
          ([key, value]) =>
            key.toLowerCase().includes(term) || value.toLowerCase().includes(term),
        );

        if (!inMessage && !inException && !inProperties) {
          return false;
        }
      }

      if (filters.range !== 'all') {
        const since = sinceFor(filters.range);

        if (since !== null && new Date(entry.timestamp).getTime() < since) {
          return false;
        }
      }

      return true;
    });
  });

  /** Lädt die Historie (ungefiltert - der Server filtert nach Level/Quelle/Text). */
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.entries.set(await this.api.getHistory({ limit: 1000, order: 'desc' }));
    } catch (error) {
      this.error.set(toApiError(error).message);
    } finally {
      this.loading.set(false);
    }
  }

  /** Fügt einen Live-Eintrag hinzu (neueste zuerst). */
  append(entry: LogEntryDto): void {
    this.entries.update((current) => [entry, ...current].slice(0, 2000));
  }

  /** Setzt einen Filter. */
  patchFilters(patch: Partial<LogFilters>): void {
    this.filters.update((current) => ({ ...current, ...patch }));
  }

  /** Setzt alle Filter zurück. */
  resetFilters(): void {
    this.filters.set({ levels: [], source: null, search: '', range: 'all' });
  }

  /** Leert die Liste **und** den Serverpuffer. */
  async clear(): Promise<void> {
    try {
      await this.api.clear();
      this.entries.set([]);
    } catch (error) {
      this.error.set(toApiError(error).message);
    }
  }

  /** Der Export der gefilterten Einträge (als Text). */
  export(): string {
    return this.visible()
      .map((entry) => formatEntry(entry))
      .join('\n');
  }
}

/** Formatiert einen Log-Eintrag (Serilog-ähnlich). */
export function formatEntry(entry: LogEntryDto): string {
  const time = new Date(entry.timestamp).toLocaleTimeString('de-DE', { timeStyle: 'medium' });
  const level = entry.level.padEnd(11, ' ');

  const lines = [`${time} ${level} ${entry.source.padEnd(40, ' ')} ${entry.message}`];

  for (const [key, value] of Object.entries(entry.properties)) {
    lines.push(`${' '.repeat(14)}└─ ${key}=${value}`);
  }

  if (entry.exception) {
    lines.push(`${' '.repeat(14)}└─ Exception: ${entry.exception}`);
  }

  return lines.join('\n');
}

/** Der Startzeitpunkt des Zeitbereichs (oder `null` für „alles"). */
function sinceFor(range: LogRange): number | null {
  const minutes = range === '5m' ? 5 : range === '15m' ? 15 : range === '1h' ? 60 : 0;

  if (minutes === 0) {
    return null;
  }

  return Date.now() - minutes * 60_000;
}
