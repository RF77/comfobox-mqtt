import { ChangeDetectionStrategy, Component, effect, inject, signal } from '@angular/core';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MenuModule } from '@openng/optimus-ui/menu';

import { EventsService } from '../core/api/events.service';
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
    MenuModule,
    RouterLink,
    RouterLinkActive,
    RouterOutlet,
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

  private readonly events = inject(EventsService);
  private readonly router = inject(Router);

  /** Ist die Navigation eingeklappt (Handy)? */
  readonly menuOpen = signal(false);

  /** Die Navigationspunkte. */
  readonly navigation = [
    { label: 'Übersicht', icon: 'pi pi-home', route: '/' },
    { label: 'Geräte', icon: 'pi pi-sliders-h', route: '/items' },
    { label: 'Konfiguration', icon: 'pi pi-cog', route: '/config' },
    { label: 'Diagnose', icon: 'pi pi-wrench', route: '/diagnostics' },
    { label: 'Hilfe', icon: 'pi pi-book', route: '/help' },
  ];

  constructor() {
    // Der SSE-Stream liefert die Live-Werte - er wird beim Start der
    // Anwendung aufgebaut und beim Verlassen wieder abgebaut.
    effect(() => {
      this.events.connect();
      void this.health.load();
      void this.items.load();
    });
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
