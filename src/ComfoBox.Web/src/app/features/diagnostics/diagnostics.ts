import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { TabsModule } from '@openng/optimus-ui/tabs';

import { HealthService } from '../../core/api/health.service';
import { HealthStore } from '../../core/state/health.store';
import { LogsStore } from '../../core/state/logs.store';
import { formatDateTime, formatRelative, formatUptime } from '../../core/util/format';
import type { ErrorEntryDto } from '../../core/models/status.model';
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
    FormsModule,
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

  /** Die Fehlerhistorie. */
  readonly errors = signal<ErrorEntryDto[]>([]);

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
  stateClass(value: string): string {
    switch (value) {
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
