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
import { TooltipModule } from '@openng/optimus-ui/tooltip';

import type { EnumOption, ItemDto } from '../../core/models/item.model';
import { I18n } from '../../core/i18n/i18n';
import { formatNumber, formatRelative } from '../../core/util/format';
/**
 * Die Bedienzeile eines Items.
 *
 * <p>Analogwerte werden mit einer Zahleneingabe (Min/Max aus dem Katalog)
 * geschrieben, Enumwerte mit einer Auswahl (die **exakten** Enum-Namen).
 * Der Wert wird **erst** nach der Bestätigung des Servers übernommen.
 */
@Component({
  selector: 'app-item-value',
  imports: [
    ButtonModule,
    FormsModule,
    InputNumberModule,
    SelectModule,
    TagModule,
    TooltipModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="cb-item">
      <div class="cb-item-head">
        <!--
          Der Tooltip trägt die Katalog-Beschreibung, den erlaubten Bereich,
          die Enum-Werte und das Topic (alles aus GET /api/items).
        -->
        <span
          class="cb-item-name"
          tabindex="0"
          [pTooltip]="tooltip()"
          tooltipPosition="top"
          tooltipEvent="both"
          tooltipStyleClass="cb-item-tooltip"
          [showDelay]="250"
          >{{ item().displayName }}</span
        >
        @if (item().stale) {
          <p-tag [value]="t().item.stale" severity="warn" />
        }
        @if (item().requiresExpertMode) {
          <p-tag [value]="t().item.expert" severity="info" />
        }
      </div>

      <div class="cb-item-value">
        <span class="cb-item-current">{{ display() ?? t().common.dash }}</span>
        <span class="cb-item-age">{{ age() }}</span>
      </div>

      @if (rangeHint(); as hint) {
        <span class="cb-item-range">{{ hint }}</span>
      }

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
          <span class="cb-item-readonly">{{ lockHint() }}</span>
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
    .cb-item-readonly,
    .cb-item-range {
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

  /**
   * Der erlaubte Bereich als Text („5 … 35 °C", „ab 5 °C", „bis 35 °C").
   *
   * <p>`min` und `max` sind **Bereiche**, keine Pflichtwerte – fehlt eine
   * Grenze, nennt der Text nur die vorhandene.</p>
   */
  readonly rangeHint = computed<string | null>(() => {
    const item = this.item();

    if (item.kind !== 'analog') {
      return null;
    }

    const unit = item.unit ? ` ${item.unit}` : '';
    const min = item.min === null ? null : `${formatNumber(item.min)}${unit}`;
    const max = item.max === null ? null : `${formatNumber(item.max)}${unit}`;

    if (min !== null && max !== null) {
      return `${min} … ${max}`;
    }

    if (min !== null) {
      return this.t().item.rangeFrom(min);
    }

    return max === null ? null : this.t().item.rangeTo(max);
  });

  /**
   * Der Tooltip des Items.
   *
   * <p>Basis ist die Katalog-Beschreibung, gefolgt vom erlaubten Bereich, den
   * Enum-Werten (als `Zahl = Name` – die Zahl ist der MQTT-Wert von
   * `/AsNumber`) und dem Topic. Fehlt die Beschreibung, steht dort der
   * Anzeigename. Die Zeilenumbrüche macht die globale Tooltip-Regel sichtbar.</p>
   */
  readonly tooltip = computed(() => {
    const item = this.item();
    const lines: string[] = [item.description?.trim() || item.displayName];
    const range = this.rangeHint();

    if (range) {
      lines.push(this.t().item.range(range));
    }

    if (item.kind === 'enum') {
      const values = this.options()
        .map((option) => `${option.value} = ${option.label}`)
        .join(' · ');

      if (values) {
        lines.push(this.t().item.values(values));
      }
    }

    if (item.stateTopic) {
      lines.push(this.t().item.topic(item.stateTopic));
    }

    return lines.join('\n');
  });

  /**
   * Der Hinweis statt des Editors.
   *
   * <p>Ein Experten-Item ist ohne Experten-Modus gesperrt (`writable` ist
   * dann `false`, der Server lehnt den Schreibvorgang ab). Der Hinweis nennt
   * deshalb den Grund - „Nur lesbar" allein wäre irreführend.</p>
   */
  readonly lockHint = computed(() => {
    const item = this.item();

    if (item.writable) {
      return '';
    }

    return item.requiresExpertMode ? this.t().item.expertRequired : this.t().item.readonly;
  });

  /**
   * Die Anzeige des aktuellen Werts.
   *
   * <p>Analogwerte werden **im Browser** formatiert, damit die Anzeige nicht
   * zwischen der ersten Ladung (Server-`valueText`) und den SSE-Updates
   * springt. Tausendertrennzeichen ist der Apostroph (siehe `formatNumber`).</p>
   */
  readonly display = computed<string | null>(() => {
    const item = this.item();

    if (item.kind === 'analog' && item.value !== null) {
      const formatted = formatNumber(item.value);

      return item.unit ? `${formatted} ${item.unit}` : formatted;
    }

    return item.valueText;
  });

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
