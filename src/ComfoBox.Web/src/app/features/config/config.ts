import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';

import { toApiError } from '../../core/api/api-client';
import { ServiceService } from '../../core/api/service.service';
import { TokensService } from '../../core/api/tokens.service';
import { ConfigStore } from '../../core/state/config.store';
import { SessionStore } from '../../core/state/session.store';
import { formatDateTime } from '../../core/util/format';
import { SerialPortPickerComponent } from '../../shared/serial-port-picker';
import type { CreatedTokenDto, TokenInfoDto } from '../../core/models/token.model';

/**
 * Die Konfigurationsseite.
 *
 * <p>Getrennt in zwei Bereiche: die zur Laufzeit änderbaren Werte (werden
 * direkt gespeichert) und die Felder, die einen **Neustart** erfordern.
 */
@Component({
  selector: 'app-config',
  imports: [
    ButtonModule,
    CardModule,
    CheckboxModule,
    ConfirmDialogModule,
    FormsModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    SerialPortPickerComponent,
    TableModule,
    TagModule,
    ToggleSwitchModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './config.html',
  styleUrl: './config.scss',
})
export class ConfigComponent {
  /** Der Zustand der Konfiguration. */
  readonly config = inject(ConfigStore);

  /** Der Zustand der Sitzung. */
  readonly session = inject(SessionStore);

  private readonly service = inject(ServiceService);
  private readonly tokens = inject(TokensService);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  /** Der Experten-Modus (Entwurf). */
  readonly expertMode = signal(false);

  /** Das Poll-Intervall (Entwurf). */
  readonly pollInterval = signal(3600);

  /** Das Limit der Schreibvorgänge (Entwurf). */
  readonly maxWrites = signal(200);

  /** Das Retain-Verhalten (Entwurf). */
  readonly useRetain = signal(true);

  /** Die API-Token. */
  readonly tokenList = signal<TokenInfoDto[]>([]);

  /** Das neu erzeugte Token (nur einmal sichtbar). */
  readonly createdToken = signal<CreatedTokenDto | null>(null);

  /** Der Name des neuen Tokens. */
  readonly tokenName = signal('home-assistant');

  /** Soll das Token administrative Rechte haben? */
  readonly tokenAdmin = signal(false);

  /** Wird gerade gespeichert? */
  readonly saving = signal(false);

  /** Ist der Experten-Modus aktiv (aus der Konfiguration)? */
  readonly expertModeActive = computed(() => this.config.expertMode());

  constructor() {
    void this.load();
  }

  /** Lädt die Konfiguration und die Token. */
  async load(): Promise<void> {
    await this.config.load();

    const loaded = this.config.config();

    if (loaded) {
      this.expertMode.set(loaded.gateway.expertMode);
      this.pollInterval.set(loaded.gateway.pollIntervalSeconds);
      this.maxWrites.set(loaded.gateway.maxWritesPer24h);
      this.useRetain.set(loaded.mqtt.useMqttRetain);
    }

    await this.loadTokens();
  }

  /** Lädt die Token-Liste. */
  async loadTokens(): Promise<void> {
    try {
      this.tokenList.set(await this.tokens.getAll());
    } catch {
      // Im LAN-Modus darf der Endpunkt offen sein - dann gibt es eben keine
      // Token-Anzeige. Das ist kein Fehler.
      this.tokenList.set([]);
    }
  }

  /** Speichert die zur Laufzeit änderbaren Werte. */
  async save(): Promise<void> {
    this.saving.set(true);

    const ok = await this.config.update({
      expertMode: this.expertMode(),
      pollIntervalSeconds: this.pollInterval(),
      maxWritesPer24h: this.maxWrites(),
      useMqttRetain: this.useRetain(),
    });

    this.saving.set(false);

    if (ok) {
      this.messages.add({
        severity: 'success',
        summary: 'Gespeichert',
        detail: 'Die neuen Werte werden nach einem Neustart wirksam.',
        life: 6000,
      });
      return;
    }

    const error = toApiError(this.config.error());

    this.messages.add({
      severity: 'error',
      summary: error.code,
      detail: error.message,
      life: 8000,
    });
  }

  /** Schaltet den Experten-Modus um (mit Bestätigung). */
  toggleExpertMode(): void {
    const next = !this.expertMode();

    if (!next) {
      this.expertMode.set(false);
      return;
    }

    this.confirm.confirm({
      header: 'Experten-Modus aktivieren',
      message:
        'Der Experten-Modus erlaubt das Schreiben von sensiblen Parametern der Heizung. ' +
        'Ein falscher Wert kann zu Fehlfunktionen führen. Fortfahren?',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Experten-Modus aktivieren',
      rejectLabel: 'Abbrechen',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.expertMode.set(true),
    });
  }

  /** Startet den Gateway neu (mit Bestätigung). */
  restart(): void {
    this.confirm.confirm({
      header: 'Gateway neu starten',
      message:
        'Der Gateway wird neu gestartet. Für etwa 10 Sekunden sind keine Werte verfügbar. ' +
        'Erst danach werden die geänderten Einstellungen wirksam.',
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Neu starten',
      rejectLabel: 'Abbrechen',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => void this.runRestart(),
    });
  }

  /** Erzeugt ein neues API-Token. */
  async createToken(): Promise<void> {
    try {
      const created = await this.tokens.create({
        name: this.tokenName().trim() || 'api-token',
        isAdmin: this.tokenAdmin(),
      });

      this.createdToken.set(created);
      await this.loadTokens();

      this.messages.add({
        severity: 'success',
        summary: 'Token erzeugt',
        detail: 'Der Klartext ist nur jetzt sichtbar - bitte sofort kopieren.',
        life: 10000,
      });
    } catch (error) {
      const apiError = toApiError(error);

      this.messages.add({
        severity: 'error',
        summary: apiError.code,
        detail: apiError.message,
        life: 8000,
      });
    }
  }

  /** Widerruft ein Token (mit Bestätigung). */
  revoke(token: TokenInfoDto): void {
    this.confirm.confirm({
      header: 'Token widerrufen',
      message: `Das Token „${token.name}" wird unwiderruflich widerrufen.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Widerrufen',
      rejectLabel: 'Abbrechen',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => void this.removeToken(token),
    });
  }

  /** Entfernt das Token aus der Liste (nach dem Widerruf). */
  private async removeToken(token: TokenInfoDto): Promise<void> {
    try {
      await this.tokens.remove(token.id);
      await this.loadTokens();

      this.messages.add({
        severity: 'success',
        summary: 'Token widerrufen',
        detail: token.name,
        life: 5000,
      });
    } catch (error) {
      const apiError = toApiError(error);

      this.messages.add({
        severity: 'error',
        summary: apiError.code,
        detail: apiError.message,
        life: 8000,
      });
    }
  }

  /** Führt den Neustart aus. */
  private async runRestart(): Promise<void> {
    try {
      await this.service.restart();

      this.messages.add({
        severity: 'info',
        summary: 'Der Gateway startet neu …',
        detail: 'Die Seite ist gleich nicht mehr erreichbar.',
        life: 8000,
      });
    } catch (error) {
      const apiError = toApiError(error);

      this.messages.add({
        severity: 'error',
        summary: apiError.code,
        detail: apiError.message,
        life: 8000,
      });
    }
  }

  /** Formatiert einen Zeitstempel. */
  formatDate(value: string): string {
    return formatDateTime(value);
  }
}
