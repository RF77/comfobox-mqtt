/**
 * Die Formatierungshelfer der Oberfläche.
 *
 * <p>Alle Zahlen und Zeitstempel werden im **aktiven** Gebietsschema dargestellt
 * (Deutsch, Englisch oder Französisch - siehe {@link setFormatLocale}). Das
 * betrifft nur die Anzeige - die Payloads an den Server bleiben invariant
 * (→ `.github/copilot-instructions.md` §3.2).
 */

/** Das aktive Gebietsschema (wird vom `I18n`-Dienst gesetzt). */
let activeLocale = 'de-DE';

/** Setzt das Gebietsschema für alle Formatierungen. */
export function setFormatLocale(locale: string): void {
  activeLocale = locale;
}

/** Das aktive Gebietsschema. */
export function formatLocale(): string {
  return activeLocale;
}

/** Formatiert eine Laufzeit in Sekunden als „2 h 14 min". */
export function formatUptime(seconds: number | null | undefined): string {
  if (seconds === null || seconds === undefined || seconds < 0) {
    return '–';
  }

  const total = Math.floor(seconds);
  const days = Math.floor(total / 86_400);
  const hours = Math.floor((total % 86_400) / 3_600);
  const minutes = Math.floor((total % 3_600) / 60);
  const secondsPart = total % 60;

  if (days > 0) {
    return `${days} d ${hours} h`;
  }

  if (hours > 0) {
    return `${hours} h ${minutes} min`;
  }

  if (minutes > 0) {
    return `${minutes} min ${secondsPart} s`;
  }

  return `${secondsPart} s`;
}

/** Formatiert einen Zeitstempel (deutsch). */
export function formatDateTime(value: string | null | undefined): string {
  if (!value) {
    return '–';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleString(activeLocale, { dateStyle: 'short', timeStyle: 'medium' });
}

/** Formatiert eine Uhrzeit (deutsch). */
export function formatTime(value: string | null | undefined): string {
  if (!value) {
    return '–';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return date.toLocaleTimeString(activeLocale, { timeStyle: 'medium' });
}

/** Formatiert eine Zahl (deutsch, mit höchstens zwei Nachkommastellen). */
export function formatNumber(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '–';
  }

  return value.toLocaleString(activeLocale, {
    minimumFractionDigits: 0,
    maximumFractionDigits: digits,
  });
}

/** Formatiert einen Anteil (0–1) als Prozentwert. */
export function formatPercent(value: number | null | undefined): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '–';
  }

  return `${Math.round(value * 100)} %`;
}

/** Die relative Zeit (in der aktiven Sprache), z. B. „vor 3 min". */
export function formatRelative(value: string | null | undefined): string {
  if (!value) {
    return '–';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const seconds = Math.round((Date.now() - date.getTime()) / 1000);

  const [justNow, ago] = relativeWords();

  if (seconds < 60) {
    return justNow;
  }

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return ago(`${minutes} min`);
  }

  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return ago(`${hours} h`);
  }

  const days = Math.floor(hours / 24);
  return ago(`${days} d`);
}

/** Die Wörter für die relative Zeit in der aktiven Sprache. */
function relativeWords(): [justNow: string, ago: (value: string) => string] {
  if (activeLocale.startsWith('en')) {
    return ['just now', (value) => `${value} ago`];
  }

  if (activeLocale.startsWith('fr')) {
    return ["à l'instant", (value) => `il y a ${value}`];
  }

  return ['gerade eben', (value) => `vor ${value}`];
}
