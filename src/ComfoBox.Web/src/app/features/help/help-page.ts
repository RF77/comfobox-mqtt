import {
  afterNextRender,
  ChangeDetectionStrategy,
  Component,
  computed,
  HostListener,
  inject,
  Injector,
  type OnInit,
  signal,
} from '@angular/core';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { DomSanitizer, type SafeHtml } from '@angular/platform-browser';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { MessageModule } from '@openng/optimus-ui/message';
import { TagModule } from '@openng/optimus-ui/tag';

import { HelpService } from '../../core/api/help.service';
import type { HelpMeta } from '../../core/models/help.model';

/**
 * Eine einzelne Hilfeseite.
 */
@Component({
  selector: 'app-help-page',
  imports: [ButtonModule, CardModule, MessageModule, RouterLink, TagModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './help-page.html',
  styleUrl: './help.scss',
})
export class HelpPageComponent implements OnInit {
  /** Der Zugriff auf die Hilfe. */
  readonly help = inject(HelpService);

  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly injector = inject(Injector);

  /** Der Slug der Seite. */
  readonly slug = signal('index');

  /** Das gerenderte HTML. */
  readonly html = signal<SafeHtml>('');

  /**
   * Die Metadaten der Seite.
   *
   * <p>Das ist ein `computed`, weil der Suchindex **asynchron** geladen
   * wird: zur ersten Render-Zeit ist er noch leer. Ohne `computed` bliebe
   * die Seite leer, bis ein zweiter Navigationsvorgang sie aktualisierte.
   */
  readonly meta = computed<HelpMeta | null>(() => this.help.meta(this.slug()));

  /** Der Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Der Nachbar nach hinten. */
  readonly previous = computed(() => this.help.neighbours(this.slug()).previous);

  /** Der Nachbar nach vorn. */
  readonly next = computed(() => this.help.neighbours(this.slug()).next);

  ngOnInit(): void {
    void this.help.loadIndex();

    this.route.paramMap.subscribe((params) => {
      const slug = params.get('slug') ?? 'index';
      this.slug.set(slug);

      void this.load(slug);
    });
  }

  /** Lädt die Seite. */
  private async load(slug: string): Promise<void> {
    this.error.set(null);

    try {
      const html = await this.help.getPage(slug);

      // Das HTML stammt aus dem **eigenen** Image (zur Build-Zeit aus
      // docs/benutzer/ erzeugt) - es ist kein Fremdinhalt. Sollte die Doku
      // später zur Laufzeit aus dem Netz geladen, muss auf
      // DomSanitizer.sanitize gewechselt werden.
      this.html.set(this.sanitizer.bypassSecurityTrustHtml(html));

      this.scrollToAnchor();
    } catch (error) {
      this.error.set(
        error instanceof Error
          ? error.message
          : 'Die Hilfeseite konnte nicht geladen werden.',
      );
    }
  }

  /**
   * Springt zum Anker der Route (z. B. `#expertenmodus`).
   *
   * <p>Das muss von Hand geschehen: der Inhalt wird **asynchron** geladen,
   * deshalb existiert das Sprungziel beim Zeitpunkt der Navigation noch
   * nicht - der Scroll-Positionierer des Routers liefe ins Leere.
   */
  private scrollToAnchor(): void {
    const anchor = this.route.snapshot.fragment;

    if (!anchor) {
      return;
    }

    // Wichtig: `afterNextRender` braucht einen Injection-Context. Die Methode
    // läuft nach einem `await` - ohne den expliziten Injector würde der
    // Aufruf fehlschlagen und der Sprung bliebe aus.
    afterNextRender(
      () => {
        // `decodeURIComponent` - der Anker darf URL-kodiert sein.
        let id = anchor;

        try {
          id = decodeURIComponent(anchor);
        } catch {
          id = anchor;
        }

        document.getElementById(id)?.scrollIntoView({ block: 'start' });
      },
      { injector: this.injector },
    );
  }

  /**
   * Fängt die Klicks auf die internen Links der Hilfeseite ab.
   *
   * <p>Die Links zeigen auf `/help/<slug>` (die Route der Oberfläche). Ohne
   * diesen Eingriff würde der Browser die Seite neu laden - die Oberfläche
   * wäre zwar lauffähig, aber der Aufbau ginge verloren.
   *
   * <p>Der Listener hängt am Host-Element (nicht am Template): der Inhalt
   * kommt per `[innerHTML]`, hat also keine eigene Komponente.
   */
  @HostListener('click', ['$event'])
  onClick(event: MouseEvent): void {
    if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey) {
      return;
    }

    const target = event.target;

    if (!(target instanceof Element)) {
      return;
    }

    const link = target.closest('a');

    if (!link) {
      return;
    }

    const href = link.getAttribute('href');

    if (!href || !href.startsWith('/help/')) {
      return;
    }

    event.preventDefault();

    void this.router.navigateByUrl(href);
  }

  /** Wechselt zur Nachbarseite. */
  go(entry: HelpMeta | null): void {
    if (entry) {
      void this.router.navigate(['/help', entry.slug]);
    }
  }
}
