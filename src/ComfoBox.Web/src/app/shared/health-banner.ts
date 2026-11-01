import { ChangeDetectionStrategy, Component, computed, inject } from '@angular/core';
import { MessageModule } from '@openng/optimus-ui/message';
import { RouterLink } from '@angular/router';

import { HealthStore } from '../core/state/health.store';

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
        <p class="font-semibold">Gateway nicht erreichbar</p>
        <p>
          Seit {{ unreachableSince() }} kommt keine Antwort vom Gateway. Die Werte unten sind
          die zuletzt bekannten.
        </p>
        <p class="mt-2">
          <a routerLink="/help/fehlersuche">Zur Fehlersuche</a>
        </p>
      </p-message>
    } @else if (health.degraded()) {
      <p-message severity="warn" styleClass="w-full cb-banner">
        <p class="font-semibold">Verbindung eingeschränkt</p>
        <p>{{ health.message() }}</p>
      </p-message>
    } @else if (health.error(); as message) {
      <p-message severity="error" styleClass="w-full cb-banner">
        <p class="font-semibold">Fehler beim Laden der Daten</p>
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

  /** Der Zeitpunkt, seit dem der Gateway nicht erreichbar ist. */
  readonly unreachableSince = computed(() => {
    const value = this.health.unreachableSince();
    return value ? formatDateTime(value) : 'einigen Momenten';
  });
}

/** Formatiert einen Zeitstempel (deutsch). */
function formatDateTime(value: string): string {
  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString('de-DE', { dateStyle: 'short', timeStyle: 'medium' });
}
