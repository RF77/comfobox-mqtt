import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { MessageModule } from '@openng/optimus-ui/message';
import { ProgressBarModule } from '@openng/optimus-ui/progressbar';
import { TagModule } from '@openng/optimus-ui/tag';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';

import { toApiError } from '../../core/api/api-client';
import { ServiceService } from '../../core/api/service.service';
import { I18n } from '../../core/i18n/i18n';
import { ConfigStore } from '../../core/state/config.store';
import { HealthStore } from '../../core/state/health.store';
import { ItemsStore } from '../../core/state/items.store';
import { SerialPortPickerComponent } from '../../shared/serial-port-picker';
import { formatNumber, formatUptime } from '../../core/util/format';
import type { ItemDto } from '../../core/models/item.model';

/**
 * Das Dashboard: Health, Verbindungen, Version, Uptime, Schreibzähler und
 * die wichtigsten Bedienelemente.
 */
@Component({
  selector: 'app-dashboard',
  imports: [
    ButtonModule,
    CardModule,
    ConfirmDialogModule,
    MessageModule,
    ProgressBarModule,
    RouterLink,
    SerialPortPickerComponent,
    TagModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class DashboardComponent {
  /** Der Zustand des Gateways. */
  readonly health = inject(HealthStore);

  /** Die Items. */
  readonly items = inject(ItemsStore);

  /** Die Konfiguration (für die Baudrate der Schnittstelle). */
  readonly config = inject(ConfigStore);

  /** Die Sprache der Oberfläche. */
  readonly i18n = inject(I18n);

  /** Die Texte. */
  readonly t = this.i18n.t;

  private readonly service = inject(ServiceService);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  /** Läuft gerade eine Aktion? */
  readonly busy = signal<string | null>(null);

  /** Die Betriebsart (ein häufig genutztes Item). */
  readonly operationMode = computed(() => this.find('zone.operation_mode'));

  /** Der Raumsollwert. */
  readonly setPoint = computed(() => this.find('zone.set_point_normal'));

  /** Der Zustand der Wärmepumpe. */
  readonly heatPump = computed(() => this.find('states.heat_pump.heat_pump_status'));

  /** Die Warmwasser-Temperatur. */
  readonly warmWater = computed(() => this.find('states.warm_water.temperature'));

  /** Die Raumtemperatur. */
  readonly roomTemperature = computed(() => this.find('states.zone.room_temperature'));

  /** Die Außentemperatur. */
  readonly outdoorTemperature = computed(() => this.find('states.zone.outdoor_temperature'));

  /** Der Schreibzähler (Fortschritt 0–1). */
  readonly writeProgress = computed(() => {
    const writes = this.health.status()?.writes;
    if (!writes || writes.limit <= 0) {
      return 0;
    }

    return Math.min(1, writes.count / writes.limit);
  });

  /** Die Anzahl der geschriebenen Werte. */
  readonly writeCount = computed(() => this.health.status()?.writes.count ?? 0);

  /** Das Limit der Schreibvorgänge. */
  readonly writeLimit = computed(() => this.health.status()?.writes.limit ?? 0);

  /** Ist der Schreibzähler fast voll? */
  readonly writeWarning = computed(() => this.writeProgress() >= 0.9);

  /** Die Laufzeit (als Text). */
  readonly uptime = computed(() => formatUptime(this.health.status()?.uptimeSeconds));

  /** Die Version. */
  readonly version = computed(() => this.health.status()?.version ?? this.health.health()?.version ?? '–');

  /** Die Geräte-Id. */
  readonly deviceId = computed(() => this.health.status()?.deviceId ?? '–');

  /** Ist der BACnet-Bus verbunden? */
  readonly bacnetConnected = computed(() => this.health.status()?.bacnet.connected ?? false);

  /** Der serielle Port (Anzeige). */
  readonly bacnetPort = computed(() => this.health.status()?.bacnet.port ?? '–');

  /**
   * Der serielle Port ohne Platzhalter.
   *
   * <p>Der Platzhalter „–" darf **nicht** an die Port-Auswahl gehen - sie
   * würde ihn als echten Port behandeln.
   */
  readonly bacnetPortRaw = computed(() => this.health.status()?.bacnet.port ?? null);

  /** Der letzte BACnet-Fehler. */
  readonly bacnetError = computed(() => this.health.status()?.bacnet.lastError ?? null);

  /**
   * Läuft der Gateway im Simulationsbetrieb?
   *
   * <p>Im Simulationsbetrieb liest der Gateway **keine** ComfoBox - die Werte
   * kommen aus dem Katalog. Das muss in der Oberfläche sichtbar sein, damit
   * niemand die Werte für echte hält.
   */
  readonly bacnetSimulated = computed(() => this.health.status()?.bacnet.simulated ?? false);

  /**
   * Ist der Status schon geladen?
   *
   * <p>Vor dem ersten Abruf der Status-API ist der BACnet-Zustand
   * <b>unbekannt</b>. Der Hinweis „BACnet nicht verbunden" darf erst dann
   * erscheinen, wenn der Gateway wirklich sagt, dass der Bus nicht antwortet -
   * sonst blinkt er beim Start kurz auf.
   */
  readonly statusKnown = computed(() => this.health.status() !== null);

  /** Die eingestellte Baudrate der seriellen Schnittstelle. */
  readonly bacnetBaudrate = computed(() => this.config.config()?.bacnet.baudrate ?? null);

  constructor() {
    // Die Baudrate steht nur in der Konfiguration - sie wird für die
    // Port-Auswahl gebraucht.
    void this.config.load();
  }

  /** Ist MQTT verbunden? */
  readonly mqttConnected = computed(() => this.health.status()?.mqtt.connected ?? false);

  /** Der Broker. */
  readonly broker = computed(() => this.health.status()?.mqtt.broker || '–');

  /** Startet den Gateway neu (mit Bestätigung). */
  restart(): void {
    const texts = this.t().dashboard;

    this.confirm.confirm({
      header: texts.restartTitle,
      message: texts.restartText,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: texts.restartAccept,
      rejectLabel: this.t().common.cancel,
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => void this.run('restart', () => this.service.restart(), texts.restartInfo),
    });
  }

  /** Verbindet die BACnet-Schnittstelle neu. */
  reconnectBacnet(): void {
    void this.run(
      'bacnet',
      () => this.service.reconnectBacnet(),
      this.t().dashboard.reconnectBacnetInfo,
    );
  }

  /** Lädt den Status neu (nach einem Portwechsel). */
  async reload(): Promise<void> {
    await Promise.all([this.health.load(), this.config.load()]);
  }

  /** Verbindet die MQTT-Broker neu. */
  reconnectMqtt(): void {
    void this.run(
      'mqtt',
      () => this.service.reconnectMqtt(),
      this.t().dashboard.reconnectMqttInfo,
    );
  }

  /** Findet ein Item nach seiner Id (ohne Treffer: `null`). */
  private find(id: string): ItemDto | null {
    return this.items.items().find((item) => item.id === id) ?? null;
  }

  /** Führt eine Aktion aus und meldet das Ergebnis. */
  private async run(
    key: string,
    action: () => Promise<void>,
    success: string,
  ): Promise<void> {
    this.busy.set(key);

    try {
      await action();
      this.messages.add({ severity: 'success', summary: success, life: 6000 });
    } catch (error) {
      const apiError = toApiError(error);
      this.messages.add({
        severity: 'error',
        summary: apiError.code,
        detail: apiError.message,
        life: 8000,
      });
    } finally {
      this.busy.set(null);
    }
  }

  /** Der formatierte Zahlenwert eines Items. */
  format(value: number | null | undefined): string {
    return formatNumber(value);
  }
}
