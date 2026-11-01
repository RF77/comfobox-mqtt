import { computed, inject, Injectable, signal } from '@angular/core';

import { toApiError } from '../api/api-client';
import { SetupService } from '../api/setup.service';
import type {
  SerialPortInfo,
  SetupStep,
  TestResultDto,
} from '../models/setup.model';

/**
 * Der Zustand des Getting-Started-Assistenten.
 *
 * <p>Der Assistent ist **wiederholbar**: der Zustand bleibt beim
 * Zurück-Navigieren erhalten, und ein Administrator kann ihn jederzeit neu
 * starten (z. B. um den Serial-Port zu wechseln).
 */
@Injectable({ providedIn: 'root' })
export class SetupStore {
  private readonly api = inject(SetupService);

  /** Der aktuelle Schritt. */
  readonly step = signal<SetupStep>('welcome');

  /** Die erkannten seriellen Ports. */
  readonly ports = signal<readonly SerialPortInfo[]>([]);

  /** Der gewählte serielle Port. */
  readonly selectedPort = signal<string | null>(null);

  /** Sollen alle seriellen Ports durchgereicht werden? */
  readonly passAllDevices = signal(true);

  /** Die Baudrate. */
  readonly baudrate = signal(38400);

  /** Die Geräte-ID des Controllers. */
  readonly masterId = signal(1);

  /** Der Broker-Host. */
  readonly brokerHost = signal('');

  /** Der Broker-Port. */
  readonly brokerPort = signal(1883);

  /** Der Broker-Benutzername. */
  readonly brokerUsername = signal('');

  /** Das Broker-Passwort (nur für den Test, nie gespeichert). */
  readonly brokerPassword = signal('');

  /** Soll TLS verwendet werden? */
  readonly useTls = signal(false);

  /** Der Benutzername des Administrators. */
  readonly adminUsername = signal('');

  /** Das Administrator-Passwort (nur für das Senden, nie gespeichert). */
  readonly adminPassword = signal('');

  /** Die Wiederholung des Passworts. */
  readonly adminPasswordRepeat = signal('');

  /** Der vorgeschlagene Web-Port. */
  readonly suggestedPort = signal<number | null>(null);

  /** Das Ergebnis des BACnet-Tests. */
  readonly bacnetResult = signal<TestResultDto | null>(null);

  /** Das Ergebnis des MQTT-Tests. */
  readonly mqttResult = signal<TestResultDto | null>(null);

  /** Läuft der BACnet-Test? */
  readonly testingBacnet = signal(false);

  /** Läuft der MQTT-Test? */
  readonly testingMqtt = signal(false);

  /** Läuft die Port-Suche? */
  readonly loadingPorts = signal(false);

  /** Wird die Konfiguration übernommen? */
  readonly applying = signal(false);

  /** Wurde die Konfiguration übernommen? */
  readonly applied = signal(false);

  /** Der letzte Fehler (oder `null`). */
  readonly error = signal<string | null>(null);

  /** Die Schritte, die der Assistent anbietet. */
  readonly steps: readonly SetupStep[] = ['welcome', 'serial', 'bacnet', 'mqtt', 'security', 'done'];

  /** Der Index des aktuellen Schritts. */
  readonly stepIndex = computed(() => this.steps.indexOf(this.step()));

  /** Sind alle Pflichtangaben vollständig? */
  readonly isComplete = computed(
    () =>
      this.selectedPort() !== null &&
      this.bacnetResult()?.ok === true &&
      this.mqttResult()?.ok === true &&
      this.adminUsername().trim().length >= 3 &&
      this.adminPassword().length >= 12 &&
      this.adminPassword() === this.adminPasswordRepeat(),
  );

  /** Die Stärke des Passworts (0–4). */
  readonly passwordStrength = computed(() => scorePassword(this.adminPassword()));

  /** Die Klassen des Passworts (0–4). */
  readonly passwordStrengthClass = computed(() => {
    switch (this.passwordStrength()) {
      case 4:
        return 'cb-strength-4';
      case 3:
        return 'cb-strength-3';
      case 2:
        return 'cb-strength-2';
      case 1:
        return 'cb-strength-1';
      default:
        return 'cb-strength-0';
    }
  });

  /** Die Fehler der Passwortregeln (leer = alles in Ordnung). */
  readonly passwordHints = computed(() => passwordHints(this.adminPassword()));

  /** Setzt den Schritt. */
  goTo(step: SetupStep): void {
    this.step.set(step);
  }

  /** Geht einen Schritt zurück. */
  back(): void {
    const index = this.stepIndex();
    const previous = this.steps[index - 1];

    if (previous) {
      this.step.set(previous);
    }
  }

  /** Geht einen Schritt vor (nur wenn er erlaubt ist). */
  next(): void {
    const index = this.stepIndex();
    const next = this.steps[index + 1];

    if (next && this.canAdvance(index)) {
      this.step.set(next);
    }
  }

  /** Darf der aktuelle Schritt verlassen werden? */
  canAdvance(index = this.stepIndex()): boolean {
    switch (this.steps[index]) {
      case 'serial':
        return this.selectedPort() !== null;
      case 'bacnet':
        return this.bacnetResult()?.ok === true;
      case 'mqtt':
        return this.mqttResult()?.ok === true;
      case 'security':
        return this.adminUsername().trim().length >= 3 && this.passwordHints().length === 0;
      default:
        return true;
    }
  }

