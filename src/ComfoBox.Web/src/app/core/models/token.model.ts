/**
 * Die Typen der API-Token.
 *
 * <p>Quelle: `src/ComfoBox.Gateway/Api/Models/TokenDtos.cs`.
 */

/** Die Anfrage zur Erzeugung eines API-Tokens. */
export interface CreateTokenRequest {
  /** Der Anzeigename des Tokens, z. B. `home-assistant`. */
  readonly name: string;
  /** Darf das Token administrative Aufgaben ausführen? */
  readonly isAdmin: boolean;
}

/**
 * Das Ergebnis der Token-Erzeugung.
 *
 * <p>{@link token} ist der **einzige** Moment, in dem der Klartext zu sehen ist.
 */
export interface CreatedTokenDto {
  /** Die technische Id des Tokens. */
  readonly id: string;
  /** Der Anzeigename des Tokens. */
  readonly name: string;
  /** Der Klartext des Tokens - **nur jetzt** sichtbar. */
  readonly token: string;
  /** Der Zeitpunkt der Erzeugung. */
  readonly createdAt: string;
  /** Darf das Token administrative Aufgaben ausführen? */
  readonly isAdmin: boolean;
}

/** Die Beschreibung eines Tokens (ohne Klartext). */
export interface TokenInfoDto {
  /** Die technische Id des Tokens. */
  readonly id: string;
  /** Der Anzeigename des Tokens. */
  readonly name: string;
  /** Der Zeitpunkt der Erzeugung. */
  readonly createdAt: string;
  /** Darf das Token administrative Aufgaben ausführen? */
  readonly isAdmin: boolean;
}

/**
 * Die Fehlerantwort der REST-Schnittstelle.
 *
 * <p>Der Feldname `error` ist ein stabiler Fehlercode.
 */
export interface ApiErrorDto {
  /** Der stabile Fehlercode, z. B. `value_out_of_range`. */
  readonly error: string;
  /** Die Meldung (deutsch, für die Anzeige). */
  readonly message: string;
  /** Die Id des betroffenen Items (bei Item-Fehlern). */
  readonly itemId?: string | null;
  /** Die Anzahl der Schreibvorgänge im 24-h-Fenster. */
  readonly writesLast24h?: number | null;
  /** Das Limit der Schreibvorgänge im 24-h-Fenster. */
  readonly maxWritesPer24h?: number | null;
  /** Die untere Grenze des zulässigen Bereichs. */
  readonly min?: number | null;
  /** Die obere Grenze des zulässigen Bereichs. */
  readonly max?: number | null;
  /** Der Hinweis für die Oberfläche, z. B. `no_iam`. */
  readonly hint?: string | null;
}
