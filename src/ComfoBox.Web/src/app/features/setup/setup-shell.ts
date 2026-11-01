import { ChangeDetectionStrategy, Component, computed, DestroyRef, inject, type OnInit } from '@angular/core';
import { NavigationEnd, Router, RouterLink, RouterOutlet } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { MessageModule } from '@openng/optimus-ui/message';
import { StepsModule } from '@openng/optimus-ui/steps';
import { filter } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { SetupStore } from '../../core/state/setup.store';
import type { SetupStep } from '../../core/models/setup.model';

/**
 * Das Grundgerüst des Getting-Started-Assistenten.
 *
 * <p>Die Schritte stehen in einer Liste mit Vor/Zurück - kein linearer
 * Wizard, sondern eine Seitenschritt-Ansicht mit erhaltenem Zustand.
 */
@Component({
  selector: 'app-setup-shell',
  imports: [ButtonModule, MessageModule, RouterLink, RouterOutlet, StepsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './setup-shell.html',
  styleUrl: './setup-shell.scss',
})
export class SetupShellComponent implements OnInit {
  /** Der Zustand des Assistenten. */
  readonly setup = inject(SetupStore);

  private readonly router = inject(Router);

  private readonly destroyRef = inject(DestroyRef);

  ngOnInit(): void {
    // Die gespeicherten Werte vorladen - damit ist der Assistent auch nach
    // der Ersteinrichtung wiederholbar, ohne dass alles neu eingegeben
    // werden muss.
    void this.setup.loadState();

    // Der direkte Aufruf einer Schritt-URL (z. B. /setup/serial) muss den
    // Zustand setzen: die erste Navigation ist schon vor ngOnInit
    // abgeschlossen, es kommt also kein NavigationEnd mehr.
    this.syncStepFromUrl(this.router.url);

    // Der Zurück-Knopf des Browsers muss den Schritt mitführen - sonst
    // zeigt die Fortschrittsanzeige einen anderen Schritt als die Seite.
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((event) => this.syncStepFromUrl(event.urlAfterRedirects));
  }

  /** Übernimmt den Schritt aus der URL in den Zustand. */
  private syncStepFromUrl(url: string): void {
    const step = url.split('?')[0]?.split('/').pop();

    if (step && this.setup.steps.includes(step as SetupStep)) {
      this.setup.goTo(step as SetupStep);
    }
  }

  /** Die Schrittbeschriftungen. */
  readonly labels: Readonly<Record<string, string>> = {
    welcome: 'Willkommen',
    serial: 'Schnittstelle',
    bacnet: 'BACnet',
    mqtt: 'MQTT',
    security: 'Web-Zugang',
    done: 'Fertig',
  };

  /** Der Fortschritt in Prozent (für die Textangabe). */
  readonly progress = computed(() =>
    Math.round(((this.setup.stepIndex() + 1) / this.setup.steps.length) * 100),
  );

  /** Die Schrittliste für die Anzeige. */
  readonly items = computed(() =>
    this.setup.steps.map((step, index) => ({
      step,
      label: this.labels[step] ?? step,
      route: `/setup/${step}`,
      active: index === this.setup.stepIndex(),
      completed: index < this.setup.stepIndex(),
    })),
  );

  /** Springt zu einem Schritt (die Schrittliste oben). */
  goTo(step: string): void {
    if (!this.setup.steps.includes(step as SetupStep)) {
      return;
    }

    this.setup.goTo(step as SetupStep);
    void this.router.navigate(['/setup', step]);
  }

  /** Geht einen Schritt zurück. */
  back(): void {
    this.setup.back();
    void this.router.navigate(['/setup', this.setup.step()]);
  }

  /** Geht einen Schritt vor. */
  next(): void {
    this.setup.next();
    void this.router.navigate(['/setup', this.setup.step()]);
  }
}