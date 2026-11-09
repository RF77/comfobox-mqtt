import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { PasswordModule } from '@openng/optimus-ui/password';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';

import { SetupStore } from '../../../core/state/setup.store';
import { I18n } from '../../../core/i18n/i18n';
import { hintText } from './hints';

/**
 * Schritt 3: der MQTT-Broker.
 */
@Component({
  selector: 'app-setup-mqtt',
  imports: [
    ButtonModule,
    CardModule,
    FormsModule,
    InputNumberModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    RouterLink,
    ToggleSwitchModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './mqtt.html',
  styleUrl: './steps.scss',
})
export class MqttStep {
  /** Der Zustand des Assistenten. */
  readonly setup = inject(SetupStore);

  private readonly i18n = inject(I18n);

  /** Die Texte der gewählten Sprache. */
  readonly t = this.i18n.t;

  /** Der Text zum Fehlerhinweis. */
  hintText(hint: string): string {
    return hintText(this.t().setupSteps, hint);
  }
}
