import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MessageModule } from '@openng/optimus-ui/message';
import { StepsModule } from '@openng/optimus-ui/steps';

import { SetupStore } from '../../core/state/setup.store';

/**
 * Das Grundgerüst des Getting-Started-Assistenten.
 *
 * <p>Die Schritte stehen in einer Liste mit Vor/Zurück - kein linearer
 * Wizard, sondern eine Seitenschritt-Ansicht mit erhaltenem Zustand.
 */
@Component({
  selector: 'app-setup-shell',
  imports: [ButtonModule, MessageModule, RouterLink, RouterOutlet, StepsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './setup-shell.html',
  styleUrl: './setup-shell.scss',
})
export class SetupShellComponent {
  /** Der Zustand des Assistenten. */
  readonly setup = inject(SetupStore);

  private readonly router = inject(Router);

  /** Die Schrittbeschriftungen. */
  readonly labels: Readonly<Record<string, string>> = {
    welcome: 'Willkommen',
    serial: 'Schnittstelle',
    bacnet: 'BACnet',
    mqtt: 'MQTT',
    security: 'Web-Zugang',
    done: 'Fertig',
  };

  /** Der Fortschritt in Prozent (für die Textangabe). */
  readonly progress = computed(() =>
    Math.round(((this.setup.stepIndex() + 1) / this.setup.steps.length) * 100),
  );

  /** Die Schrittliste für die Anzeige. */
  readonly items = computed(() =>
    this.setup.steps.map((step, index) => ({
      label: this.labels[step] ?? step,
      route: `/setup/${step}`,
      active: index === this.setup.stepIndex(),
      completed: index < this.setup.stepIndex(),
    })),
  );

  /** Geht einen Schritt zurück. */
  back(): void {
    this.setup.back();
    void this.router.navigate(['/setup', this.setup.step()]);
  }

  /** Geht einen Schritt vor. */
  next(): void {
    this.setup.next();
    void this.router.navigate(['/setup', this.setup.step()]);
  }
}