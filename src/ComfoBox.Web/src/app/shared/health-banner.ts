import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MessageModule } from '@openng/optimus-ui/message';
import { RouterLink } from '@angular/router';

import { I18n } from '../core/i18n/i18n';
import { HealthStore } from '../core/state/health.store';
import { formatDateTime } from '../core/util/format';

/**
 * Das Health-Banner.
 *
 * <p>Es steht auf **allen** Seiten (nicht nur im Dashboard) - ein Benutzer,
 * der die Konfiguration öffnet, muss auch sehen, dass die Heizung gerade
 * nicht erreichbar ist.
 */
@Component({
  selector: 'app-health-banner',
  imports: [MessageModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (health.unreachable()) {
      <p-message severity="error" styleClass="w-full cb-banner">
        <p class="font-semibold">{{ t().health.unreachable }}</p>
        <p>{{ t().health.unreachableSince(unreachableSince()) }}</p>
        <p class="mt-2">
          <a routerLink="/help/fehlersuche">{{ t().health.troubleshooting }}</a>
        </p>
      </p-message>
    } @else if (health.degraded()) {
      <p-message severity="warn" styleClass="w-full cb-banner">
        <p class="font-semibold">{{ t().health.degraded }}</p>
        <p>{{ health.message() }}</p>
      </p-message>
    } @else if (health.error(); as message) {
      <p-message severity="error" styleClass="w-full cb-banner">
        <p class="font-semibold">{{ t().health.error }}</p>
        <p>{{ message }}</p>
      </p-message>
    }
  `,
  styles: `
    .cb-banner {
      display: block;
      margin-bottom: 1rem;
    }
  `,
})
export class HealthBannerComponent {
  /** Der Zustand des Gateways. */
  readonly health = inject(HealthStore);

  /** Die Sprache der Oberfläche. */
  readonly i18n = inject(I18n);

  /** Die Texte. */
  readonly t = this.i18n.t;

  /** Der Zeitpunkt, seit dem der Gateway nicht erreichbar ist. */
  readonly unreachableSince = computed(() => {
    const value = this.health.unreachableSince();

    return value ? formatDateTime(value) : this.t().health.unreachableMoment;
  });
}
