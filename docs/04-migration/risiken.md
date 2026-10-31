# Risiken

## 1. Risikoregister

| ID | Risiko | Eintritt | Auswirkung | Wahrscheinlichkeit | Gegenmaßnahme |
|---|---|---|---|---|---|
| **R1** | **BACnet-Portierung verändert das Verhalten** (Byte-Fehler im Codec) | mittel | **kritisch** – Heizung steuert nicht mehr | mittel | Golden-File-Tests **vor** der Portierung; Codec unverändert übernehmen; Review durch BACnet-Verständigen |
| **R2** | Item-Katalog verliert oder verändert Einträge (127 → 126) | mittel | hoch – einzelne Werte fehlen | mittel | Contract-Test gegen `docs/topics.json`; Generator-Skript statt Handarbeit |
| **R3** | **EEPROM-Schreibschutz wird umgangen** (zähler nicht persistent / Zähler-Bug) | hoch | **kritisch** – Garantieverlust, Hardware-Schaden | hoch | Persistenter Zähler (Phase 2) + Test `Count_Persists_AcrossRestart`; **erst danach** produktives Schreiben |
| **R4** | Bestehende HA-Automationen brechen (Payload-Interpretation, `,` vs. `.`) | mittel | mittel – Heizung schaltet nicht wie erwartet | mittel | Migrations-Guide mit `,`→`.`-Konvertierung; `Status/Errors` meldet Abweisungen; parallel betreiben |
| **R5** | DS923+ bekommt den seriellen Port nicht durchgereicht | mittel | kritisch – kein Betrieb | mittel | Früher Prototyp (Phase 0/1) auf dem NAS; `by-id`-Pfad; udev-Regel; Fehlerbehandlung mit Retry |
| **R6** | Zeitplan wird unterschritten | hoch | mittel | mittel | 25 % Puffer; Phasen einzeln freigeben; Golden-Tests vermeiden Debug-Sessions |
| **R7** | `System.IO.Ports` verhält sich unter Linux anders (Zeitouts, Pufferung) | mittel | hoch – instabiles Polling | mittel | Portabilitäts-Test des MSTP-Transports unter Linux in CI (echter Port-Loopback) |
| **R8** | Home-Assistant-Discovery-Payloads sind fehlerhaft → Entitäten fehlen | mittel | mittel | mittel | Discovery-Schema-Test; Sichtprüfung im Test-HA; Beta-Phase mit dem Nutzer |
| **R9** | MQTTnet-Verhalten weicht von M2Mqtt ab (QoS, Retain, LWT-Delay) | mittel | mittel | mittel | Integrationstests gegen echten Mosquitto; `Connected=1` nach Reconnect explizit testen (A10) |
| **R10** | Speicherleck im Langzeitbetrieb (Value-Cache, Event-Subscriptions) | niedrig | mittel | niedrig | `dotnet-counters`/`dotnet-diagnostics` im Langzeittest; Cache-Größe begrenzen |
| **R11** | **DS923+-Variante ohne `dialout`-Gruppe / abweichende GIDs** | mittel | hoch – Permission-Fehler | mittel | GID im Deployment prüfen; `group_add` mit **numerischer** GID; Fehlermeldung „Permission denied" → Health `degraded` |
| **R12** | Das alte System fällt während der Migration aus | mittel | kritisch – Heizung ohne Steuerung | niedrig | Parallelbetrieb (Phase 7); alter Dienst bleibt aktiv; Notfall-Rollback dokumentiert |
| **R13** | Angreifer nutzt den unauthentifizierten Broker für Schreibzugriffe | mittel | **kritisch** – unerlaubte Parameteränderungen | mittel | MQTT-Auth (MQTTnet), `ExpertMode` abschaltbar, `RestartEnabled` abschaltbar, ACLs im Mosquitto, UI nicht ins Internet |
| **R14** | Angular/Optimus UI: Breaking Change in einer Minor-Version | niedrig | niedrig | mittel | exakte Version pinnen (`package-lock.json`), `ng update` nur bewusst |
| **R15** | Team-Wissen geht verloren (alleiniges Projekt, 10 Jahre ohne Weiterentwicklung) | mittel | mittel | mittel | diese Dokumentation; ADR; ausführliche Code-Kommentare an kritischen Stellen |
| **R16** | `ComfoBox` wird auf 76800 zurückgestellt (Werks-Default) | mittel | mittel | niedrig | Baudrate in der Config sichtbar; UI zeigt die eingestellte Baudrate; Diagnosehinweis |

## 2. Risikokategorien

### 2.1 Technische Risiken

| Bereich | Hauptrisiko | Mitigation |
|---|---|---|
| BACnet | Fremdcode ohne Tests | Golden-Files vor Portierung (R1) |
| Datenmodell | Verlust der Item-Definitionen | `docs/topics.json` als unveränderliche Fixture (R2) |
| MQTT | Semantik-Änderungen | Integrationstests + Contract-Tests (R4, R9) |
| Plattform | Linux/Serial-Abweichungen | Tests in CI auf Linux (R7) |

### 2.2 Betriebsrisiken

| Bereich | Hauptrisiko | Mitigation |
|---|---|---|
| Hardware | EEPROM-Verschleiß | persistenter Schreibschutz (R3) |
| Deployment | Serial-Port auf dem NAS | früher Prototyp + Diagnose (R5, R11) |
| Ausfall | Gateway läuft nicht | Parallelbetrieb + Rollback (R12) |

### 2.3 Sicherheitsrisiken

