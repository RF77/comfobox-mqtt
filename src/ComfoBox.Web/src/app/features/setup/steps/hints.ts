/**
 * Die **kontextbezogenen** Fehlerhinweise des Assistenten.
 *
 * <p>Ein „Fehler 500" hilft niemandem - hier steht stattdessen, was zu tun
 * ist (→ [datenmodell-und-api §3.4](../../docs/02-modernisierung/datenmodell-und-api.md)).
 */

/** Der Text zu einem maschinenlesbaren Hinweis. */
export function hintText(hint: string): string {
  switch (hint) {
    case 'serial_port_not_found':
      return 'Der gewählte Port ist nicht mehr vorhanden. Bitte Adapter neu einstecken oder einen anderen Port wählen.';

    case 'no_iam':
      return 'Die ComfoBox antwortet nicht. Prüfen Sie: (1) steht sie auf 38400 Baud und wurde neu gestartet? (2) sind A und B richtig angeschlossen? (3) ist der Adapter an einem anderen Port?';

    case 'busy':
      return 'Der Port wird bereits von einem anderen Programm verwendet.';

    case 'permission_denied':
      return 'Keine Berechtigung auf den Port. Die Gruppe „dialout" fehlt.';

    case 'mqtt_unreachable':
      return 'Der Broker ist nicht erreichbar. Prüfen Sie Host, Port und ob der Broker läuft.';

    case 'mqtt_auth_failed':
      return 'Die Anmeldung am Broker ist fehlgeschlagen. Benutzername und Passwort prüfen.';

    case 'mqtt_tls_failed':
      return 'Die TLS-Verhandlung ist fehlgeschlagen. Zertifikat oder Broker-Konfiguration prüfen.';

    default:
      return 'Weitere Hinweise finden Sie in der Hilfe.';
  }
}
