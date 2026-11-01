/**
 * Die Typen des Hilfe-Bereichs.
 *
 * <p>Der Index wird zur Build-Zeit aus `docs/benutzer/` erzeugt
 * (`tools/build-docs.mjs`) und liegt statisch im Image.
 */

/** Ein Eintrag des Suchindex. */
export interface HelpMeta {
  /** Der Slug der Seite (z. B. `erste-schritte`). */
  readonly slug: string;
  /** Der Titel. */
  readonly title: string;
  /** Die Kurzbeschreibung. */
  readonly description: string;
  /** Die Sortierung (aufsteigend). */
  readonly order: number;
  /** Die Zielgruppe (`user` oder `dev`). */
  readonly audience: string;
  /** Die Schlagworte. */
  readonly tags: readonly string[];
  /** Der Volltext für die Suche. */
  readonly content: string;
}
