/**
 * Die Typen des Getting-Started-Assistenten.
 *
 * <p>Quelle: `src/ComfoBox.Gateway/Api/Models/SetupDtos.cs`.
 */

/** Der Zustand der Ersteinrichtung. */
export interface SetupStateDto {
  /** Ist die Ersteinrichtung abgeschlossen? */
  readonly setupCompleted: boolean;
  /** Die Schritte des Assistenten in Reihenfolge. */
  readonly steps: readonly string[];
  /** Die Version des Setup-Formats. */
  readonly version: number;
  /** Die aktuell gespeicherten Werte (für den wiederholbaren Assistenten). */
  readonly current?: SetupCurrentValues | null;
}

/**
 * Die aktuell gespeicherten Werte der Einrichtung.
 *
 * <p>Es werden **niemals** Passwörter geliefert.
 */
export interface SetupCurrentValues {
  /** Der konfigurierte serielle Port (oder `null`). */
  readonly port: string | null;
  /** Die konfigurierte Baudrate. */
  readonly baudrate: number;
  /** Der Host des Brokers (oder `null` - noch kein Broker gewählt). */
  readonly brokerHost: string | null;
  /** Der Port des Brokers. */
  readonly brokerPort: number;
  /** Der Benutzername am Broker (oder `null`). */
  readonly brokerUsername: string | null;
  /** Der Benutzername des Administrators (oder `null`). */
  readonly adminUsername: string | null;
  /** Werden alle seriellen Ports durchgereicht? */
  readonly passAllSerialDevices: boolean;
}

/** Ein erkannter serieller Port. */
export interface SerialPortInfo {
  /** Der Name des Ports, z. B. `COM3` oder `/dev/ttyUSB0`. */
  readonly port: string;
  /** Die Beschreibung des Ports. */
  readonly description: string | null;
  /** Der stabile Pfad aus `ports.json` des Supervisors (oder `null`). */
  readonly stablePath: string | null;
}

/** Das Ergebnis eines Verbindungstests. */
export interface TestResultDto {
  /** War der Test erfolgreich? */
  readonly ok: boolean;
  /** Die Meldung (deutsch, für die Anzeige). */
  readonly message: string;
  /** Der maschinenlesbare Hinweis, z. B. `no_iam`. */
  readonly hint: string | null;
  /** Die Dauer des Tests in Millisekunden. */
  readonly durationMs: number;
}

/** Die Anfrage für den BACnet-Verbindungstest. */
export interface BacnetTestRequest {
  /** Der serielle Port, z. B. `/dev/ttyUSB0`. */
  readonly port: string | null;
  /** Die Baudrate (Standard: 38400). */
  readonly baudrate: number;
}

/** Die Anfrage für den MQTT-Verbindungstest. */
export interface MqttTestRequest {
  /** Der Broker-Host. */
  readonly host: string | null;
  /** Der Broker-Port (Standard: 1883). */
  readonly port: number;
  /** Der Benutzername (oder `null`). */
  readonly username: string | null;
  /** Das Passwort im Klartext (oder `null`). */
  readonly password: string | null;
  /** Soll TLS verwendet werden? */
  readonly useTls: boolean;
}

/**
 * Die Konfiguration, die der Assistent schreibt.
 *
 * <p>Das Passwort kommt im **Klartext** an und wird serverseitig gehasht.
 */
export interface SetupConfigRequest {
  /** Der serielle Port, z. B. `/dev/ttyUSB0`. */
  readonly port: string | null;
  /** Die Baudrate (Standard: 38400). */
  readonly baudrate: number;
  /** Der Broker-Host. */
  readonly brokerHost: string | null;
  /** Der Broker-Port (Standard: 1883). */
  readonly brokerPort: number;
  /** Der Broker-Benutzername (oder `null`). */
  readonly brokerUsername: string | null;
  /** Das Broker-Passwort im Klartext (oder `null`). */
  readonly brokerPassword: string | null;
  /** Soll TLS verwendet werden? */
  readonly useTls: boolean;
  /** Der Benutzername des Administrators. */
  readonly adminUsername: string | null;
  /** Das Passwort des Administrators im Klartext. */
  readonly adminPassword: string | null;  /** Sollen alle seriellen Ports durchgereicht werden? */
  readonly passAllSerialDevices: boolean;}

/** Die Schritte des Assistenten. */
export type SetupStep = 'welcome' | 'serial' | 'bacnet' | 'mqtt' | 'security' | 'done';

/** Die Reihenfolge der Schritte. */
export const SETUP_STEPS: readonly SetupStep[] = [
  'welcome',
  'serial',
  'bacnet',
  'mqtt',
  'security',
  'done',
] as const;
