/**
 * Die Reihenfolge der Gruppen auf der Seite „Werte".
 *
 * <p>Der Katalog liefert die Gruppen in seiner eigenen Reihenfolge (sie
 * entspricht dem Legacy-Programm und damit der Reihenfolge der MQTT-Topics).
 * Für die Bedienung ist diese Reihenfolge nicht überall sinnvoll: die
 * Test-Ausgänge stehen dort an zweiter Stelle, obwohl sie reine
 * Diagnose-Werte sind. Sie gehören ans Ende.</p>
 */

/**
 * Gruppen, die in der Oberfläche **immer am Ende** stehen.
 *
 * <p>Nur die Darstellung ist betroffen – Katalog, Topics und Home Assistant
 * behalten die ursprüngliche Reihenfolge.</p>
 */
export const TRAILING_GROUPS: readonly string[] = ['Tests'];

/**
 * Sortiert Gruppen: Katalogreihenfolge, aber {@link TRAILING_GROUPS} zuletzt.
 *
 * <p>Die Funktion ist rein (keine Kopie der Eingabe, keine Seiteneffekte) und
 * lässt die Reihenfolge aller anderen Gruppen unverändert.</p>
 *
 * @param groups Die Gruppen in Katalogreihenfolge.
 * @returns Die Gruppen mit den Test-Gruppen am Ende.
 */
export function trailingGroupsLast(groups: readonly string[]): string[] {
  const isTrailing = (group: string): boolean => TRAILING_GROUPS.includes(group);

  return [...groups.filter((group) => !isTrailing(group)), ...groups.filter(isTrailing)];
}
