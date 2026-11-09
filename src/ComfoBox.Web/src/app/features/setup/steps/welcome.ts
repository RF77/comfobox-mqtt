import { ChangeDetectionStrategy, Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';

import { I18n } from '../../../core/i18n/i18n';

/**
 * Schritt 0: die Erklärung.
 */
@Component({
  selector: 'app-setup-welcome',
  imports: [CardModule, MessageModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-card [header]="t().setupSteps.welcomeCard">
      <p>
        {{ t().setupSteps.welcomeText }}
      </p>

      <p class="cb-lead">{{ t().setupSteps.welcomeNeeds }}</p>

      <ul class="cb-list">
        <li>{{ t().setupSteps.welcomeItem1 }}</li>
        <li>{{ t().setupSteps.welcomeItem2 }}</li>
        <li>{{ t().setupSteps.welcomeItem3 }}</li>
        <li>{{ t().setupSteps.welcomeItem4 }}</li>
      </ul>

      <p-message severity="warn" styleClass="w-full cb-hint-block">
        <p class="font-semibold">{{ t().setupSteps.welcomeWarnTitle }}</p>
        <p>
          {{ t().setupSteps.welcomeWarnText }}
        </p>
        <p>
          <a routerLink="/help/anschluss">{{ t().setupSteps.welcomeHelpLink }}</a>
        </p>
      </p-message>

      <p-message severity="info" styleClass="w-full cb-hint-block">
        <p>
          <b>{{ t().setupSteps.welcomeRepeatTitle }}</b> {{ t().setupSteps.welcomeRepeatText }}
        </p>
      </p-message>
    </p-card>
  `,
  styles: `
    .cb-lead {
      margin-bottom: 0.25rem;
      font-weight: 600;
    }

    .cb-list {
      margin-top: 0;
      padding-left: 1.25rem;
    }

    .cb-hint-block {
      display: block;
      margin-top: 1rem;
    }
  `,
})
export class WelcomeStep {
  /** Die Texte der gewählten Sprache. */
  readonly t = inject(I18n).t;
}
