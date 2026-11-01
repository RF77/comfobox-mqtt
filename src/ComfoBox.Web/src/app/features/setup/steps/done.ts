import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { ProgressBarModule } from '@openng/optimus-ui/progressbar';

import { SetupStore } from '../../../core/state/setup.store';

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

  /** Die Checkliste. */
  readonly checklist = computed(() => [
    { text: `Serieller Port: ${this.setup.selectedPort() ?? '–'}`, ok: this.setup.selectedPort() !== null },
    {
      text: `BACnet: ${this.setup.bacnetResult()?.ok ? 'verbunden' : 'nicht geprüft'}`,
      ok: this.setup.bacnetResult()?.ok === true,
    },
    {
      text: `MQTT: ${this.setup.mqttResult()?.ok ? 'verbunden' : 'nicht geprüft'}`,
      ok: this.setup.mqttResult()?.ok === true,
    },
    {
      text: `Administrator: ${this.setup.adminUsername() || '–'}`,
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
