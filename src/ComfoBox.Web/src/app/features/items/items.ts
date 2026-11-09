import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CheckboxModule } from '@openng/optimus-ui/checkbox';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { MessageService } from '@openng/optimus-ui/api';

import { toApiError } from '../../core/api/api-client';
import { I18n } from '../../core/i18n/i18n';
import { ItemsStore } from '../../core/state/items.store';
import type { ItemDto } from '../../core/models/item.model';
import { ItemValueComponent } from './item-value';

/**
 * Der Item-Browser: der vollständige Katalog, gruppiert und durchsuchbar.
 */
@Component({
  selector: 'app-items',
  imports: [
    ButtonModule,
    CheckboxModule,
    FormsModule,
    InputTextModule,
    ItemValueComponent,
    MessageModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './items.html',
  styleUrl: './items.scss',
})
export class ItemsComponent {
  /** Der Zustand des Katalogs. */
  readonly items = inject(ItemsStore);

  private readonly route = inject(ActivatedRoute);
  private readonly messages = inject(MessageService);

  /** Die Texte der Oberfläche. */
  readonly i18n = inject(I18n);

  /** Die Texte der gewählten Sprache. */
  readonly t = this.i18n.t;

  /** Die Ansicht: Karten oder Liste. */
  readonly view = signal<'cards' | 'list'>('cards');

  /** Ist die Gruppe eingeklappt? */
  readonly collapsed = signal<ReadonlySet<string>>(new Set());

  /** Die Anzahl der angezeigten Items. */
  readonly visibleCount = computed(() => this.items.visible().length);

  constructor() {
    // Ein Klick auf dem Dashboard („In den Geräten öffnen") kann eine
    // Suchanfrage mitbringen.
    const query = this.route.snapshot.queryParamMap.get('q');

    if (query) {
      this.items.search.set(query);
    }
  }

  /** Schreibt einen Wert und meldet das Ergebnis. */
  async write(event: { item: ItemDto; value: number | string }): Promise<void> {
    const ok = await this.items.write(event.item, event.value);

    if (ok) {
      this.messages.add({
        severity: 'success',
        summary: this.t().common.save,
        detail: event.item.displayName,
        life: 4000,
      });
      return;
    }

    const error = toApiError(this.items.error());

    this.messages.add({
      severity: 'error',
      summary: error.code,
      detail: error.message,
      life: 8000,
    });
  }

  /** Klappt eine Gruppe ein oder aus. */
  toggleGroup(group: string): void {
    const next = new Set(this.collapsed());

    if (next.has(group)) {
      next.delete(group);
    } else {
      next.add(group);
    }

    this.collapsed.set(next);
  }

  /** Ist die Gruppe eingeklappt? */
  isCollapsed(group: string): boolean {
    return this.collapsed().has(group);
  }

  /** Setzt alle Filter zurück. */
  reset(): void {
    this.items.resetFilters();
  }

  /** Lädt den Katalog neu. */
  reload(): void {
    void this.items.load();
  }
}
