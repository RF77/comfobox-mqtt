import {
  ChangeDetectionStrategy,
  Component,
  type ElementRef,
  computed,
  effect,
  inject,
  viewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { KeyValuePipe } from '@angular/common';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { MultiSelectModule } from '@openng/optimus-ui/multiselect';
import { SelectModule } from '@openng/optimus-ui/select';
import { MessageService } from '@openng/optimus-ui/api';

import { LogStreamService } from '../../../core/api/log-stream.service';
import { LogsStore } from '../../../core/state/logs.store';
import { LOG_LEVELS, type LogEntryDto } from '../../../core/models/log.model';
import { formatTime } from '../../../core/util/format';

/**
 * Der Log-Viewer: die letzten Einträge aus dem Ringpuffer, optional als
 * Live-Tail.
 */
@Component({
  selector: 'app-log-viewer',
  imports: [
    ButtonModule,
    CheckboxModule,
    FormsModule,
    InputTextModule,
    KeyValuePipe,
    MessageModule,
    MultiSelectModule,
    SelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './log-viewer.html',
  styleUrl: './log-viewer.scss',
})
export class LogViewerComponent {
  /** Der Zustand des Log-Viewers. */
  readonly logs = inject(LogsStore);

  private readonly stream = inject(LogStreamService);
  private readonly messages = inject(MessageService);

  /** Die Auswahl der Level. */
  readonly levelOptions = LOG_LEVELS.map((level) => ({ label: level, value: level }));

  /** Die Auswahl der Zeitbereiche. */
  readonly rangeOptions = [
    { label: 'Alles', value: 'all' },
    { label: 'Letzte 5 Minuten', value: '5m' },
    { label: 'Letzte 15 Minuten', value: '15m' },
    { label: 'Letzte Stunde', value: '1h' },
  ];
  /** Der Bereich, in dem der Auto-Scroll greift. */
  private readonly viewport = viewChild<ElementRef<HTMLDivElement>>('viewport');

  /** Der Zustand des Live-Streams. */
  readonly streamState = this.stream.state;

  constructor() {
    // Der Live-Tail wird über den Schalter gesteuert - die Verbindung wird
    // nur aufgebaut, wenn der Nutzer ihn auch sehen will.
    effect(() => {
      if (this.logs.live()) {
        this.stream.connect((entry) => this.logs.append(entry));
      } else {
        this.stream.disconnect();
      }
    });

    // Der Auto-Scroll folgt dem Ende, solange er aktiv ist.
    effect(() => {
      const entries = this.logs.visible();

      if (!this.logs.autoScroll() || entries.length === 0) {
        return;
      }

      const element = this.viewport()?.nativeElement;

      if (element) {
        element.scrollTop = element.scrollHeight;
      }
    });
  }

  /** Schaltet den Live-Tail um. */
  toggleLive(): void {
    this.logs.live.update((value) => !value);
  }

  /** Lädt die Historie neu. */
  reload(): void {
    void this.logs.load();
  }

  /** Setzt alle Filter zurück. */
  reset(): void {
    this.logs.resetFilters();
  }

  /** Leert die Liste **und** den Serverpuffer. */
  async clear(): Promise<void> {
    await this.logs.clear();

    this.messages.add({
      severity: 'success',
      summary: 'Protokoll geleert',
      detail: 'Der Ringpuffer des Gateways ist leer.',
      life: 4000,
    });
  }

  /** Exportiert die gefilterten Einträge als `.log`-Datei. */
  export(): void {
    const content = this.logs.export();

    const blob = new Blob([content], { type: 'text/plain;charset=utf-8' });
    const url = URL.createObjectURL(blob);

    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `comfobox-${stamp}.log`;
    anchor.click();

    URL.revokeObjectURL(url);

    this.messages.add({
      severity: 'success',
      summary: 'Export erstellt',
      detail: `${this.logs.visible().length} Einträge exportiert.`,
      life: 4000,
    });
  }

  /** Kopiert einen einzelnen Eintrag (inklusive Exception). */
  async copy(entry: LogEntryDto): Promise<void> {
    const text = [
      `${entry.timestamp} ${entry.level} ${entry.source}`,
      entry.message,
      ...Object.entries(entry.properties).map(([key, value]) => `${key}=${value}`),
      ...(entry.exception ? [entry.exception] : []),
    ].join('\n');

    try {
      await navigator.clipboard.writeText(text);

      this.messages.add({
        severity: 'success',
        summary: 'Kopiert',
        detail: 'Der Eintrag liegt in der Zwischenablage.',
        life: 3000,
      });
    } catch {
      this.messages.add({
        severity: 'error',
        summary: 'Kopieren nicht möglich',
        detail: 'Der Browser hat den Zugriff auf die Zwischenablage verweigert.',
        life: 5000,
      });
    }
  }

  /** Der Zeitstempel (als Text). */
  formatTime(value: string): string {
    return formatTime(value);
  }

  /** Die Klasse für die Level-Farbe. */
  levelClass(level: string): string {
    switch (level) {
      case 'Debug':
      case 'Verbose':
        return 'cb-level-debug';
      case 'Information':
        return 'cb-level-info';
      case 'Warning':
        return 'cb-level-warn';
      case 'Error':
        return 'cb-level-error';
      case 'Fatal':
        return 'cb-level-fatal';
      default:
        return '';
    }
  }

  /** Die Anzahl der angezeigten Einträge. */
  readonly count = computed(() => this.logs.visible().length);
}
