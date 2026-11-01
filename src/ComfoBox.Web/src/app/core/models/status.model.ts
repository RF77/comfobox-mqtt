/**
 * Die Typen des Health- und Statusberichts.
 *
 * <p>Quelle: `src/ComfoBox.Gateway/Api/Models/HealthReport.cs` und
 * `src/ComfoBox.Gateway/Api/Models/StatusReport.cs`.
 */

/** Der Gesundheitszustand des Gateways. */
export type HealthState = 'online' | 'degraded' | 'offline';

/** Der Bericht über den Zustand des Gateways. */
export interface HealthReport {
  /** Der Gesamtzustand. */
  readonly state: HealthState;
  /** Die Meldung zum Gesamtzustand. */
  readonly message: string;
  /** Die Zustände der einzelnen Komponenten. */
  readonly checks: HealthCheckDto[];
  /** Die Version des Gateways. */
  readonly version: string;
  /** Die Laufzeit in Sekunden. */
  readonly uptimeSeconds: number;
  /** Der Zeitpunkt des Starts. */
  readonly startedAt: string;
}

/** Der Zustand einer Komponente. */
export interface HealthCheckDto {
  /** Der Name der Komponente, z. B. `Bacnet`. */
  readonly name: string;
  /** Der Zustand. */
  readonly state: HealthState;
  /** Die letzte Meldung der Komponente. */
  readonly message: string;
  /** Der Zeitpunkt der letzten Änderung. */
  readonly changedAt: string;
}

/** Der vollständige Laufzeitstatus. */
export interface StatusReport {
  /** Die Geräte-Id, z. B. `comfobox1`. */
  readonly deviceId: string;
  /** Die Version des Gateways. */
  readonly version: string;
  /** Die Laufzeit in Sekunden. */
  readonly uptimeSeconds: number;
  /** Der Zustand der BACnet-Verbindung. */
  readonly bacnet: BacnetStatusDto;
  /** Der Zustand der MQTT-Verbindung. */
  readonly mqtt: MqttStatusDto;
  /** Der Zustand des 24-h-Schreibzählers. */
  readonly writes: WriteStatusDto;
}

/** Der Zustand der BACnet-Verbindung. */
export interface BacnetStatusDto {
  /** Ist die ComfoBox erreichbar? */
  readonly connected: boolean;
  /** Der serielle Port, z. B. `/dev/ttyUSB0`. */
  readonly port: string;
  /** Läuft der Gateway im Simulationsbetrieb (ohne ComfoBox)? */
  readonly simulated: boolean;
  /** Der letzte Fehler (oder `null`). */
  readonly lastError: string | null;
}

/** Der Zustand der MQTT-Verbindung. */
export interface MqttStatusDto {
  /** Ist mindestens ein Broker verbunden? */
  readonly connected: boolean;
  /** Die Broker (durch Komma getrennt). */
  readonly broker: string;
}

/** Der Zustand des 24-h-Schreibzählers. */
export interface WriteStatusDto {
  /** Die Anzahl der Schreibvorgänge im Fenster. */
  readonly count: number;
  /** Das Limit (`0` = kein Limit). */
  readonly limit: number;
  /** Der Zeitpunkt, zu dem das Fenster zurückgesetzt wird (oder `null`). */
  readonly resetsAt: string | null;
}

/** Ein Eintrag der Fehlerhistorie. */
export interface ErrorEntryDto {
  /** Der Zeitpunkt des Fehlers. */
  readonly timestamp: string;
  /** Der Schweregrad: `info`, `warning` oder `error`. */
  readonly severity: string;
  /** Die Quelle, z. B. `Bacnet`. */
  readonly source: string;
  /** Der Fehlercode, z. B. `timeout`. */
  readonly code: string;
  /** Die Meldung. */
  readonly message: string;
  /** Weitere Details (oder `null`). */
  readonly detail: string | null;
}
