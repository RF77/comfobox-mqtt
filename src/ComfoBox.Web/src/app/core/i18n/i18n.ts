import { computed, Injectable, signal } from '@angular/core';

import { setFormatLocale } from '../util/format';
import { DE } from './de';
import { EN } from './en';
import { FR } from './fr';
import type { Language, LanguageOption, Translations } from './translations';

/** Die Wörterbücher. */
const DICTIONARIES: Record<Language, Translations> = {
  de: DE,
  en: EN,
  fr: FR,
};

/** Die Gebietsschemata für Zahlen und Datumsangaben. */
const LOCALES: Record<Language, string> = {
  de: 'de-DE',
  en: 'en-GB',
  fr: 'fr-FR',
};

/** Der Schlüssel im `localStorage`. */
const STORAGE_KEY = 'cbk-language';

/**
 * Die Sprache der Oberfläche.
 *
 * <p>Die Texte liegen als typisierte Wörterbücher vor. Das Template liest
 * `t()` - ein Signal, damit die Änderung der Sprache **ohne** Neuladen
 * durchschlägt (OnPush).</p>
 *
 * <p>Die Wahl wird im `localStorage` gemerkt; beim ersten Besuch entscheidet
 * die Browsersprache.</p>
 */
@Injectable({ providedIn: 'root' })
export class I18n {
  /** Die gewählte Sprache. */
  readonly language = signal<Language>(detectLanguage());

  /** Die Texte der gewählten Sprache. */
  readonly t = computed<Translations>(() => DICTIONARIES[this.language()]);

  /** Das Gebietsschema (für Zahlen und Datumsangaben). */
  readonly locale = computed(() => LOCALES[this.language()]);

  /**
   * Die auswählbaren Sprachen.
   *
   * <p>Bewusst ein veränderbares Feld: `p-select` von Optimus UI akzeptiert
   * kein `readonly`-Array (TS4104 im Template).</p>
   */
  readonly options: LanguageOption[] = [
    { code: 'de', label: 'Deutsch' },
    { code: 'en', label: 'English' },
    { code: 'fr', label: 'Français' },
  ];

  constructor() {
    // Zahlen und Zeitstempel folgen derselben Sprache.
    setFormatLocale(LOCALES[this.language()]);
  }

  /** Wechselt die Sprache und merkt sie. */
  set(language: Language): void {
    this.language.set(language);
    setFormatLocale(LOCALES[language]);

    try {
      localStorage.setItem(STORAGE_KEY, language);
      document.documentElement.lang = language;
    } catch {
      // Ein blockierter Speicher (privater Modus) ist kein Fehler.
    }
  }
}

/** Ermittelt die Startsprache. */
function detectLanguage(): Language {
  try {
    const stored = localStorage.getItem(STORAGE_KEY);

    if (stored === 'de' || stored === 'en' || stored === 'fr') {
      return stored;
    }
  } catch {
    // siehe oben
  }

  const candidates = typeof navigator === 'undefined'
    ? []
    : [...(navigator.languages ?? []), navigator.language ?? ''];

  for (const candidate of candidates) {
    const code = candidate.toLowerCase().slice(0, 2);

    if (code === 'de' || code === 'en' || code === 'fr') {
      return code;
    }
  }

  return 'de';
}
