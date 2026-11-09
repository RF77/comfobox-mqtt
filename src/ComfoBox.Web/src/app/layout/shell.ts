import { ChangeDetectionStrategy, Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { SelectModule } from '@openng/optimus-ui/select';

import { EventsService } from '../core/api/events.service';
import { I18n } from '../core/i18n/i18n';
import type { Language } from '../core/i18n/translations';
import { HealthStore } from '../core/state/health.store';
import { ItemsStore } from '../core/state/items.store';
import { SessionStore } from '../core/state/session.store';
import { HealthBannerComponent } from '../shared/health-banner';

/**
 * Das Grundgerüst der Oberfläche: Kopfleiste, Navigation und der Bereich
 * für den Health-Banner.
 */
@Component({
  selector: 'app-shell',
  imports: [
    ButtonModule,
    FormsModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
    SelectModule,
    HealthBannerComponent,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class ShellComponent {
  /** Der Zustand des Gateways. */
  readonly health = inject(HealthStore);

  /** Der Zustand der Sitzung. */
  readonly session = inject(SessionStore);

  /** Die Items (für die Schnellzüge auf dem Dashboard). */
  readonly items = inject(ItemsStore);

  /** Die Sprache der Oberfläche. */
  readonly i18n = inject(I18n);

  /** Die Texte. */
  readonly t = this.i18n.t;

  private readonly events = inject(EventsService);
  private readonly router = inject(Router);

  /** Ist die Navigation eingeklappt (Handy)? */
  readonly menuOpen = signal(false);

  /** Die Navigationspunkte. */
  readonly navigation = computed(() => {
    const texts = this.t();

    return [
      { label: texts.nav.dashboard, icon: 'pi pi-home', route: '/' },
      { label: texts.nav.items, icon: 'pi pi-sliders-h', route: '/items' },
      { label: texts.nav.config, icon: 'pi pi-cog', route: '/config' },
      { label: texts.nav.diagnostics, icon: 'pi pi-wrench', route: '/diagnostics' },
      { label: texts.nav.help, icon: 'pi pi-book', route: '/help' },
    ];
  });

  /** Die Sprachwahl. */
  readonly languages = this.i18n.options;

  constructor() {
    // Der SSE-Stream liefert die Live-Werte - er wird beim Start der
    // Anwendung aufgebaut und beim Verlassen wieder abgebaut.
    effect(() => {
      this.events.connect();
      void this.health.load();
      void this.items.load();
    });
  }

  /** Wechselt die Sprache. */
  setLanguage(language: Language): void {
    this.i18n.set(language);
  }

  /** Schließt das Menü (nach der Auswahl auf dem Handy). */
  closeMenu(): void {
    this.menuOpen.set(false);
  }

  /** Meldet den Benutzer ab und kehrt zur Anmeldung zurück. */
  async logout(): Promise<void> {
    await this.session.logout();
    await this.router.navigate(['/login']);
  }
}
