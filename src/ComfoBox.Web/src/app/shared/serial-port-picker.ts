import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
  type OnInit,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MessageModule } from '@openng/optimus-ui/message';
import { RadioButtonModule } from '@openng/optimus-ui/radiobutton';
import { SelectModule } from '@openng/optimus-ui/select';
import { TagModule } from '@openng/optimus-ui/tag';
import { MessageService } from '@openng/optimus-ui/api';

import { toApiError } from '../core/api/api-client';
import { SerialService } from '../core/api/serial.service';
import { I18n } from '../core/i18n/i18n';
import type { SerialPortInfo, TestResultDto } from '../core/models/setup.model';

/**
 * Die Auswahl des seriellen Ports.
 *
 * <p>Die Komponente wird an zwei Stellen benutzt: auf der Konfigurationsseite
 * und auf der Startseite (dort prominent, wenn BACnet nicht verbunden ist).
 *
 * <p>Die Änderung wirkt **sofort**: der Gateway schreibt die Konfiguration
 * und baut die Verbindung neu auf - ein Neustart ist nicht nötig.
 */
@Component({
  selector: 'app-serial-port-picker',
  imports: [
    ButtonModule,
    FormsModule,
    MessageModule,
    RadioButtonModule,
    SelectModule,
    TagModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './serial-port-picker.html',
  styleUrl: './serial-port-picker.scss',
})
export class SerialPortPickerComponent implements OnInit {
  /** Der aktuell eingestellte Port (für die Anzeige). */
  readonly currentPort = input<string | null>(null);

  /** Die aktuell eingestellte Baudrate (für die Anzeige). */
  readonly currentBaudrate = input<number | null>(null);

  /** Wird ausgelöst, wenn die Schnittstelle umgestellt wurde. */
  readonly applied = output<void>();

  private readonly serial = inject(SerialService);
  private readonly messages = inject(MessageService);

  /** Die Texte der Oberfläche. */
  readonly i18n = inject(I18n);

  /** Die Texte der gewählten Sprache. */
  readonly t = this.i18n.t;

  /** Die gefundenen Ports (roh, ohne den aktuellen). */
  readonly scannedPorts = signal<readonly SerialPortInfo[]>([]);

  /**
   * Die Ports zur Auswahl.
   *
   * <p>Der aktuell eingestellte Port steht **immer** in der Liste - auch
   * wenn das Betriebssystem ihn nicht auflistet (z. B. ein Pfad unter
   * <c>/dev/serial/by-id/</c>).
   */
  readonly ports = computed(() => {
    const current = this.currentPort();
    const scanned = this.scannedPorts();

    if (!current || scanned.some((port) => port.port === current)) {
      return scanned;
    }

    return [
      { port: current, description: this.t().serial.currentOption, stablePath: null },
      ...scanned,
    ];
  });

  /** Der gewählte Port. */
  readonly selected = signal<string | null>(null);

  /** Hat der Benutzer selbst einen Port gewählt? */
  readonly touched = signal(false);

  /** Die gewählte Baudrate. */
  readonly baudrate = signal(38400);

  /** Läuft die Suche nach den Ports? */
  readonly scanning = signal(false);

  /** Läuft der Verbindungstest? */
  readonly testing = signal(false);

  /** Läuft die Umstellung? */
  readonly applying = signal(false);

  /** Das Ergebnis des letzten Tests. */
  readonly result = signal<TestResultDto | null>(null);

  /** Die von BACnet erlaubten Baudraten. */
  readonly baudrates = [9600, 19200, 38400, 57600, 76800, 115200].map((value) => ({
    value,
    label: `${value} Baud`,
  }));

  /** Der Name der Radio-Gruppe (muss je Instanz eindeutig sein). */
  readonly groupName = `serial-${SerialPortPickerComponent.nextId()}`;

  /** Wurde die Einstellung vom Benutzer geändert? */
  readonly changed = computed(
    () =>
      this.selected() !== this.currentPort() || this.baudrate() !== this.currentBaudrate(),
  );

  /** Der aktuell eingestellte Port als Text. */
  readonly currentText = computed(() => {
    const port = this.currentPort();
    const baud = this.currentBaudrate();

    if (!port) {
      return 'nicht eingestellt';
    }

    return baud ? `${port} @ ${baud} Baud` : port;
  });

  /**
   * Wählt einen Port (der Benutzer hat aktiv gewählt).
   *
   * @param port Der gewählte Port.
   */
  select(port: string): void {
    this.touched.set(true);
    this.selected.set(port);
  }

  /**
   * Wählt eine Baudrate (der Benutzer hat aktiv gewählt).
   *
   * @param baudrate Die gewählte Baudrate.
   */
  selectBaudrate(baudrate: number): void {
    this.touched.set(true);
    this.baudrate.set(baudrate);
  }

  /** Der Zähler für die eindeutigen Gruppen-Namen. */
  private static counter = 0;

  async ngOnInit(): Promise<void> {
    await this.scan();
  }

  constructor() {
    // Der Serverwert kann **nach** der Initialisierung eintreffen (der
    // Status lädt asynchron). Solange der Benutzer nichts gewählt hat, folgt
    // die Auswahl dem Serverwert.
    effect(() => {
      const current = this.currentPort();
      const baud = this.currentBaudrate();

      // Hat der Server die Benutzerwahl bestätigt? Dann darf die Oberfläche
      // wieder dem Server folgen.
      if (
        this.touched() &&
        current === this.selected() &&
        baud === this.baudrate()
      ) {
        this.touched.set(false);
        return;
      }

      if (this.touched()) {
        return;
      }

      if (current) {
        this.selected.set(current);
      }

      if (baud) {
        this.baudrate.set(baud);
      }
    });
  }

  /** Sucht die seriellen Ports neu. */
  async scan(): Promise<void> {
    this.scanning.set(true);

    try {
      this.scannedPorts.set(await this.serial.getPorts());
    } catch (error) {
      const apiError = toApiError(error);

      this.messages.add({
        severity: 'error',
        summary: apiError.code,
        detail: apiError.message,
        life: 8000,
      });
    } finally {
      this.scanning.set(false);
    }
  }

  /** Prüft den gewählten Port. */
  async test(): Promise<void> {
    const port = this.selected();
    if (!port) {
      return;
    }

    this.testing.set(true);
    this.result.set(null);

    try {
      this.result.set(await this.serial.test(port, this.baudrate()));
    } catch (error) {
      const apiError = toApiError(error);

      this.messages.add({
        severity: 'error',
        summary: apiError.code,
        detail: apiError.message,
        life: 8000,
      });
    } finally {
      this.testing.set(false);
    }
  }

  /** Übernimmt die Einstellung - wirkt sofort, ohne Neustart. */
  async apply(): Promise<void> {
    const port = this.selected();
    if (!port) {
      return;
    }

    this.applying.set(true);

    try {
      await this.serial.apply(port, this.baudrate());

      this.messages.add({
        severity: 'success',
        summary: 'Schnittstelle umgestellt',
        detail: `Der Gateway benutzt jetzt ${port} @ ${this.baudrate()} Baud. Ein Neustart ist nicht nötig.`,
        life: 7000,
      });

      // Der Server bestätigt die Wahl - dann folgt die Oberfläche wieder
      // seinem Wert (siehe der Effekt im Konstruktor).
      this.applied.emit();
    } catch (error) {
      const apiError = toApiError(error);

      this.messages.add({
        severity: 'error',
        summary: apiError.code,
        detail: apiError.message,
        life: 8000,
      });
    } finally {
      this.applying.set(false);
    }
  }

  /** Erzeugt eine eindeutige Nummer für die Radio-Gruppe. */
  private static nextId(): number {
    SerialPortPickerComponent.counter += 1;
    return SerialPortPickerComponent.counter;
  }
}
