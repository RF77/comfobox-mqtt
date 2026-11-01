/**
 * Die Formatierungshelfer der Oberfläche.
 *
 * <p>Alle Zahlen werden **deutsch** dargestellt (Dezimalkomma). Das betrifft
 * nur die Anzeige - die Payloads an den Server bleiben invariant
 * (→ `.github/copilot-instructions.md` §3.2).
 */

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

  return date.toLocaleString('de-DE', { dateStyle: 'short', timeStyle: 'medium' });
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

  return date.toLocaleTimeString('de-DE', { timeStyle: 'medium' });
}

/** Formatiert eine Zahl (deutsch, mit höchstens zwei Nachkommastellen). */
export function formatNumber(value: number | null | undefined, digits = 2): string {
  if (value === null || value === undefined || Number.isNaN(value)) {
    return '–';
  }

  return value.toLocaleString('de-DE', {
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

/** Die relative Zeit (deutsch), z. B. „vor 3 min". */
export function formatRelative(value: string | null | undefined): string {
  if (!value) {
    return '–';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  const seconds = Math.round((Date.now() - date.getTime()) / 1000);

  if (seconds < 0) {
    return 'gerade eben';
  }

  if (seconds < 60) {
    return 'gerade eben';
  }

  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) {
    return `vor ${minutes} min`;
  }

  const hours = Math.floor(minutes / 60);
  if (hours < 24) {
    return `vor ${hours} h`;
  }

  const days = Math.floor(hours / 24);
  return `vor ${days} d`;
}
