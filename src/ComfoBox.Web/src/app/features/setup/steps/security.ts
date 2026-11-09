import { ChangeDetectionStrategy, Component, inject, type OnInit } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { CardModule } from '@openng/optimus-ui/card';
import { InputTextModule } from '@openng/optimus-ui/inputtext';
import { MessageModule } from '@openng/optimus-ui/message';
import { PasswordModule } from '@openng/optimus-ui/password';

import { SetupStore } from '../../../core/state/setup.store';
import { I18n } from '../../../core/i18n/i18n';

/**
 * Schritt 4: der Web-Zugang (Administrator und Passwort).
 */
@Component({
  selector: 'app-setup-security',
  imports: [
    CardModule,
    FormsModule,
    InputTextModule,
    MessageModule,
    PasswordModule,
    RouterLink,
  ],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './security.html',
  styleUrl: './steps.scss',
})
export class SecurityStep implements OnInit {
  /** Der Zustand des Assistenten. */
  readonly setup = inject(SetupStore);

  /** Die Texte der gewählten Sprache. */
  readonly t = inject(I18n).t;

  ngOnInit(): void {
    void this.setup.loadSuggestedPort();
  }
}
