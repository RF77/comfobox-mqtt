import { inject, Injectable } from '@angular/core';

import type {
  BacnetAnalysis,
  BacnetObservationRequest,
} from '../models/bacnet.model';
import { ApiClient } from './api-client';

/**
 * Der Zugriff auf die BACnet-Bus-Analyse.
 *
 * <p>Die Analyse ist **lesend**: sie stellt keine Anfragen an die ComfoBox
 * und schreibt keine Werte. Sie beobachtet nur, ob ein Token durch den
 * MS/TP-Ring läuft.
 */
@Injectable({ providedIn: 'root' })
export class BacnetService {
  private readonly api = inject(ApiClient);

  /** Lädt die aktuelle Auswertung. */
  async getAnalysis(): Promise<BacnetAnalysis> {
    return this.api.get<BacnetAnalysis>('/diagnostics/bacnet');
  }

  /** Startet eine neue Beobachtung. */
  async startObservation(
    request: BacnetObservationRequest,
  ): Promise<BacnetAnalysis> {
    return this.api.post<BacnetAnalysis>('/diagnostics/bacnet/observe', request);
  }

  /** Beendet die Beobachtung - das Polling läuft wieder an. */
  async stopObservation(): Promise<BacnetAnalysis> {
    return this.api.delete<BacnetAnalysis>('/diagnostics/bacnet/observe');
  }
}
