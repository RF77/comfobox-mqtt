import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CardModule } from '@openng/optimus-ui/card';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { TagModule } from '@openng/optimus-ui/tag';

import { HelpService } from '../../core/api/help.service';
import { I18n } from '../../core/i18n/i18n';

/**
 * Der Index der Hilfeseiten (mit Volltextsuche).
 */
@Component({
  selector: 'app-help-index',
  imports: [CardModule, FormsModule, InputTextModule, MessageModule, RouterLink, TagModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './help-index.html',
  styleUrl: './help.scss',
})
export class HelpIndexComponent {
  /** Der Zugriff auf die Hilfe. */
  readonly help = inject(HelpService);

  /** Die Texte der gewählten Sprache. */
  readonly t = inject(I18n).t;

  /** Der Suchbegriff. */
  readonly term = signal('');

  /** Die Treffer (leer = alle Seiten). */
  readonly results = computed(() => {
    const term = this.term().trim();

    if (term.length === 0) {
      return [...this.help.index()].sort((a, b) => a.order - b.order);
    }

    return this.help.search(term);
  });

  constructor() {
    void this.help.loadIndex();
  }
}
