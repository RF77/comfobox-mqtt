import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { MessageModule } from '@openng/optimus-ui/message';
import { SelectModule } from '@openng/optimus-ui/select';

import { SetupStore } from '../../../core/state/setup.store';
import { hintText } from './hints';

/**
 * Schritt 2: die BACnet-Konfiguration und der Verbindungstest.
 */
@Component({
  selector: 'app-setup-bacnet',
  imports: [
    ButtonModule,
    CardModule,
    FormsModule,
    InputNumberModule,
    MessageModule,
    RouterLink,
    SelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './bacnet.html',
  styleUrl: './steps.scss',
})
export class BacnetStep {
  /** Der Zustand des Assistenten. */
  readonly setup = inject(SetupStore);

  /** Die unterstützten Baudraten. */
  readonly baudrates = [
    { label: '38400 (empfohlen)', value: 38400 },
    { label: '76800', value: 76800 },
  ];

  /** Der Text zum Fehlerhinweis. */
  hintText(hint: string): string {
    return hintText(hint);
  }
}
