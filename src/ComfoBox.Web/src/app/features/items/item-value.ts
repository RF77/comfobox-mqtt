import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  input,
  output,
  signal,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ButtonModule } from '@openng/optimus-ui/button';
import { InputNumberModule } from '@openng/optimus-ui/inputnumber';
import { SelectModule } from '@openng/optimus-ui/select';
import { TagModule } from '@openng/optimus-ui/tag';

import type { EnumOption, ItemDto } from '../../core/models/item.model';
import { I18n } from '../../core/i18n/i18n';
import { formatRelative } from '../../core/util/format';
/**
 * Die Bedienzeile eines Items.
 *
 * <p>Analogwerte werden mit einer Zahleneingabe (Min/Max aus dem Katalog)
 * geschrieben, Enumwerte mit einer Auswahl (die **exakten** Enum-Namen).
 * Der Wert wird **erst** nach der Bestätigung des Servers übernommen.
 */
@Component({
  selector: 'app-item-value',
  imports: [ButtonModule, FormsModule, InputNumberModule, SelectModule, TagModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cb-item">
      <div class="cb-item-head">
        <span class="cb-item-name">{{ item().displayName }}</span>
        @if (item().stale) {
          <p-tag [value]="t().item.stale" severity="warn" />
        }
        @if (item().requiresExpertMode) {
          <p-tag [value]="t().item.expert" severity="info" />
        }
      </div>

      <div class="cb-item-value">
        <span class="cb-item-current">{{ item().valueText ?? t().common.dash }}</span>
        <span class="cb-item-age">{{ age() }}</span>
      </div>

      @if (item().writable && item().kind === 'analog') {
        <div class="cb-item-editor">
          <p-inputnumber
            [ngModel]="numericValue()"
            (ngModelChange)="numericValue.set($event)"
            [min]="item().min ?? undefined"
            [max]="item().max ?? undefined"
            [minFractionDigits]="0"
            [maxFractionDigits]="2"
            [useGrouping]="false"
            [showButtons]="true"
            buttonLayout="horizontal"
            [inputStyle]="{ width: '9rem' }"
            [attr.aria-label]="t().item.newValue(item().displayName)"
            [disabled]="saving()"
          />

          <p-button
            [label]="t().item.save"
            icon="pi pi-check"
            [loading]="saving()"
            [disabled]="!canSave()"
            (onClick)="saveNumeric()"
          />
        </div>
      } @else if (item().writable && item().kind === 'enum') {
        <div class="cb-item-editor">
          <p-select
            [options]="options()"
            optionLabel="label"
            optionValue="value"
            [ngModel]="selectedEnum()"
            (ngModelChange)="selectedEnum.set($event)"
            [disabled]="saving()"
            [attr.aria-label]="t().item.newValue(item().displayName)"
            [placeholder]="t().item.choose"
          />

          <p-button
            [label]="t().item.save"
            icon="pi pi-check"
            [loading]="saving()"
            [disabled]="!canSave()"
            (onClick)="saveEnum()"
          />
        </div>
      } @else {
        <div class="cb-item-editor">
          <span class="cb-item-readonly">
            {{ item().writable ? '' : t().item.readonly }}
          </span>
        </div>
      }

      @if (item().stateTopic; as topic) {
        <code class="cb-item-topic" [title]="t().item.topic(topic)">{{ topic }}</code>
      }
    </div>
  `,
  styles: `
    :host {
      display: block;
    }

    .cb-item {
      display: flex;
      flex-direction: column;
      gap: 0.5rem;
      padding: 0.85rem;
      border: 1px solid var(--p-surface-300, var(--p-content-border-color));
      border-radius: var(--p-border-radius-lg, 0.75rem);
    }

    .cb-item-head {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
    }

    .cb-item-name {
      font-weight: 600;
    }

    .cb-item-value {
      display: flex;
      align-items: baseline;
      gap: 0.6rem;
      flex-wrap: wrap;
    }

    .cb-item-current {
      font-size: 1.2rem;
      font-weight: 600;
    }

    .cb-item-age,
    .cb-item-readonly {
      font-size: 0.8rem;
      color: var(--p-text-muted-color, inherit);
    }

    .cb-item-editor {
      display: flex;
      align-items: center;
      gap: 0.5rem;
      flex-wrap: wrap;
    }

    .cb-item-topic {
      font-size: 0.75rem;
      color: var(--p-text-muted-color, inherit);
      word-break: break-all;
    }
  `,
})
export class ItemValueComponent {
  /** Das Item. */
  readonly item = input.required<ItemDto>();

  /** Wird gerade geschrieben? */
  readonly saving = input(false);

  /** Wird ausgelöst, wenn ein Wert geschrieben werden soll. */
  readonly write = output<{ item: ItemDto; value: number | string }>();

  private readonly i18n = inject(I18n);

  /** Die Texte der gewählten Sprache. */
  readonly t = this.i18n.t;

  /** Der aktuelle Zahlenwert (für die Zahleneingabe). */
  readonly numericValue = signal<number | null>(null);

  /** Der aktuell gewählte Enum-Wert. */
  readonly selectedEnum = signal<number | null>(null);

  /** Die Auswahloptionen (die exakten Enum-Namen). */
  readonly options = computed<EnumOption[]>(() => {
    const entries = this.item().enumValues ?? {};

    return Object.entries(entries)
      .map(([key, label]) => ({ value: Number(key), label }))
      .sort((a, b) => a.value - b.value);
  });

  /** Das Alter des Werts (als Text). */
  readonly age = computed(() => formatRelative(this.item().lastUpdated));

  constructor() {
    // Der Wert wird aus dem Serverwert übernommen - **nicht** optimistisch
    // gesetzt. Beim Wechsel des Items wird der Serverwert erneut übernommen.
    effect(() => {
      const item = this.item();

      this.numericValue.set(item.value);
      this.selectedEnum.set(this.currentEnumValue(item));
    });
  }

  /** Kann der aktuelle Wert gespeichert werden? */
  canSave(): boolean {
    const item = this.item();

    if (item.kind === 'analog') {
      const value = this.numericValue();
      if (value === null || value === undefined) {
        return false;
      }

      if (item.min !== null && value < item.min) {
        return false;
      }

      if (item.max !== null && value > item.max) {
        return false;
      }

      return value !== item.value;
    }

    const selected = this.selectedEnum();

    return selected !== null && selected !== this.currentEnumValue(item);
  }

  /** Schreibt einen Zahlenwert. */
  saveNumeric(): void {
    const value = this.numericValue();

    if (value === null || value === undefined) {
      return;
    }

    this.write.emit({ item: this.item(), value });
  }

  /** Schreibt einen Enum-Wert (als **Name**). */
  saveEnum(): void {
    const selected = this.selectedEnum();

    if (selected === null) {
      return;
    }

    const option = this.options().find((entry) => entry.value === selected);

    if (!option) {
      return;
    }

    this.write.emit({ item: this.item(), value: option.label });
  }

  /** Der aktuelle Enum-Wert des Items (als Zahl). */
  private currentEnumValue(item: ItemDto): number | null {
    if (item.kind !== 'enum') {
      return null;
    }

    for (const option of this.options()) {
      if (option.label === item.valueText) {
        return option.value;
      }
    }

    return item.value;
  }
}
