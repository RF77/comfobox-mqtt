/** Ein beobachtetes MS/TP-Ereignis auf dem BACnet-Bus. */
export interface BacnetEvent {
  /** Die Art des Ereignisses, z. B. `TokenReceived`. */
  readonly kind: string;
  /** Die beteiligte Station (MSTP-Adresse). */
  readonly station: number;
  /** Der MS/TP-Frame-Typ. */
  readonly frameType: number;
  /** Der Zeitpunkt (UTC). */
  readonly timestamp: string;
}

/** Die Auswertung der BACnet-Bus-Analyse. */
export interface BacnetAnalysis {
  /** Der Beginn der Beobachtung. */
  readonly startedAt: string;
  /** Die Dauer der Beobachtung in Sekunden. */
  readonly durationSeconds: number;
  /** Läuft gerade eine Beobachtung? */
  readonly observing: boolean;
  /** Ist das Polling pausiert? */
  readonly pollingPaused: boolean;
  /** Der konfigurierte serielle Port. */
  readonly port: string;
  /** Die eingestellte Baudrate. */
  readonly baudrate: number;
  /** Die MSTP-Adresse des Gateways. */
  readonly thisStation: number;
  /** Die Anzahl der beobachteten Ereignisse. */
  readonly totalEvents: number;
  /** Die Zähler je Ereignisart. */
  readonly counters: Readonly<Record<string, number>>;
  /** Die Stationen, die sich gemeldet haben. */
  readonly stationsSeen: number[];
  /** Die Ereignisse (älteste zuerst). */
  readonly events: BacnetEvent[];
  /** Die Bewertung in einem Satz. */
  readonly verdict: string;
}

/** Die Anfrage, eine Bus-Analyse zu starten. */
export interface BacnetObservationRequest {
  /** Die Dauer in Sekunden (3 … 300). */
  readonly durationSeconds: number;
  /** Soll das Polling während der Beobachtung pausiert werden? */
  readonly pausePolling: boolean;
}
