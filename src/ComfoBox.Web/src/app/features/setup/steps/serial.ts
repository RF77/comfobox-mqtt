import { ChangeDetectionStrategy, Component, inject, type OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { RadioButtonModule } from '@openng/optimus-ui/radiobutton';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';

import { SetupStore } from '../../../core/state/setup.store';

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

  ngOnInit(): void {
    void this.setup.reloadPorts();
  }
}
