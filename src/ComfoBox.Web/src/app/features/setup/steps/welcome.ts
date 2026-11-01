import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';

/**
 * Schritt 0: die Erklärung.
 */
@Component({
  selector: 'app-setup-welcome',
  imports: [CardModule, MessageModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <p-card header="Willkommen">
      <p>
        Dieser Assistent bringt das ComfoBox-Gateway in Betrieb. Er führt Sie in sechs
        Schritten durch die Einrichtung: den seriellen Anschluss, die BACnet-Konfiguration,
        den MQTT-Broker, den Web-Zugang und die Zusammenfassung.
      </p>

      <p class="cb-lead">Sie brauchen dafür:</p>

      <ul class="cb-list">
        <li>die ComfoBox mit ELESTA-Controller,</li>
        <li>einen USB-RS485-Adapter (z. B. FTDI oder CH340),</li>
        <li>ein A/B-Kabel für die Verbindung zur Heizung,</li>
        <li>optional einen MQTT-Broker (z. B. Mosquitto) für Home Assistant.</li>
      </ul>

      <p-message severity="warn" styleClass="w-full cb-hint-block">
        <p class="font-semibold">Wichtig vor dem Start</p>
        <p>
          Die ComfoBox muss auf <b>38400 Baud</b> umgestellt und <b>neu gestartet</b> sein.
          Steht sie noch auf 76800, antwortet sie nicht.
        </p>
        <p>
          <a routerLink="/help/anschluss">Zur Anleitung „Anschluss" (ohne Anmeldung)</a>
        </p>
      </p-message>

      <p-message severity="info" styleClass="w-full cb-hint-block">
        <p>
          Der Assistent ist <b>wiederholbar</b>: Sie können ihn später erneut durchlaufen, um
          den Port oder den Broker zu wechseln.
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
export class WelcomeStep {}
