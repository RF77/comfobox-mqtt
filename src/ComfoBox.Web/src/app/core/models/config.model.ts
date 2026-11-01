/**
 * Die Typen der Konfiguration.
 *
 * <p>Quelle: `src/ComfoBox.Gateway/Api/Models/ConfigDtos.cs`.
 */

/** Die aktuelle Konfiguration (ohne Passwörter). */
export interface ConfigDto {
  /** Der Abschnitt „Gateway". */
  readonly gateway: GatewayConfigDto;
  /** Der Abschnitt „Bacnet". */
  readonly bacnet: BacnetConfigDto;
  /** Der Abschnitt „Mqtt". */
  readonly mqtt: MqttConfigDto;
  /** Der Abschnitt „Logging". */
  readonly logging: LoggingConfigDto;
  /** Der Abschnitt „Api". */
  readonly api: ApiConfigDto;
  /** Woher kommt die Konfiguration? (`file`, `environment`, `defaults`). */
  readonly source: string;
}

/** Der Abschnitt „Gateway". */
export interface GatewayConfigDto {
  /** Das Basis-Topic (Standard: `ComfoBox`). */
  readonly baseTopic: string;
  /** Die Geräte-Id. */
  readonly deviceId: string;
  /** Ist der Experten-Modus aktiv? */
  readonly expertMode: boolean;
  /** Das Datenverzeichnis. */
  readonly dataDirectory: string;
  /** Das Poll-Intervall in Sekunden. */
  readonly pollIntervalSeconds: number;
  /** Der Faktor für sekundäre Items. */
  readonly secondaryPollFactor: number;
  /** Das Limit der Schreibvorgänge im 24-h-Fenster. */
  readonly maxWritesPer24h: number;
  /** Ist der Neustart über die API erlaubt? */
  readonly restartEnabled: boolean;
}

/** Der Abschnitt „Bacnet". */
export interface BacnetConfigDto {
  /** Der serielle Port. */
  readonly port: string;
  /** Die Baudrate. */
  readonly baudrate: number;
  /** Die Geräte-Instanz der ComfoBox. */
  readonly deviceId: number;
  /** Die MSTP-Adresse dieses Knotens. */
  readonly thisStation: number;
  /** Die höchste MAC-Adresse im Netzsegment. */
  readonly maxMaster: number;
  /** Das Zeitfenster je Item in Millisekunden. */
  readonly itemTimeoutMs: number;
  /** Die Anzahl der Versuche je Anfrage. */
  readonly retries: number;
}

/** Der Abschnitt „Mqtt". */
export interface MqttConfigDto {
  /** Die Client-ID am Broker. */
  readonly clientId: string;
  /** Das Keep-Alive in Sekunden. */
  readonly keepAliveSeconds: number;
  /** Das Präfix der Discovery-Topics. */
  readonly discoveryPrefix: string;
  /** Ist die Home-Assistant-Discovery aktiviert? */
  readonly enableDiscovery: boolean;
  /** Werden die Item-Werte retained publiziert? */
  readonly useMqttRetain: boolean;
  /** Das Intervall der Status-Publishes in Sekunden. */
  readonly statusIntervalSeconds: number;
  /** Die Broker (ohne Passwörter). */
  readonly brokers: MqttBrokerConfigDto[];
}

/** Ein Broker (ohne Passwort). */
export interface MqttBrokerConfigDto {
  /** Der Hostname oder die IP-Adresse. */
  readonly host: string;
  /** Der Port. */
  readonly port: number;
  /** Der Benutzername (oder `null`). */
  readonly username: string | null;
  /** Ist ein Passwort gesetzt? (der Wert selbst wird nie ausgegeben). */
  readonly passwordSet: boolean;
  /** Läuft die Verbindung über TLS? */
  readonly useTls: boolean;
  /** Werden selbstsignierte Zertifikate akzeptiert? */
  readonly allowUntrustedCertificates: boolean;
}

/** Der Abschnitt „Logging". */
export interface LoggingConfigDto {
  /** Das Verzeichnis der Log-Dateien. */
  readonly directory: string;
  /** Die Anzahl der aufbewahrten Log-Dateien. */
  readonly retainedFileCount: number;
  /** Die Kapazität des In-Memory-Ringpuffers. */
  readonly ringBufferCapacity: number;
  /** Der minimale Level im Ringpuffer. */
  readonly ringBufferMinimumLevel: string;
}

/** Der Abschnitt „Api". */
export interface ApiConfigDto {
  /** Müssen alle Anfragen authentifiziert werden? */
  readonly requireAuthentication: boolean;
  /** Ist die Ersteinrichtung abgeschlossen? */
  readonly setupCompleted: boolean;
  /** Der Benutzername des Administrators (oder `null`). */
  readonly adminUsername: string | null;
  /** Wie viele API-Token existieren? */
  readonly tokenCount: number;
}

/**
 * Die zur Laufzeit änderbaren Konfigurationswerte.
 *
 * <p>Alles andere (Port, Baudrate, Broker) erfordert einen Neustart.
 */
export interface ConfigUpdateRequest {
  /** Der Experten-Modus. */
  readonly expertMode?: boolean;
  /** Das Poll-Intervall in Sekunden. */
  readonly pollIntervalSeconds?: number;
  /** Das Limit der Schreibvorgänge im 24-h-Fenster. */
  readonly maxWritesPer24h?: number;
  /** Werden die Item-Werte retained publiziert? */
  readonly useMqttRetain?: boolean;
}
