import { inject, Injectable } from '@angular/core';

import type {
  BacnetTestRequest,
  MqttTestRequest,
  SerialPortInfo,
  SetupConfigRequest,
  SetupStateDto,
  TestResultDto,
} from '../models/setup.model';
import { ApiClient } from './api-client';

/** Der Zugriff auf die Setup-Endpunkte des Getting-Started-Assistenten. */
@Injectable({ providedIn: 'root' })
export class SetupService {
  private readonly api = inject(ApiClient);

  /** Liest den Zustand der Ersteinrichtung. */
  async getState(): Promise<SetupStateDto> {
    return this.api.get<SetupStateDto>('/setup/state');
  }

  /** Liest die erkannten seriellen Ports. */
  async getPorts(): Promise<SerialPortInfo[]> {
    return this.api.get<SerialPortInfo[]>('/setup/ports');
  }

  /** Holt einen Vorschlag für den Web-Port. */
  async suggestPort(): Promise<number> {
    return this.api.get<number>('/setup/ports/suggest');
  }

  /** Testet die BACnet-Verbindung (WhoIs → IAm). */
  async testBacnet(request: BacnetTestRequest): Promise<TestResultDto> {
    return this.api.post<TestResultDto>('/setup/bacnet/test', request);
  }

  /** Testet die MQTT-Verbindung (Connect + Test-Topic). */
  async testMqtt(request: MqttTestRequest): Promise<TestResultDto> {
    return this.api.post<TestResultDto>('/setup/mqtt/test', request);
  }

  /** Schreibt die Konfiguration (das Passwort wird serverseitig gehasht). */
  async writeConfig(request: SetupConfigRequest): Promise<void> {
    await this.api.put<void>('/setup/config', request);
  }

  /** Übernimmt die Konfiguration und löst den Neustart aus. */
  async apply(): Promise<void> {
    await this.api.post('/setup/apply');
  }
}
