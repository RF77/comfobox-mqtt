import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { ProgressBarModule } from '@openng/optimus-ui/progressbar';

import { SetupStore } from '../../../core/state/setup.store';
import { I18n } from '../../../core/i18n/i18n';

/**
 * Schritt 5: die Zusammenfassung und das Übernehmen.
 */
@Component({
  selector: 'app-setup-done',
  imports: [ButtonModule, CardModule, MessageModule, ProgressBarModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './done.html',
  styleUrl: './steps.scss',
})
export class DoneStep {
  /** Der Zustand des Assistenten. */
  readonly setup = inject(SetupStore);

  private readonly router = inject(Router);

  private readonly i18n = inject(I18n);

  /** Die Texte der gewählten Sprache. */
  readonly t = this.i18n.t;

  /** Die Checkliste. */
  readonly checklist = computed(() => [
    {
      text: `${this.t().setupSteps.checkPort}: ${this.setup.selectedPort() ?? this.t().common.dash}`,
      ok: this.setup.selectedPort() !== null,
    },
    {
      text: `${this.t().setupSteps.checkBacnet}: ${this.setup.bacnetResult()?.ok ? this.t().diagnostics.connected : this.t().diagnostics.disconnected}`,
      ok: this.setup.bacnetResult()?.ok === true,
    },
    {
      text: this.setup.hasBroker()
        ? `${this.t().setupSteps.checkMqtt}: ${this.setup.mqttResult()?.ok ? this.t().diagnostics.connected : this.t().diagnostics.disconnected}`
        : `${this.t().setupSteps.checkMqtt}: ${this.t().common.dash}`,
      ok: this.setup.mqttResult()?.ok === true || !this.setup.hasBroker(),
    },
    {
      text: `${this.t().setupSteps.checkAdmin}: ${this.setup.adminUsername() || this.t().common.dash}`,
      ok: this.setup.adminUsername().trim().length >= 3,
    },
  ]);

  /** Übernimmt die Konfiguration und löst den Neustart aus. */
  async apply(): Promise<void> {
    await this.setup.apply();
  }

  /** Startet den Zustand neu (wiederholbar). */
  restart(): void {
    this.setup.reset();
    void this.router.navigate(['/setup/welcome']);
  }
}
