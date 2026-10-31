# Phase 7 – Parallelbetrieb & Umschaltung (3 PT)

**Ziel:** Der neue Gateway läuft neben dem alten, danach wird umgeschaltet.
**Braucht:** Phase 6
**Spec:** [uebersicht-und-zielbild §7](../02-modernisierung/uebersicht-und-zielbild.md) ·
[mqtt-kompatibilitaet §5](../04-migration/mqtt-kompatibilitaet.md)

> ⚠️ **Die letzte Phase.** Hier wird der alte Dienst abgeschaltet – bis dahin läuft
> er **unverändert** weiter. Der Umschaltpunkt ist der riskanteste Moment des
> Projekts, weil ab hier die Heizung vom neuen Gateway versorgt wird.

---

## 7.1 – Parallel starten · 0.5 PT

| | |
|---|---|
| **Trick** | `BaseTopic=ComfoBox2` |

````bash
docker run -d --name comfobox-new \
  -e COMFOBOX_Gateway__BaseTopic=ComfoBox2 \
  -e COMFOBOX_Bacnet__Port=/dev/ttyUSB1 \
  ... comfobox-gateway:1.0.0
````

> **Wichtig:** Der alte Dienst belegt den seriellen Port. Für den Parallelbetrieb
> braucht man einen **zweiten** Adapter – oder man verschiebt den alten Dienst
> vorübergehend auf den zweiten Port.
>
> **Zwei Master auf einem Bus sind nicht zulässig.** Dieser Punkt ist keine
> Formalität.

**Akzeptanzkriterien:**
- [ ] Beide Gateway laufen mit **unterschiedlichem** `BaseTopic`
- [ ] Beide nutzen **verschiedene** serielle Ports
- [ ] Beide senden LWT – ein Gateway darf den anderen nicht „tot“ melden

---

## 7.2 – Payload-Vergleichstool · 1.0 PT

| | |
|---|---|
| **Ziel** | `tools/compare-payloads/` |

**Zweck:** Zeigen, dass alte und neue Implementierung **dieselben** Werte
publizieren.

| Vergleich | Regel |
|---|---|
| Zahlen | Betrag + Toleranz (0.01) |
| Enums | **exakter** Name (keine Toleranz!) |
| Verfügbarkeit | Zeitstempel-Differenz < 2 Poll-Zyklen |

````bash
python tools/compare-payloads/ \
  --old mqtt://host:1883/ComfoBox/# \
  --new mqtt://host:1883/ComfoBox2/# \
  --duration 300
````

**Akzeptanzkriterien:**
- [ ] Das Tool läuft über einen Zeitraum (Standard 5 min)
- [ ] Es meldet **jede** Abweichung mit Item, alt, neu
- [ ] Enum-Abweichungen sind **immer** ein Fehler (keine Toleranz)
- [ ] Der Exit-Code ist ≠ 0, wenn es Abweichungen gab

---

## 7.3 – HA-Discovery mit `ComfoBox2` · 0.5 PT

| | |
|---|---|
| **Zweck** | Prüfen, dass die Discovery-Payloads gültig sind, **bevor** die echten Entitäten betroffen sind |

**Akzeptanzkriterien:**
- [ ] Die Discovery unter `homeassistant/comfobox2/#` erscheint in HA
- [ ] Alle Komponenten werden angelegt (ohne Fehler im HA-Protokoll)
- [ ] Die `unique_id`s sind **anders** als die des alten Gateways (kein Überschreiben)

> **Achtung:** Wenn die `unique_id`s identisch sind, überschreiben sich die
> Entitäten – der Umschaltpunkt wird dann unvorhersehbar. Deshalb: **vorher**
> prüfen.

---

## 7.4 – Umschaltung · 0.5 PT

| | |
|---|---|
| **Ablauf** | in dieser Reihenfolge |

| Schritt | Aktion | Prüfung |
|---|---|---|
| 1 | alten Gateway **stoppen** (nicht löschen) | Serial-Port ist frei |
| 2 | neuen Gateway auf den **alten** Port umstellen | `BaseTopic=ComfoBox` |
| 3 | neuen Gateway starten | Health = `online` |
| 4 | Discovery neu publizieren lassen | Entitäten aktualisieren sich |
| 5 | HA-Entitäten aufräumen (die alten `ComfoBox2`-Geräte) | keine Duplikate |

**Akzeptanzkriterien:**
- [ ] Nach der Umschaltung sind die Werte in HA **identisch** zu vorher
- [ ] `unique_id`s sind **gleich** geblieben → die Entitäten aktualisieren sich
- [ ] Keine doppelten Entitäten in HA
- [ ] Bestehende Automationen funktionieren **ohne** Änderung

> **Wenn die Automationen brechen:** Das ist der Moment, an dem sich ein
> Payload-Format geändert hätte. Der Contract-Test hätte das verhindern müssen.

---

## 7.5 – Alten Dienst stilllegen & Doku · 0.5 PT

| | |
|---|---|
| **Ziel** | der alte Dienst (Mono auf dem Pi / Windows-Service) wird abgeschaltet |

| Schritt | Aktion |
|---|---|
| 1 | alten Dienst **stoppen**, erst danach **deinstallieren** |
| 2 | Autostart-Eintrag entfernen (systemd, Windows-Dienst, `rc.local`) |
| 3 | Backup der alten Konfiguration aufbewahren |
| 4 | `README.md` auf den neuen Betrieb umstellen |
| 5 | `docs/`-Index aktualisieren |

**Akzeptanzkriterien:**
- [ ] Der alte Dienst läuft **nicht** mehr
- [ ] Kein Autostart-Eintrag mehr vorhanden
- [ ] Die alte Konfiguration ist gesichert (für den Notfall)
- [ ] `README.md` beschreibt den **neuen** Betrieb
- [ ] `docs/06-aufgaben/status.md` ist aktualisiert

> **Den alten Dienst nicht sofort löschen.** Einen Monat parallel aufbewahren –
> dann erst entfernen. Wenn in dieser Zeit etwas ausfällt, ist der Rückweg frei.

---

## Abnahme der Phase 7

- [ ] Neues Gateway läuft parallel mit `BaseTopic=ComfoBox2`
- [ ] Vergleichstool zeigt keine relevanten Abweichungen
- [ ] HA-Discovery mit `ComfoBox2` geprüft
- [ ] `BaseTopic` auf `ComfoBox` umgestellt
- [ ] Discovery-Entitäten aktualisieren sich (gleiche `unique_id`)
- [ ] Alte manuelle HA-Entitäten aufgeräumt
- [ ] Bestehende Automationen geprüft (Payload-Format, Enum-Namen)
- [ ] Alter Dienst gestoppt, Rollback getestet
- [ ] `README.md` und `docs/`-Index aktualisiert
- [ ] Meilenstein **M6** erreicht: „Alte Installation abgeschaltet“

---

## Gesamtabnahme des Projekts

| Kriterium | Erfüllt |
|---|---|
| Alle acht Anforderungen umgesetzt | ☐ |
| Installation per Klick (SPK) + Assistent auf der DS923+ verifiziert | ☐ |
| HA-Integration ohne Broker verifiziert | ☐ |
| Contract-Test grün | ☐ |
| Keine offenen P0-Befunde aus den [Ist-Befunden](../01-ist-zustand/befunde.md) | ☐ |
| Betrieb auf der DS923+ > 7 Tage stabil | ☐ |
| Backup & Restore dokumentiert **und** getestet | ☐ |
| Rollback-Pfad dokumentiert und geübt | ☐ |
| Benutzerdokumentation vollständig | ☐ |
| Dokumentation aktuell | ☐ |