| Bereich | Hauptrisiko | Mitigation |
|---|---|---|
| Broker | unauthentifizierter Zugriff | Auth + ACLs + `ExpertMode` abschaltbar (R13) |
| Restart | DoS über MQTT | `RestartEnabled` abschaltbar, optional Token |
| UI | unauthentifizierte Konfiguration | API-Key oder Reverse-Proxy-Auth (siehe [offene Frage Q2](../02-modernisierung/uebersicht-und-zielbild.md#9-offene-fragen-entscheidung-durch-den-auftraggeber)) |

## 3. Höchste Risiken im Detail

### R3 – EEPROM-Schreibschutz (kritisch, höchste Priorität)

**Problem:** Der Ist-Zustand zählt Schreibvorgänge nur im Prozessspeicher. Ein Neustart setzt den
Zähler zurück → die 24-h-Grenze von 200 wird umgangen → der Controller kann über die
1.000.000-Schreibzyklengrenze hinaus beschädigt werden.

**Maßnahmen:**

1. Persistenter Zähler im Docker-Volume (`/var/lib/comfobox`).
2. Zähler wird nach **jedem** Schreibvorgang geschrieben (atomar).
3. Kalendarisches Fenster statt Prozesslaufzeit.
4. Fehlgeschlagene Writes zählen nicht.
5. **Reihenfolge-Constraint:** Vor dem ersten produktiven Schreibvorgang muss der persistente
   Zähler live sein (Phase 2, Task 2.6).
6. `MaxNumberOfWritesPer24h` nie auf 0/„unbegrenzt" setzen. Der Wert 0 deaktiviert den Schutz
   – das darf nur mit expliziter Bestätigung möglich sein.

**Restrisiko:** Wird das Volume gelöscht (`docker compose down -v`), ist der Zähler weg. → Deshalb:
`down -v` nur mit expliziter Bestätigung, Backup im Deployment-Guide.

### R1 – BACnet-Verhaltensänderung

**Problem:** 12.800 Zeilen Fremdcode werden auf eine andere Runtime portiert.

**Maßnahmen:**

1. Golden-File-Tests **zuerst** gegen den alten Code (Phase 1, Task 1.2).
2. Codec (`BACnetBase`) möglichst **1:1** übernehmen – keine „Verbesserungen" während der Portierung.
3. Nur die 3 ungenutzten Transporte entfernen (Pipe, UDP, PTP) – der MSTP-Code bleibt unangetastet.
4. Zeitverhalten dokumentieren: `T_REPLY_TIMEOUT` 295 ms usw. unverändert lassen.
5. Review-Schwerpunkt: alle Stellen mit `lock`, `m_invoke_id++`, Timeouts.

**Abbruchkriterium:** Wenn ein Golden-Test nach der Portierung fehlschlägt und die Ursache nicht
**eindeutig** eine Verbesserung ist → Portierung stoppen, zurück zur Analyse.

### R5/R11 – Serial-Port auf dem DS923+

**Problem:** Container + Serial + NAS = mehrere Fehlerquellen (udev, Rechte, Pfad, Belegung).

**Maßnahmen:**

1. **Prototyp in Phase 0/1** auf dem realen NAS: Container starten, Port öffnen, `IAm` empfangen.
2. `by-id`-Pfad statt `ttyUSB0`.
3. `group_add` mit **numerischer GID** (DSM-spezifisch: `dialout` ist üblicherweise GID 20).
4. Klare Fehlermeldung bei `UnauthorizedAccessException` → Health `degraded` mit
   `bacnet_port_permission_denied`.
5. Retry mit Backoff, damit ein spät enumerierter USB-Port nicht zum Crash führt.

**Abbruchkriterium:** Wenn der Serial-Durchschliff auf der DS923+ nicht funktioniert → Plan B:
**TCP-Serial-Gateway** (siehe [synology-ds923.md §8](../03-deployment/synology-ds923.md#8-alternative-zum-usb-adapter-tcp-serial-gateway)).
Der BACnet-Transport wird deshalb von Anfang an hinter einem Interface gehalten.

## 4. Risiko-Matrix

| | **gering** | **mittel** | **hoch** |
|---|---|---|---|
| **kritisch** | – | R1, R5 | R3 |
| **hoch** | – | R2, R7, R11, R13 | R12 |
| **mittel** | R10, R14, R16 | R4, R6, R8, R9, R15 | – |
| **gering** | – | – | – |

## 5. Frühwarnindikatoren

| Indikator | Bedeutung | Reaktion |
|---|---|---|
| Golden-Tests schlagen **sofort** nach der ersten Portierungs-Änderung fehl | Grundannahme „verhaltensgleich" falsch | Portierung stoppen, Analyse wiederholen |
| Contract-Test schlägt bei **jedem** Item fehl | Katalog-Quelle falsch (nicht `docs/topics.json`) | Generator-Skript prüfen |
| `bash` zeigt 100 % CPU im Container | A8 nicht behoben | Sofort beheben, nicht deployen |
| `Status/Health` bleibt `degraded` nach 5 min | Port/Gerät/Broker-Problem | Diagnose-Sektion der UI |
| HA-Entitäten fehlen, Discovery-Payload vorhanden | Discovery-Schema-Fehler | Payload gegen HA-Doku prüfen |

## 6. Rollback-Plan

| Stufe | Rollback |
|---|---|
| Vor Phase 7 | Alte Installation ist unverändert → einfach zurückschalten (`BaseTopic`/Container stoppen) |
| Während Phase 7 | Beide Systeme parallel → alten Dienst wieder starten, `BaseTopic` des neuen auf `ComfoBox2` |
| Nach Phase 7 | Git-Tag der alten Version (`v0.5.0`) ist archiviert → Build + Deployment reproduzierbar |

> **Wichtig:** Der Ist-Zustand wird **nicht** gelöscht, sondern in einen Branch `legacy/net452`
> verschoben. Nur so ist ein Rollback jederzeit möglich.