  /** Lädt die Liste der seriellen Ports. */
  async reloadPorts(): Promise<void> {
    this.loadingPorts.set(true);
    this.error.set(null);

    try {
      const ports = await this.api.getPorts();
      this.ports.set(ports);

      // Ist noch nichts ausgewählt, wird der erste Port übernommen.
      if (this.selectedPort() === null && ports.length > 0) {
        this.selectedPort.set(ports[0]?.port ?? null);
      }
    } catch (error) {
      this.error.set(toApiError(error).message);
    } finally {
      this.loadingPorts.set(false);
    }
  }

  /** Lädt den Vorschlag für den Web-Port. */
  async loadSuggestedPort(): Promise<void> {
    try {
      this.suggestedPort.set(await this.api.suggestPort());
    } catch {
      this.suggestedPort.set(null);
    }
  }

  /** Testet die BACnet-Verbindung. */
  async testBacnet(): Promise<void> {
    this.testingBacnet.set(true);
    this.error.set(null);

    try {
      const result = await this.api.testBacnet({
        port: this.selectedPort(),
        baudrate: this.baudrate(),
      });

      this.bacnetResult.set(result);
    } catch (error) {
      this.error.set(toApiError(error).message);
      this.bacnetResult.set(null);
    } finally {
      this.testingBacnet.set(false);
    }
  }

  /** Testet die MQTT-Verbindung. */
  async testMqtt(): Promise<void> {
    this.testingMqtt.set(true);
    this.error.set(null);

    try {
      const result = await this.api.testMqtt({
        host: this.brokerHost().trim(),
        port: this.brokerPort(),
        username: this.brokerUsername().trim() || null,
        password: this.brokerPassword() || null,
        useTls: this.useTls(),
      });

      this.mqttResult.set(result);
    } catch (error) {
      this.error.set(toApiError(error).message);
      this.mqttResult.set(null);
    } finally {
      this.testingMqtt.set(false);
    }
  }

  /** Schreibt die Konfiguration und löst den Neustart aus. */
  async apply(): Promise<void> {
    this.applying.set(true);
    this.error.set(null);

    try {
      await this.api.writeConfig({
        port: this.selectedPort(),
        baudrate: this.baudrate(),
        brokerHost: this.brokerHost().trim() || null,
        brokerPort: this.brokerPort(),
        brokerUsername: this.brokerUsername().trim() || null,
        brokerPassword: this.brokerPassword() || null,
        useTls: this.useTls(),
        adminUsername: this.adminUsername().trim() || null,
        adminPassword: this.adminPassword() || null,
      });

      await this.api.apply();

      this.applied.set(true);

      // Das Passwort wird nicht mehr gebraucht - es bleibt nicht im State.
      this.adminPassword.set('');
      this.adminPasswordRepeat.set('');
      this.brokerPassword.set('');
    } catch (error) {
      this.error.set(toApiError(error).message);
    } finally {
      this.applying.set(false);
    }
  }

  /** Setzt den Assistenten zurück (wiederholbar). */
  reset(): void {
    this.step.set('welcome');
    this.bacnetResult.set(null);
    this.mqttResult.set(null);
    this.applied.set(false);
    this.error.set(null);
  }
}

/**
 * Bewertet die Passwortstärke (0–4).
 *
 * <p>Die Regeln: mindestens 12 Zeichen und mindestens 3 von 4 Klassen
 * (Großbuchstaben, Kleinbuchstaben, Ziffern, Symbole).
 */
export function scorePassword(password: string): number {
  if (password.length === 0) {
    return 0;
  }

  let classes = 0;

  if (/[A-ZÄÖÜ]/.test(password)) {
    classes++;
  }

  if (/[a-zäöüß]/.test(password)) {
    classes++;
  }

  if (/\d/.test(password)) {
    classes++;
  }

  if (/[^A-Za-zÄÖÜäöüß\d\s]/.test(password)) {
    classes++;
  }

  let score = 0;

  if (password.length >= 12) {
    score++;
  }

  if (password.length >= 16) {
    score++;
  }

  if (classes >= 3) {
    score++;
  }

  if (classes === 4 && password.length >= 16) {
    score++;
  }

  return Math.min(score, 4);
}

/**
 * Die offenen Passwort-Hinweise (leer = alles in Ordnung).
 */
export function passwordHints(password: string): readonly string[] {
  const hints: string[] = [];

  if (password.length < 12) {
    hints.push('Mindestens 12 Zeichen.');
  }

  let classes = 0;

  if (/[A-ZÄÖÜ]/.test(password)) {
    classes++;
  }

  if (/[a-zäöüß]/.test(password)) {
    classes++;
  }

  if (/\d/.test(password)) {
    classes++;
  }

  if (/[^A-Za-zÄÖÜäöüß\d\s]/.test(password)) {
    classes++;
  }

  if (password.length > 0 && classes < 3) {
    hints.push('Mindestens 3 von 4 Zeichenklassen (Groß-, Kleinbuchstaben, Ziffern, Symbole).');
  }

  return hints;
}
