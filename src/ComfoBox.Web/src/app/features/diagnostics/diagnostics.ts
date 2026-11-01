import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { DecimalPipe, KeyValuePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { TabsModule } from '@openng/optimus-ui/tabs';

import { HealthService } from '../../core/api/health.service';
import { BacnetService } from '../../core/api/bacnet.service';
import { HealthStore } from '../../core/state/health.store';
import { LogsStore } from '../../core/state/logs.store';
import { formatDateTime, formatRelative, formatUptime } from '../../core/util/format';
import type { ErrorEntryDto } from '../../core/models/status.model';
import type { BacnetAnalysis } from '../../core/models/bacnet.model';
import { LogViewerComponent } from './logs/log-viewer';

/**
 * Die Diagnoseseite: Health-Checks, Fehlerhistorie, Schreibvorgänge,
 * Verbindungen und das Protokoll (Log-Viewer).
 */
@Component({
  selector: 'app-diagnostics',
  imports: [
    ButtonModule,
    CardModule,
    DecimalPipe,
    FormsModule,
    KeyValuePipe,
    LogViewerComponent,
    MessageModule,
    TableModule,
    TabsModule,
    TagModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './diagnostics.html',
  styleUrl: './diagnostics.scss',
})
export class DiagnosticsComponent {
  /** Der Zustand des Gateways. */
  readonly health = inject(HealthStore);

  /** Der Zustand des Log-Viewers. */
  readonly logs = inject(LogsStore);

  private readonly api = inject(HealthService);

  private readonly bacnetApi = inject(BacnetService);

  /** Die Fehlerhistorie. */
  readonly errors = signal<ErrorEntryDto[]>([]);

  /** Der aktive Tab der Diagnose-Seite. */
  readonly activeTab = signal('checks');

  /** Die Auswertung der BACnet-Bus-Analyse. */
  readonly bacnet = signal<BacnetAnalysis | null>(null);

  /** Läuft gerade eine Anfrage an die Bus-Analyse? */
  readonly bacnetBusy = signal(false);

  /** Läuft gerade eine Beobachtung? */
  readonly observing = computed(() => this.bacnet()?.observing ?? false);

  /**
   * Die Zähler der Bus-Ereignisse - nur die mit einem Wert größer null.
   *
   * <p>Die Aufbereitung liegt im Signal, nicht im Template: so bleibt der
   * Template-Block flach (kein verschachtelter {@code @for} in einem
   * {@code @if}).
   */
  readonly bacnetCounters = computed(() => {
    const analysis = this.bacnet();

    if (!analysis) {
      return [];
    }

    return Object.entries(analysis.counters)
      .filter(([, value]) => value > 0)
      .map(([kind, value]) => ({ kind, label: this.bacnetKindLabel(kind), value }));
  });

  /** Die gewünschte Beobachtungsdauer in Sekunden. */
  readonly bacnetDuration = signal(15);

  /** Soll das Polling während der Beobachtung pausiert werden? */
  readonly bacnetPausePolling = signal(true);

  /** Die verfügbaren Beobachtungsdauern. */
  readonly bacnetDurations = [5, 10, 15, 30, 60, 120];

  private bacnetTimer: ReturnType<typeof setInterval> | null = null;

  /** Der Schweregrad-Filter. */
  readonly severity = signal<string | null>(null);

  /** Die gefilterte Fehlerhistorie. */
  readonly filteredErrors = computed(() => {
    const severity = this.severity();

    if (!severity) {
      return this.errors();
    }

    return this.errors().filter((entry) => entry.severity === severity);
  });

  /** Die verfügbaren Schweregrade. */
  readonly severities = computed(() => {
    const seen = new Set<string>();

    for (const entry of this.errors()) {
      seen.add(entry.severity);
    }

    return [...seen].sort();
  });

  /** Die Health-Checks. */
  readonly checks = computed(() => this.health.health()?.checks ?? []);

  constructor() {
    void this.load();
    void this.logs.load();
    void this.loadBacnet();

    // Der Aktualisierungs-Timer darf die Seite nicht überleben.
    inject(DestroyRef).onDestroy(() => {
      if (this.bacnetTimer !== null) {
        clearInterval(this.bacnetTimer);
        this.bacnetTimer = null;
      }
    });
  }

  /**
   * Lädt die Auswertung der BACnet-Bus-Analyse.
   *
   * <p>Während einer laufenden Beobachtung wird der Wert regelmäßig
   * nachgeladen - so sieht der Nutzer den Token-Umlauf live.
   */
  async loadBacnet(): Promise<void> {
    try {
      const analysis = await this.bacnetApi.getAnalysis();
      this.bacnet.set(analysis);

      this.updateBacnetTimer(analysis.observing);
    } catch {
      // Die Analyse ist optional - ein Fehler darf die Seite nicht brechen.
    }
  }

  /**
   * Startet eine neue Beobachtung.
   */
  async startBacnetObservation(): Promise<void> {
    this.bacnetBusy.set(true);

    try {
      const analysis = await this.bacnetApi.startObservation({
        durationSeconds: this.bacnetDuration(),
        pausePolling: this.bacnetPausePolling(),
      });

      this.bacnet.set(analysis);
      this.updateBacnetTimer(true);
    } catch {
      // Der Fehler wird unten angezeigt.
    } finally {
      this.bacnetBusy.set(false);
    }
  }

  /**
   * Beendet die Beobachtung - das Polling läuft wieder an.
   */
  async stopBacnetObservation(): Promise<void> {
    this.bacnetBusy.set(true);

    try {
      const analysis = await this.bacnetApi.stopObservation();
      this.bacnet.set(analysis);
      this.updateBacnetTimer(false);
    } catch {
      // Der Fehler wird unten angezeigt.
    } finally {
      this.bacnetBusy.set(false);
    }
  }

  /**
   * Startet oder stoppt den Aktualisierungs-Timer.
   *
   * @param active Läuft eine Beobachtung?
   */
  private updateBacnetTimer(active: boolean): void {
    if (this.bacnetTimer !== null) {
      clearInterval(this.bacnetTimer);
      this.bacnetTimer = null;
    }

    if (!active) {
      return;
    }

    this.bacnetTimer = setInterval(() => void this.loadBacnet(), 2000);
  }

  /**
   * Die deutsche Bezeichnung einer Ereignisart.
   *
   * @param kind Die Ereignisart (englischer Name aus dem Gateway).
   * @returns Die deutsche Bezeichnung.
   */
  bacnetKindLabel(kind: string): string {
    switch (kind) {
      case 'TokenReceived':
        return 'Token übernommen';
      case 'TokenUsed':
        return 'Token benutzt';
      case 'TokenPassed':
        return 'Token weitergegeben';
      case 'PollForMaster':
        return 'Poll-For-Master';
      case 'SoleMaster':
        return 'Alleiniger Master';
      case 'FrameReceived':
        return 'Frame empfangen';
      case 'FrameSent':
        return 'Frame gesendet';
      case 'DataReceived':
        return 'BACnet-Daten empfangen';
      case 'ReplyPosted':
        return 'Reply Postponed';
      default:
        return kind;
    }
  }

  /** Lädt die Diagnose-Daten. */
  async load(): Promise<void> {
    await this.health.load();

    try {
      this.errors.set(await this.api.getErrors());
    } catch {
      this.errors.set([]);
    }
  }

  /** Die Severity eines Fehlers (für die Farbe). */
  severityClass(value: string): string {
    switch (value) {
      case 'error':
        return 'cb-severity-error';
      case 'warning':
        return 'cb-severity-warning';
      default:
        return 'cb-severity-info';
    }
  }

  /** Die Klasse des Health-Checks. */
  stateClass(value: string): string {    switch (value) {
      case 'online':
        return 'cb-state-online';
      case 'degraded':
        return 'cb-state-degraded';
      default:
        return 'cb-state-offline';
    }
  }

  /** Formatiert einen Zeitstempel. */
  formatDate(value: string | null | undefined): string {
    return formatDateTime(value);
  }

  /** Formatiert einen Zeitstempel (relativ). */
  formatRelative(value: string | null | undefined): string {
    return formatRelative(value);
  }

  /** Formatiert eine Laufzeit. */
  formatUptime(seconds: number | null | undefined): string {
    return formatUptime(seconds);
  }
}
