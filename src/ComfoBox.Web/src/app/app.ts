import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { ToastModule } from '@openng/optimus-ui/toast';

/**
 * Die Wurzel der Oberfläche.
 *
 * <p>Hier steht der **einzige** Toaster der Anwendung. Der
 * <c>MessageService</c> wird von fast allen Seiten benutzt; ohne ein
 * <c>p-toast</c> im Template werden seine Meldungen stillschweigend
 * verworfen - der Benutzer sieht weder „Gespeichert“ noch einen Fehler.</p>
 *
 * <p>Bewusst hier und **nicht** in der Shell: Anmeldung, Einrichtung und
 * Hilfe liegen außerhalb der Shell. Zwei Toaster würden außerdem jede
 * Meldung doppelt anzeigen.</p>
 */
@Component({
  selector: 'app-root',
  imports: [RouterOutlet, ToastModule],
  templateUrl: './app.html',
  styleUrl: './app.scss',
})
export class App {}
