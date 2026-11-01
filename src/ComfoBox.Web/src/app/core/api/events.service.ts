import { inject, Injectable, signal } from '@angular/core';

import type { CachedValueDto } from '../models/item.model';
import { ApiClient } from './api-client';

/** Der Zustand des SSE-Streams. */
export type StreamState = 'idle' | 'connecting' | 'connected' | 'reconnecting';

/**
 * Der SSE-Client für die Live-Werte.
 *
 * <p>Der Browser-`EventSource` reconnectet bei einem Server-Absturz **nicht**
 * zuverlässig - deshalb wird der Stream hier selbst neu aufgebaut, mit
 * exponentiellem Backoff (maximal 30 s).
 */
@Injectable({ providedIn: 'root' })
export class EventsService {
  private readonly api = inject(ApiClient);

  /** Die zuletzt empfangenen Werte (nach Id). */
  readonly values = signal<ReadonlyMap<string, CachedValueDto>>(new Map());

  /** Der Zustand des Streams. */
  readonly state = signal<StreamState>('idle');

  /** Der letzte Fehler (oder `null`). */
  readonly lastError = signal<string | null>(null);

  private source: EventSource | null = null;
  private attempt = 0;
  private reconnectTimer: ReturnType<typeof setTimeout> | null = null;
  private stopped = false;

  /** Startet den Stream (idempotent). */
  connect(): void {
    this.stopped = false;
    this.open();
  }

  /** Beendet den Stream. */
  disconnect(): void {
    this.stopped = true;

    if (this.reconnectTimer !== null) {
      clearTimeout(this.reconnectTimer);
      this.reconnectTimer = null;
    }

    this.source?.close();
    this.source = null;
    this.state.set('idle');
  }

  /** Öffnet die Verbindung (und richtet die Ereignishandler ein). */
  private open(): void {
    if (this.stopped) {
      return;
    }

    this.state.set(this.attempt === 0 ? 'connecting' : 'reconnecting');

    const source = new EventSource(this.api.url('/events'));
    this.source = source;

    source.addEventListener('open', () => {
      this.attempt = 0;
      this.state.set('connected');
      this.lastError.set(null);
    });

    source.addEventListener('snapshot', (event) => {
      const payload = this.parse<CachedValueDto[]>(event as MessageEvent<string>);
      if (!payload) {
        return;
      }

      const map = new Map<string, CachedValueDto>();
      for (const entry of payload) {
        map.set(entry.id, entry);
      }

      this.values.set(map);
    });

    source.addEventListener('value', (event) => {
      const payload = this.parse<CachedValueDto>(event as MessageEvent<string>);
      if (!payload) {
        return;
      }

      this.values.update((current) => {
        const next = new Map(current);
        next.set(payload.id, payload);
        return next;
      });
    });

    source.addEventListener('error', () => {
      this.lastError.set('Die Verbindung zum Gateway wurde unterbrochen.');

      source.close();
      this.source = null;

      if (this.stopped) {
        return;
      }

      this.scheduleReconnect();
    });
  }

  /** Plant den nächsten Verbindungsversuch (exponentieller Backoff). */
  private scheduleReconnect(): void {
    this.attempt += 1;
    this.state.set('reconnecting');

    const delay = Math.min(30_000, 2 ** this.attempt * 1000);

    this.reconnectTimer = setTimeout(() => {
      this.reconnectTimer = null;
      this.open();
    }, delay);
  }

  /** Liest die Nutzlast eines SSE-Ereignisses (robust gegen Fehlermeldungen). */
  private parse<T>(event: MessageEvent<string>): T | null {
    try {
      return JSON.parse(event.data) as T;
    } catch {
      return null;
    }
  }
}
