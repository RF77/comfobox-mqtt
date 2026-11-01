import { inject, Injectable } from '@angular/core';

import { ApiClient } from './api-client';

/** Der Zugriff auf die Betriebssteuerung (Neustart, Reconnects). */
@Injectable({ providedIn: 'root' })
export class ServiceService {
  private readonly api = inject(ApiClient);

  /**
   * Startet den Gateway neu.
   *
   * <p>Der Neustart läuft **in-process**: der Host beendet sich kontrolliert,
   * der Supervisor startet ihn neu.
   */
  async restart(): Promise<void> {
    await this.api.post('/service/restart');
  }

  /** Verbindet die BACnet-Schnittstelle neu. */
  async reconnectBacnet(): Promise<void> {
    await this.api.post('/service/reconnect/bacnet');
  }

  /** Verbindet die MQTT-Broker neu. */
  async reconnectMqtt(): Promise<void> {
    await this.api.post('/service/reconnect/mqtt');
  }
}
