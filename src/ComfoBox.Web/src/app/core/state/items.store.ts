import { computed, effect, inject, Injectable, signal } from '@angular/core';

import type { ItemDto } from '../models/item.model';
import { EventsService } from '../api/events.service';
import { ItemsService } from '../api/items.service';
import { toApiError } from '../api/api-client';

/**
 * Der Zustand des Item-Katalogs.
 *
 * <p>Die Werte kommen aus dem REST-Endpunkt (einmalige Ladung) und werden
 * danach per SSE aktualisiert. **Kein** Polling im Browser.
 */
@Injectable({ providedIn: 'root' })
export class ItemsStore {
  private readonly api = inject(ItemsService);
  private readonly events = inject(EventsService);

  /** Alle Items des Katalogs. */
  readonly items = signal<ItemDto[]>([]);

  /** Wird gerade geladen? */
  readonly loading = signal(false);

  /** Der letzte Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Die Ids der Items, die gerade geschrieben werden. */
  readonly saving = signal<ReadonlySet<string>>(new Set());

  /** Die Volltextsuche. */
  readonly search = signal('');

  /** Nur schreibbare Items anzeigen? */
  readonly onlyWritable = signal(false);

  /** Nur Items anzeigen, die den Experten-Modus erfordern? */
  readonly onlyExpert = signal(false);

  /** Nur veraltete Werte anzeigen? */
  readonly onlyStale = signal(false);

  /** Die gewählte Gruppe (oder `null` = alle). */
  readonly group = signal<string | null>(null);

  /** Die sichtbaren Items (nach Filter und Suche). */
  readonly visible = computed<ItemDto[]>(() => {
    const term = this.search().trim().toLowerCase();
    const group = this.group();
    const onlyWritable = this.onlyWritable();
    const onlyExpert = this.onlyExpert();
    const onlyStale = this.onlyStale();

    return this.items().filter((item) => {
      if (group !== null && item.group !== group) {
        return false;
      }

      if (onlyWritable && !item.writable) {
        return false;
      }

      if (onlyExpert && !item.requiresExpertMode) {
        return false;
      }

      if (onlyStale && !item.stale) {
        return false;
      }

      if (term.length === 0) {
        return true;
      }

      return (
        item.displayName.toLowerCase().includes(term) ||
        item.name.toLowerCase().includes(term) ||
        item.group.toLowerCase().includes(term) ||
        item.id.toLowerCase().includes(term) ||
        (item.stateTopic?.toLowerCase().includes(term) ?? false)
      );
    });
  });

  /** Die sichtbaren Items, gruppiert (in der Reihenfolge des Katalogs). */
  readonly grouped = computed<{ group: string; items: ItemDto[] }[]>(() => {
    const buckets = new Map<string, ItemDto[]>();

    for (const item of this.visible()) {
      const bucket = buckets.get(item.group);
      if (bucket) {
        bucket.push(item);
      } else {
        buckets.set(item.group, [item]);
      }
    }

    return [...buckets.entries()].map(([group, items]) => ({ group, items }));
  });

  /** Die verfügbaren Gruppen (für die Navigation). */
  readonly groups = computed<string[]>(() => {
    const seen = new Set<string>();
    const result: string[] = [];

    for (const item of this.items()) {
      if (!seen.has(item.group)) {
        seen.add(item.group);
        result.push(item.group);
      }
    }

    return result;
  });

  /** Die Anzahl der Items insgesamt. */
  readonly count = computed(() => this.items().length);

  constructor() {
    // Der SSE-Stream ist die Wahrheit für die Live-Werte: er überschreibt
    // den Wert aus der initialen Ladung.
    effect(() => {
      const live = this.events.values();

      this.items.update((items) => {
        if (items.length === 0 || live.size === 0) {
          return items;
        }

        let changed = false;
        const next: ItemDto[] = items.map((item) => {
          const update = live.get(item.id);
          if (!update) {
            return item;
          }

          const value = update.value;
          const text = update.valueName ?? this.formatAnalog(update.value, item.unit);

          if (
            item.value === value &&
            item.valueText === text &&
            item.stale === update.isStale &&
            item.lastUpdated === update.timestamp
          ) {
            return item;
          }

          changed = true;

          return {
            ...item,
            value,
            valueText: text,
            stale: update.isStale,
            lastUpdated: update.timestamp,
          };
        });

        return changed ? next : items;
      });
    });
  }

  /** Lädt den Katalog (die initiale Ladung). */
  async load(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      this.items.set(await this.api.getAll());
    } catch (error) {
      this.error.set(toApiError(error).message);
    } finally {
      this.loading.set(false);
    }
  }

  /**
   * Schreibt einen Wert.
   *
   * <p>Es gibt **kein** optimistisches Update: der Wert wird erst nach der
   * Bestätigung des Servers übernommen. Bei einem Fehler wird der Serverwert
   * wiederhergestellt - die Oberfläche zeigt nie einen Wert, den das Gerät
   * nicht angenommen hat.
   *
   * @param item Das Item.
   * @param value Der neue Wert (Zahl oder Enum-Name).
   * @returns <c>true</c>, wenn der Wert angenommen wurde.
   */
  async write(item: ItemDto, value: number | string): Promise<boolean> {
    this.saving.update((current) => new Set(current).add(item.id));
    this.error.set(null);

    try {
      await this.api.write(item.id, value);

      // Der Serverwert ist die Wahrheit - der SSE-Stream liefert ihn gleich.
      return true;
    } catch (error) {
      this.error.set(toApiError(error).message);
      await this.reload(item.id);
      return false;
    } finally {
      this.saving.update((current) => {
        const next = new Set(current);
        next.delete(item.id);
        return next;
      });
    }
  }

  /** Lädt ein einzelnes Item neu (nach einem Schreibfehler). */
  async reload(id: string): Promise<void> {
    try {
      const fresh = await this.api.getById(id);

      this.items.update((items) =>
        items.map((item) => (item.id === fresh.id ? fresh : item)),
      );
    } catch (error) {
      this.error.set(toApiError(error).message);
    }
  }

  /** Setzt alle Filter zurück. */
  resetFilters(): void {
    this.search.set('');
    this.onlyWritable.set(false);
    this.onlyExpert.set(false);
    this.onlyStale.set(false);
    this.group.set(null);
  }

  /** Die lokalisierte Darstellung eines Analogwerts. */
  private formatAnalog(value: number | null, unit: string | null): string | null {
    if (value === null) {
      return null;
    }

    const formatted = value.toLocaleString('de-DE', { maximumFractionDigits: 2 });

    return unit ? `${formatted} ${unit}` : formatted;
  }
}
