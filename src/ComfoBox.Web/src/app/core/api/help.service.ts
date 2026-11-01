import { Injectable, signal } from '@angular/core';

import type { HelpMeta } from '../models/help.model';

/** Der Zugriff auf den Hilfe-Bereich (statische Dateien aus dem Image). */
@Injectable({ providedIn: 'root' })
export class HelpService {
  /** Der Suchindex (einmal geladen). */
  readonly index = signal<readonly HelpMeta[]>([]);

  /** Wurde der Index geladen? */
  readonly loaded = signal(false);

  /** Der letzte Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Lädt den Suchindex. */
  async loadIndex(): Promise<void> {
    if (this.loaded()) {
      return;
    }

    try {
      // Die Dateien liegen unter /help/ (statisch, nicht unter /api).
      const response = await fetch('/help/index.json');
      const data = (await response.json()) as HelpMeta[];

      this.index.set(data);
      this.loaded.set(true);
    } catch {
      this.error.set('Die Hilfe ist in dieser Installation nicht verfügbar.');
    }
  }

  /** Lädt eine Hilfeseite (als HTML). */
  async getPage(slug: string): Promise<string> {
    // Die Hilfeseiten liegen **statisch** unter /help/ - sie sind kein Teil
    // der REST-Schnittstelle.
    //
    // Geladen wird das Fragment (`<slug>.part.html`), nicht das ganze
    // HTML-Dokument: das vollständige Dokument enthält `<head>`, eine
    // Sidebar und ein eigenes `<h1>` – eingefügt in die Oberfläche käme
    // die Seite dort doppelt.
    const response = await fetch(`/help/${encodeURIComponent(slug)}.part.html`);

    if (!response.ok) {
      throw new Error(`Die Hilfeseite „${slug}" wurde nicht gefunden (HTTP ${response.status}).`);
    }

    return response.text();
  }

  /** Die Metadaten einer Seite. */
  meta(slug: string): HelpMeta | null {
    return this.index().find((entry) => entry.slug === slug) ?? null;
  }

  /** Die Nachbarn einer Seite (Vor/Zurück). */
  neighbours(slug: string): { previous: HelpMeta | null; next: HelpMeta | null } {
    const sorted = [...this.index()].sort((a, b) => a.order - b.order);
    const position = sorted.findIndex((entry) => entry.slug === slug);

    if (position < 0) {
      return { previous: null, next: null };
    }

    return {
      previous: position > 0 ? (sorted[position - 1] ?? null) : null,
      next: position < sorted.length - 1 ? (sorted[position + 1] ?? null) : null,
    };
  }

  /** Die Volltextsuche über den Index. */
  search(term: string): readonly HelpMeta[] {
    const needle = term.trim().toLowerCase();

    if (needle.length === 0) {
      return [];
    }

    return this.index().filter(
      (entry) =>
        entry.title.toLowerCase().includes(needle) ||
        entry.description.toLowerCase().includes(needle) ||
        entry.tags.some((tag) => tag.toLowerCase().includes(needle)) ||
        entry.content.toLowerCase().includes(needle),
    );
  }
}
