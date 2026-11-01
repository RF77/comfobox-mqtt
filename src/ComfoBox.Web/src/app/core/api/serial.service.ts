import { inject, Injectable } from '@angular/core';

import type { SerialPortInfo, TestResultDto } from '../models/setup.model';
import { ApiClient } from './api-client';

/**
 * Der Zugriff auf die Endpunkte der seriellen Schnittstelle.
 *
 * <p>Der Benutzer wählt damit den seriellen Port selbst aus - ein Neustart
 * des Gateways ist dafür **nicht** nötig.
 */
@Injectable({ providedIn: 'root' })
export class SerialService {
  private readonly api = inject(ApiClient);

  /** Liest die erkannten seriellen Ports. */
  async getPorts(): Promise<SerialPortInfo[]> {
    return this.api.get<SerialPortInfo[]>('/config/serial-ports');
  }

  /** Prüft einen Port, ohne die Einstellung zu ändern. */
  async test(port: string, baudrate: number): Promise<TestResultDto> {
    return this.api.post<TestResultDto>('/config/bacnet/test', { port, baudrate });
  }

  /** Stellt die Schnittstelle um - die Änderung wirkt sofort. */
  async apply(port: string, baudrate: number): Promise<void> {
    await this.api.put<void>('/config/bacnet', { port, baudrate });
  }
}
