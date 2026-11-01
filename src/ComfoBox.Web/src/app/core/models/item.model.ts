/**
 * Die Typen des Item-Katalogs.
 *
 * <p>Die Feldnamen sind der Vertrag des Gateways (`src/ComfoBox.Gateway/Api/Models/ItemDto.cs`).
 * Sie dürfen nicht umbenannt werden - die Home-Assistant-Integration liest sie ebenso.
 */

/** Die Art eines Item-Werts. */
export type ItemKind = 'analog' | 'enum' | 'date';

/** Die Anzeigeform eines Werts. */
export interface ItemDto {
  /** Die stabile Id, z. B. `zone.set_point_normal`. */
  readonly id: string;
  /** Die Topic-Gruppe, z. B. `Zone`. */
  readonly group: string;
  /** Der Topic-Name, z. B. `SetPointNormal`. */
  readonly name: string;
  /** Der Anzeigename (aus der Beschreibung abgeleitet). */
  readonly displayName: string;
  /** Die Art des Werts. */
  readonly kind: ItemKind;
  /** Der aktuelle Zahlenwert (oder `null`). */
  readonly value: number | null;
  /** Der aktuelle Wert als Text (Anzeige), z. B. `21,5 °C`. */
  readonly valueText: string | null;
  /** Die Einheit, z. B. `°C`. */
  readonly unit: string | null;
  /** Die untere Grenze für Schreibvorgänge. */
  readonly min: number | null;
  /** Die obere Grenze für Schreibvorgänge. */
  readonly max: number | null;
  /** Ist das Item aktuell schreibbar (Experten-Modus berücksichtigt)? */
  readonly writable: boolean;
  /** Erfordert das Item den Experten-Modus? */
  readonly requiresExpertMode: boolean;
  /** Das MQTT-Topic des Werts. */
  readonly stateTopic: string | null;
  /** Das MQTT-Schreib-Topic (nur bei schreibbaren Items). */
  readonly setTopic: string | null;
  /** Der Zeitpunkt des letzten erfolgreichen Lesens. */
  readonly lastUpdated: string | null;
  /** Ist der Wert veraltet? */
  readonly stale: boolean;
  /** Die Enum-Werte (Nummer → Name) - nur bei Enum-Items. */
  readonly enumValues: Readonly<Record<string, string>>;
}

/** Ein gecachter Item-Wert aus dem SSE-Stream. */
export interface CachedValueDto {
  /** Die Id des Items. */
  readonly id: string;
  /** Der Zahlenwert (oder `null`). */
  readonly value: number | null;
  /** Der Enum-Name (oder `null`). */
  readonly valueName: string | null;
  /** Die Einheit (oder `null`). */
  readonly unit: string | null;
  /** Der Zeitpunkt des letzten erfolgreichen Lesens. */
  readonly timestamp: string;
  /** Ist der Wert veraltet? */
  readonly isStale: boolean;
}

/** Ein Enum-Wert als Auswahloption. */
export interface EnumOption {
  /** Die Nummer des Werts (der Schlüssel aus `enumValues`). */
  readonly value: number;
  /** Der Name des Werts (unverändert - Teil des Topic-Vertrags). */
  readonly label: string;
}
