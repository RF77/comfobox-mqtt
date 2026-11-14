import { ChangeDetectionStrategy, Component, DestroyRef, inject, type OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { RadioButtonModule } from '@openng/optimus-ui/radiobutton';
import { TagModule } from '@openng/optimus-ui/tag';
import { ToggleSwitchModule } from '@openng/optimus-ui/toggleswitch';

import type { SerialPortInfo } from '../../../core/models/setup.model';
import { SetupStore } from '../../../core/state/setup.store';
import { I18n } from '../../../core/i18n/i18n';

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
    TagModule,
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

  /** Die Texte der gewählten Sprache. */
  readonly t = inject(I18n).t;

  /**
   * Die Art des Ports als Text.
   *
   * @param port Der Port.
   * @returns Die Beschriftung.
   */
  kindLabel(port: SerialPortInfo): string {
    switch (port.kind) {
      case 'usb':
        return this.t().serial.kindUsb;
      case 'builtin':
        return this.t().serial.kindBuiltin;
      default:
        return this.t().serial.kindOther;
    }
  }

  /**
   * Die USB-Kennung als Text (oder `null`).
   *
   * @param port Der Port.
   * @returns Die Kennung.
   */
  usbIds(port: SerialPortInfo): string | null {
    return port.vendorId && port.productId
      ? this.t().serial.usbIds(port.vendorId, port.productId)
      : null;
  }

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
