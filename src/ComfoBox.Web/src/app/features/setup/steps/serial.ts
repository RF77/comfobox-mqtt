import { ChangeDetectionStrategy, Component, DestroyRef, inject, type OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { RadioButtonModule } from '@openng/optimus-ui/radiobutton';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';

import { SetupStore } from '../../../core/state/setup.store';

/** Das Intervall der Port-Aktualisierung (Hotplug) in Millisekunden. */
const PORT_REFRESH_MS = 5000;

/**
 * Schritt 1: die serielle Schnittstelle.
 */
@Component({
  selector: 'app-setup-serial',
  imports: [
    ButtonModule,
    CardModule,
    FormsModule,
    MessageModule,
    RadioButtonModule,
    RouterLink,
    ToggleSwitchModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './serial.html',
  styleUrl: './steps.scss',
})
export class SerialStep implements OnInit {
  /** Der Zustand des Assistenten. */
  readonly setup = inject(SetupStore);

  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    void this.setup.reloadPorts();

    // Ein nachträglich eingesteckter Adapter soll ohne Neuladen der Seite
    // in der Liste auftauchen.
    const timer = setInterval(() => {
      void this.setup.reloadPorts();
    }, PORT_REFRESH_MS);

    this.destroyRef.onDestroy(() => clearInterval(timer));
  }
}
