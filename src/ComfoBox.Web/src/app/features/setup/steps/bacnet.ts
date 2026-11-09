import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { MessageModule } from '@openng/optimus-ui/message';
import { SelectModule } from '@openng/optimus-ui/select';

import { SetupStore } from '../../../core/state/setup.store';
import { I18n } from '../../../core/i18n/i18n';
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

  private readonly i18n = inject(I18n);

  /** Die Texte der gewählten Sprache. */
  readonly t = this.i18n.t;

  /** Die unterstützten Baudraten. */
  readonly baudrates = computed(() => [
    { label: this.t().setupSteps.bacnetRecommended, value: 38400 },
    { label: '76800', value: 76800 },
  ]);

  /** Der Text zum Fehlerhinweis. */
  hintText(hint: string): string {
    return hintText(this.t().setupSteps, hint);
  }
}
