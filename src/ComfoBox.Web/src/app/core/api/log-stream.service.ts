import { inject, Injectable, signal } from '@angular/core';

import type { LogEntryDto } from '../models/log.model';
import { LogsService } from './logs.service';

/**
 * Der Live-Tail des Log-Viewers (SSE).
 *
 * <p>Der Browser-`EventSource` reconnectet nicht zuverlässig - hier wird der
 * Stream mit Backoff neu aufgebaut.
 */
@Injectable({ providedIn: 'root' })
export class LogStreamService {
  private readonly logs = inject(LogsService);

  /** Der Zustand des Streams. */
  readonly state = signal<'idle' | 'connecting' | 'connected' | 'reconnecting'>('idle');

  /** Der letzte Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  private source: EventSource | null = null;
  private attempt = 0;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private stopped = false;

  /** Startet den Live-Tail. */
  connect(onEntry: (entry: LogEntryDto) => void): void {
    this.stopped = false;
    this.open(onEntry);
  }

  /** Beendet den Live-Tail. */
  disconnect(): void {
    this.stopped = true;

    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }

    this.source?.close();
    this.source = null;
    this.state.set('idle');
  }

  /** Öffnet die Verbindung. */
  private open(onEntry: (entry: LogEntryDto) => void): void {
    if (this.stopped) {
      return;
    }

    this.state.set(this.attempt === 0 ? 'connecting' : 'reconnecting');

    const source = new EventSource(this.logs.streamUrl);
    this.source = source;

    source.addEventListener('open', () => {
      this.attempt = 0;
      this.state.set('connected');
      this.error.set(null);
    });

    source.addEventListener('log', (event) => {
      try {
        onEntry(JSON.parse((event as MessageEvent<string>).data) as LogEntryDto);
      } catch {
        // Ein kaputter Eintrag darf den Stream nicht abbrechen.
      }
    });

    source.addEventListener('error', () => {
      this.error.set('Die Verbindung zum Protokoll wurde unterbrochen.');

      source.close();
      this.source = null;

      if (this.stopped) {
        return;
      }

      this.scheduleReconnect(onEntry);
    });
  }

  /** Plant den nächsten Verbindungsversuch. */
  private scheduleReconnect(onEntry: (entry: LogEntryDto) => void): void {
    this.attempt += 1;
    this.state.set('reconnecting');

    const delay = Math.min(30_000, 2 ** this.attempt * 1000);

    this.timer = setTimeout(() => {
      this.timer = null;
      this.open(onEntry);
    }, delay);
  }
}
