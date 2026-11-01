/**
 * Die Typen des Log-Viewers.
 *
 * <p>Quelle: `src/ComfoBox.Gateway/Api/Models/DiagnosticsDtos.cs`.
 */

/** Ein Eintrag des Log-Ringpuffers. */
export interface LogEntryDto {
  /** Der Zeitpunkt des Eintrags. */
  readonly timestamp: string;
  /** Der Level, z. B. `Information`. */
  readonly level: string;
  /** Die Logger-Kategorie, z. B. `ComfoBox.Bacnet.BacnetClient`. */
  readonly source: string;
  /** Die gerenderte Meldung. */
  readonly message: string;
  /** Der Stacktrace (oder `null`). */
  readonly exception: string | null;
  /** Die strukturierten Properties des Eintrags. */
  readonly properties: Readonly<Record<string, string>>;
}

/** Die Log-Level (in Anzeigereihenfolge). */
export const LOG_LEVELS = [
  'Verbose',
  'Debug',
  'Information',
  'Warning',
  'Error',
  'Fatal',
] as const;

/** Ein Log-Level. */
export type LogLevel = (typeof LOG_LEVELS)[number];
