import type { Translations } from '../../../core/i18n/translations';

/**
 * Die **kontextbezogenen** Fehlerhinweise des Assistenten.
 *
 * <p>Ein „Fehler 500" hilft niemandem - hier steht stattdessen, was zu tun
 * ist (→ [datenmodell-und-api §3.4](../../docs/02-modernisierung/datenmodell-und-api.md)).</p>
 *
 * <p>Die Texte liegen im Wörterbuch (`setupSteps.hint…`) und sind damit in
 * allen unterstützten Sprachen verfügbar.</p>
 */

/** Der Text zu einem maschinenlesbaren Hinweis. */
/*
 * Ein `null`-Hinweis (erfolgreicher Test) und `not_connected` liefern einen
 * leeren Text: die Meldung des Gateways sagt in beiden Fällen schon alles.
 */
export function hintText(hints: Translations['setupSteps'], hint: string | null): string {
  switch (hint) {
    case null:
    case 'not_connected':
      return '';

    case 'serial_port_not_found':
      return hints.hintSerialNotFound;

    case 'no_iam':
      return hints.hintNoIam;

    case 'busy':
      return hints.hintBusy;

    case 'permission_denied':
      return hints.hintPermissionDenied;

    case 'mqtt_unreachable':
      return hints.hintMqttUnreachable;

    case 'mqtt_auth_failed':
      return hints.hintMqttAuthFailed;

    case 'mqtt_tls_failed':
      return hints.hintMqttTlsFailed;

    default:
      return hints.hintDefault;
  }
}

