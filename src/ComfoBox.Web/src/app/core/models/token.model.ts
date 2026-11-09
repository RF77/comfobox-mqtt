/**
 * Die Typen der API-Token.
 *
 * <p>Quelle: `src/ComfoBox.Gateway/Api/Models/TokenDtos.cs`.
 */

/** Der Umfang eines Tokens. */
export type TokenScope = 'read' | 'control' | 'admin';

/** Die Anfrage zur Erzeugung eines API-Tokens. */
export interface CreateTokenRequest {
  /** Der Anzeigename des Tokens, z. B. `home-assistant`. */
  readonly name: string;
  /** Der Umfang: nur lesen, regeln oder verwalten. */
  readonly scope: TokenScope;
  /**
   * Die Gültigkeitsdauer in Tagen.
   *
   * <p>`null` oder `0` bedeutet: kein Ablaufdatum.</p>
   */
  readonly expiresInDays: number | null;
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
  /** Der Umfang des Tokens. */
  readonly scope: TokenScope;
  /** Der Zeitpunkt des Ablaufs (oder `null` für „unbegrenzt"). */
  readonly expiresAt: string | null;
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
  /** Der Umfang des Tokens. */
  readonly scope: TokenScope;
  /** Der Zeitpunkt des Ablaufs (oder `null` für „unbegrenzt"). */
  readonly expiresAt: string | null;
  /** Ist das Token bereits abgelaufen? */
  readonly expired: boolean;
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
