import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ButtonModule } from '@openng/optimus-ui/button';
import { CardModule } from '@openng/optimus-ui/card';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { PasswordModule } from '@openng/optimus-ui/password';
import { SelectModule } from '@openng/optimus-ui/select';

import { I18n } from '../../core/i18n/i18n';
import type { Language } from '../../core/i18n/translations';
import { SessionStore } from '../../core/state/session.store';

/**
 * Die Anmeldung.
 *
 * <p>Das Passwort wird **einmal** im Klartext an das Gateway gesendet (PBKDF2
 * kostet dort rund 0,3 s) und danach **nicht** gespeichert - weder im State
 * noch im `localStorage`.
 */
@Component({
  selector: 'app-login',
  imports: [
    ButtonModule,
    CardModule,
    FormsModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    RouterLink,
    SelectModule,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './login.html',
  styleUrl: './login.scss',
})
export class LoginComponent {
  /** Der Zustand der Sitzung. */
  readonly session = inject(SessionStore);

  /** Die Sprache der Oberfläche. */
  readonly i18n = inject(I18n);

  /** Die Texte. */
  readonly t = this.i18n.t;

  /** Die Sprachwahl. */
  readonly languages = this.i18n.options;

  private readonly router = inject(Router);

  /** Der Benutzername. */
  readonly userName = signal('');

  /** Das Passwort (nur für den Aufruf). */
  readonly password = signal('');

  /** Läuft die Anmeldung? */
  readonly busy = signal(false);

  /** Der Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Meldet sich an. */
  async login(): Promise<void> {
    this.busy.set(true);
    this.error.set(null);

    const ok = await this.session.login(this.userName().trim(), this.password());

    this.busy.set(false);

    // Das Passwort wird sofort verworfen.
    this.password.set('');

    if (!ok) {
      this.error.set(this.session.error() ?? this.t().login.invalid);
      return;
    }

    await this.router.navigate(['/']);
  }

  /** Wechselt die Sprache. */
  setLanguage(language: Language): void {
    this.i18n.set(language);
  }
}
