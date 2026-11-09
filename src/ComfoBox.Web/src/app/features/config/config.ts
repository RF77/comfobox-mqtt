import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { ConfirmDialogModule } from '@openng/optimus-ui/confirmdialog';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { PasswordModule } from '@openng/optimus-ui/password';
import { SelectModule } from '@openng/optimus-ui/select';
import { TableModule } from '@openng/optimus-ui/table';
import { TagModule } from '@openng/optimus-ui/tag';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';
import { ConfirmationService, MessageService } from '@openng/optimus-ui/api';

import { toApiError } from '../../core/api/api-client';
import { SecurityService } from '../../core/api/security.service';
import { ServiceService } from '../../core/api/service.service';
import { TokensService } from '../../core/api/tokens.service';
import { I18n } from '../../core/i18n/i18n';
import { ConfigStore } from '../../core/state/config.store';
import { SessionStore } from '../../core/state/session.store';
import { formatDateTime } from '../../core/util/format';
import { SerialPortPickerComponent } from '../../shared/serial-port-picker';
import type { CreatedTokenDto, TokenInfoDto, TokenScope } from '../../core/models/token.model';

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
    ConfirmDialogModule,
    FormsModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    SelectModule,
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
  private readonly security = inject(SecurityService);
  private readonly confirm = inject(ConfirmationService);
  private readonly messages = inject(MessageService);

  /** Die Texte der Oberfläche. */
  readonly i18n = inject(I18n);

  /** Die Texte der gewählten Sprache. */
  readonly t = this.i18n.t;

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

  /** Der Umfang des neuen Tokens. */
  readonly tokenScope = signal<TokenScope>('control');

  /**
   * Die Gültigkeitsdauer des neuen Tokens in Tagen.
   *
   * <p>`0` bedeutet: kein Ablaufdatum.</p>
   */
  readonly tokenDays = signal(0);

  /** Die Auswahl der Gültigkeitsdauern (in Tagen). */
  readonly tokenDayOptions = [0, 30, 90, 365];

  /** Die Auswahl der Umfänge. */
  readonly scopeOptions: TokenScope[] = ['read', 'control', 'admin'];

  /** Die Umfänge mit übersetztem Text (für `p-select`). */
  readonly scopeChoices = computed(() =>
    this.scopeOptions.map((scope) => ({ value: scope, label: this.scopeText(scope) })),
  );

  /** Die Gültigkeitsdauern mit übersetztem Text (für `p-select`). */
  readonly dayChoices = computed(() =>
    this.tokenDayOptions.map((days) => ({ value: days, label: this.daysText(days) })),
  );

  /** Muss sich die Oberfläche anmelden? (Entwurf) */
  readonly requireAuth = signal(false);

  /** Der Benutzername des Administrators (Entwurf). */
  readonly adminUserName = signal('');

  /** Das aktuelle Passwort (Entwurf). */
  readonly currentPassword = signal('');

  /** Das neue Passwort (Entwurf). */
  readonly newPassword = signal('');

  /** Die Wiederholung des neuen Passworts (Entwurf). */
  readonly repeatPassword = signal('');

  /** Ist ein Administrator eingerichtet? */
  readonly hasAdmin = signal(false);

  /** Wird gerade gespeichert? */
  readonly saving = signal(false);

  /** Die Passwörter stimmen nicht überein. */
  readonly passwordMismatch = computed(
    () => this.newPassword().length > 0 && this.newPassword() !== this.repeatPassword(),
  );

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
    await this.loadSecurity();
  }

  /** Lädt die Sicherheits-Einstellungen (nur als Administrator möglich). */
  async loadSecurity(): Promise<void> {
    try {
      const settings = await this.security.get();

      this.hasAdmin.set(settings.hasAdmin);
      this.requireAuth.set(settings.requireAuthentication);
      this.adminUserName.set(settings.adminUsername ?? '');
    } catch {
      // Ohne Administratorrechte gibt es keinen Sicherheitsbereich.
      this.hasAdmin.set(this.session.adminUserName() !== null);
      this.requireAuth.set(this.session.authenticationRequired());
      this.adminUserName.set(this.session.adminUserName() ?? '');
    }
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
        summary: this.t().config.saved,
        detail: this.t().config.savedHint,
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
      header: this.t().config.expertModeConfirmTitle,
      message: this.t().config.expertModeConfirmText,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t().config.expertModeConfirmAccept,
      rejectLabel: this.t().common.cancel,
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.expertMode.set(true),
    });
  }

  /** Startet den Gateway neu (mit Bestätigung). */
  restart(): void {
    this.confirm.confirm({
      header: this.t().config.restartTitle,
      message: this.t().config.restartText,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t().config.restartAccept,
      rejectLabel: this.t().common.cancel,
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => void this.runRestart(),
    });
  }

  /** Erzeugt ein neues API-Token. */
  async createToken(): Promise<void> {
    try {
      const created = await this.tokens.create({
        name: this.tokenName().trim() || 'api-token',
        scope: this.tokenScope(),
        expiresInDays: this.tokenDays() === 0 ? null : this.tokenDays(),
      });

      this.createdToken.set(created);
      await this.loadTokens();

      this.messages.add({
        severity: 'success',
        summary: this.t().config.tokenCreated,
        detail: this.t().config.tokenCreatedHint,
        life: 10000,
      });
    } catch (error) {
      this.showError(error);
    }
  }

  /** Widerruft ein Token (mit Bestätigung). */
  revoke(token: TokenInfoDto): void {
    this.confirm.confirm({
      header: this.t().config.tokenRevokeTitle,
      message: this.t().config.tokenRevokeText(token.name),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t().config.tokenRevokeAccept,
      rejectLabel: this.t().common.cancel,
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
        summary: this.t().config.tokenRevokeDone,
        detail: token.name,
        life: 5000,
      });
    } catch (error) {
      this.showError(error);
    }
  }

  /** Speichert die Anmelde-Pflicht. */
  async saveSecurity(): Promise<void> {
    try {
      await this.security.setRequireAuthentication(this.requireAuth());

      this.session.authenticationRequired.set(this.requireAuth());

      this.messages.add({
        severity: 'success',
        summary: this.t().config.settingsSaved,
        detail: this.requireAuth()
          ? this.t().config.requireAuthHint
          : this.t().config.securityHint,
        life: 6000,
      });
    } catch (error) {
      this.showError(error);
    }
  }

  /** Ändert Benutzername und Passwort des Administrators. */
  async changeAdmin(): Promise<void> {
    if (this.passwordMismatch()) {
      this.messages.add({
        severity: 'warn',
        summary: this.t().config.changeAdmin,
        detail: this.t().config.passwordMismatch,
        life: 6000,
      });
      return;
    }

    try {
      await this.security.updateAdmin(
        this.currentPassword() || null,
        this.adminUserName().trim() || null,
        this.newPassword() || null,
      );

      this.session.adminUserName.set(this.adminUserName().trim() || null);

      this.currentPassword.set('');
      this.newPassword.set('');
      this.repeatPassword.set('');

      this.messages.add({
        severity: 'success',
        summary: this.t().config.changeAdmin,
        detail: this.t().config.passwordChanged,
        life: 8000,
      });
    } catch (error) {
      this.showError(error);
    }
  }

  /** Führt den Neustart aus. */
  private async runRestart(): Promise<void> {
    try {
      await this.service.restart();

      this.messages.add({
        severity: 'info',
        summary: this.t().config.restartInfo,
        detail: this.t().config.savedHint,
        life: 8000,
      });
    } catch (error) {
      this.showError(error);
    }
  }

  /** Zeigt einen Fehler der REST-Schnittstelle an. */
  private showError(error: unknown): void {
    const apiError = toApiError(error);

    this.messages.add({
      severity: 'error',
      summary: apiError.code,
      detail: apiError.message,
      life: 8000,
    });
  }

  /** Formatiert einen Zeitstempel. */
  formatDate(value: string): string {
    return formatDateTime(value);
  }

  /** Formatiert einen Ablaufzeitpunkt (oder den Text für „unbegrenzt"). */
  formatExpiry(value: string | null): string {
    return value === null ? this.t().config.tokenNever : formatDateTime(value);
  }

  /** Der Text eines Umfangs. */
  scopeText(scope: TokenScope): string {
    const config = this.t().config;

    switch (scope) {
      case 'read':
        return config.tokenScopeRead;
      case 'admin':
        return config.tokenScopeAdmin;
      default:
        return config.tokenScopeControl;
    }
  }

  /** Der Text einer Gültigkeitsdauer. */
  daysText(days: number): string {
    return days === 0 ? this.t().config.tokenExpiryNever : this.t().config.tokenExpiryDays(days);
  }
}
