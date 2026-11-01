import { computed, effect, inject, Injectable, signal } from '@angular/core';

import { toApiError } from '../api/api-client';
import { EventsService } from '../api/events.service';
import { HealthService } from '../api/health.service';
import type { HealthReport, HealthState, StatusReport } from '../models/status.model';

/** Der Schlüssel, unter dem der letzte bekannte Status im Browser gespeichert wird. */
const CACHE_KEY = 'comfobox.status';

/** Der zwischengespeicherte Zustand (für den Fall, dass der Server weg ist). */
interface CachedStatus {
  readonly state: HealthState;
  readonly message: string;
  readonly at: string;
}

/**
 * Der Zustand des Gateways (Health, Verbindungen, Schreibzähler).
 *
 * <p>Der letzte bekannte Zustand wird im `localStorage` gespiegelt: ist der
 * Server nicht erreichbar, zeigt die Oberfläche trotzdem, was zuletzt
 * bekannt war - mit dem Zusatz „seit … nicht erreichbar".
 */
@Injectable({ providedIn: 'root' })
export class HealthStore {
  private readonly api = inject(HealthService);
  private readonly events = inject(EventsService);

  /** Der Health-Bericht (oder `null`, wenn er noch nicht geladen wurde). */
  readonly health = signal<HealthReport | null>(null);

  /** Der Laufzeitstatus (oder `null`). */
  readonly status = signal<StatusReport | null>(null);

  /** Der Zustand - aus dem Health-Bericht oder aus dem Cache. */
  readonly state = signal<HealthState>('online');

  /** Die Meldung zum Zustand. */
  readonly message = signal<string>('');

  /** Wird gerade geladen? */
  readonly loading = signal(false);

  /** Der letzte Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Ist der Server nicht erreichbar? */
  readonly unreachable = signal(false);

  /** Seit wann ist der Server nicht erreichbar (oder `null`). */
  readonly unreachableSince = signal<string | null>(null);

  /** Ist der Zustand „degraded"? */
  readonly degraded = computed(() => this.state() === 'degraded');

  /** Ist der Zustand „offline"? */
  readonly offline = computed(() => this.state() === 'offline');

  /** Ist der Zustand einwandfrei? */
  readonly healthy = computed(() => this.state() === 'online');

  /** Die verfügbaren Schnellaktionen (nur wenn der Gateway erreichbar ist). */
  readonly canAct = computed(() => !this.unreachable());

  constructor() {
    // Der SSE-Stream bricht ab, wenn der Server weg ist - das ist das Signal
    // für das Offline-Banner.
    effect(() => {
      const streamState = this.events.state();

      if (streamState === 'reconnecting' && !this.unreachable()) {
        this.unreachable.set(true);
        this.unreachableSince.set(new Date().toISOString());
      }
    });
  }

  /** Lädt Health und Status. */
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const health = await this.api.getHealth();
      this.health.set(health);
      this.state.set(health.state);
      this.message.set(health.message);
      this.unreachable.set(false);
      this.unreachableSince.set(null);
      this.persist(health.state, health.message);

      try {
        this.status.set(await this.api.getStatus());
      } catch (error) {
        this.error.set(toApiError(error).message);
      }
    } catch (error) {
      this.error.set(toApiError(error).message);
      this.markUnreachable();
    } finally {
      this.loading.set(false);
    }
  }

  /** Merkt sich, dass der Server nicht erreichbar ist. */
  markUnreachable(): void {
    this.unreachable.set(true);

    if (this.unreachableSince() === null) {
      this.unreachableSince.set(new Date().toISOString());
    }

    // Der letzte bekannte Zustand bleibt sichtbar - aus dem Cache.
    const cached = this.readCache();
    if (cached) {
      this.state.set(cached.state);
      this.message.set(cached.message);
    } else {
      this.state.set('offline');
      this.message.set('Der Gateway ist nicht erreichbar.');
    }
  }

  /** Liest den letzten bekannten Zustand aus dem `localStorage`. */
  readCache(): CachedStatus | null {
    try {
      const raw = localStorage.getItem(CACHE_KEY);
      if (!raw) {
        return null;
      }

      const parsed = JSON.parse(raw) as Partial<CachedStatus>;

      if (parsed.state !== 'online' && parsed.state !== 'degraded' && parsed.state !== 'offline') {
        return null;
      }

      return {
        state: parsed.state,
        message: typeof parsed.message === 'string' ? parsed.message : '',
        at: typeof parsed.at === 'string' ? parsed.at : '',
      };
    } catch {
      return null;
    }
  }

  /** Schreibt den Zustand in den `localStorage`. */
  private persist(state: HealthState, message: string): void {
    try {
      localStorage.setItem(
        CACHE_KEY,
        JSON.stringify({ state, message, at: new Date().toISOString() }),
      );
    } catch {
      // Der Browser verbietet den Speicher (z. B. privater Modus) - das ist
      // kein Fehler: die Oberfläche funktioniert auch ohne Cache.
    }
  }
}
