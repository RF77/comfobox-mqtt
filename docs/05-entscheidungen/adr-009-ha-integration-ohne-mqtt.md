# ADR-009: Home Assistant – Integration über das Gateway statt eigenem BACnet-Stack

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Home Assistant, Frontend, Deployment, Architektur |
| **Bestätigt durch** | Auftraggeber am 2026-10-31: HA läuft auf **HA OS / Supervised**, Integration über **HTTP/SSE**, Authentifizierung per **API-Token** |

## Kontext

Zusätzlich zu den bisherigen sechs Anforderungen kam der Wunsch:

> *„Es soll auch möglich sein, alles in HA direkt selber zu installieren. Das sollte ja auch
> Docker Images zulassen. Zusätzlich, wohl via HACS hätte ich on top gerne auch ein HA Plugin,
> damit ich auch ohne MQTT Konfiguration direkt auf die Comfobox Zugriff hätte und auch schön
> das als Gerät mit seinen Entities sehen würde. Würde da zusätzlich noch ein direkter Zugriff
> Sinn machen?"*

Daraus ergeben sich vier eigenständige Fragestellungen:

| # | Frage |
|---|---|
| F1 | Wie bekommt der Nutzer das Gateway installiert – **mehrstufig**? |
| F2 | Läuft die Installation **in** Home Assistant (Add-on)? |
| F3 | Wie bekommt HA Entitäten **ohne** MQTT-Broker? (HACS-Integration) |
| F4 | Braucht es zusätzlich einen **direkten** BACnet-Zugriff aus HA heraus? |

### Ausgangslage

Der Ist-Zustand kennt nur M2Mqtt → HA. Ein MQTT-Broker ist zwingende Voraussetzung, die
Discovery ist an das MQTT-Protokoll gekoppelt. Wer keinen Broker betreiben will (oder
Home Assistant die einzige Instanz sein soll), hat heute **keinen** Weg.

Der BACnet-MS/TP-Stack existiert als C#-Portierung (Phase 1 der Roadmap) und ist die
**einzige** Quelle der Item-Metadaten (127 Items, Min/Max, Einheiten, Enum-Namen,
Schreibbarkeit).

### Die harte physikalische Grenze

RS485/BACnet-MS/TP ist ein **Shared Bus mit Token**. Genau **ein** Master darf gleichzeitig
aktiv sein. Zwei aktive Master auf demselben Bus führen zu:

| Phänomen | Folge |
|---|---|
| **I-Am-Kollisionen** | beide Master senden `I-Am` → der Slave antwortet nicht → Timeout |
| **vermisste Antworten** | die Antwort des einen Masters landet beim anderen → falsche Objektwerte |
| **verzögerte Token-Umschläge** | der Token muss 60 Bitzeiten idle sein → doppelte Wartezeit |
| **EEPROM-Stress** | parallele Schreibzugriffe → Schreibzähler des Gateways wird umgangen |

> **Konsequenz:** Gateway **oder** Direktintegration – **nie beides gleichzeitig.**
> Das ist keine Geschmacksfrage, sondern eine physikalische Randbedingung.

## Entscheidung

### E1 – Mehrstufige Installation (F1)

Es gibt **vier** Installationsstufen. Sie bauen aufeinander auf, schließen sich aber nicht aus:

| Stufe | Artefakt | Installation | Zielgruppe |
|---|---|---|---|
| **1** | `docker compose up -d` | Image aus der Registry | alle mit Docker (NAS, Pi, Linux-Server) |
| **2** | `*.spk` | DSM-Paketcenter → *Installieren* | DS923+-Nutzer ohne Docker-Orchestrierung |
| **3** | HA-**Add-on** | HACS → Add-on Store → *Installieren* | HA-OS-/Supervised-Nutzer |
| **4** | HA-**Integration** | HACS → Integration → *Hinzufügen* | alle HA-Nutzer |

→ Details: [Installationsstufen](../03-deployment/installationsstufen.md)
→ [ADR-008](adr-008-synology-spk.md) für Stufe 2.

### E2 – HA-Add-on (F2)

**Ja**, es gibt ein offizielles Home-Add-on. Es nutzt **dasselbe Image** wie Stufe 1 und 2 –
es ist nur ein dritter Installationsweg, kein zweiter Code-Pfad.

> **Bestätigt:** Der Auftraggeber betreibt Home Assistant auf **HA OS / Supervised**
> (Green, Pi oder VM) – damit ist das Add-on der **primäre** Installationsweg für die
> HA-Nutzer, und der HA-Supervisor löst die Portwahl nativ.

| Punkt | Entscheidung |
|---|---|
| Serielle Schnittstelle | **Supervisor-Option** `device(subsystem=tty)` → der Supervisor zeigt eine **Auswahlliste** der seriellen Ports und mappt den gewählten **stabilen** `/dev/serial/by-id/…`-Pfad in den Container |
| Web-Oberfläche | `ingress: true` → die UI erscheint als **Panel in HA** (kein zusätzlicher Port nötig) |
| Erreichbarkeit für die Integration | zusätzlich `ports: { 8080/tcp: 8099 }` → die HA-Integration spricht **direkt** über `http://<ha-host>:8099` mit dem Gateway (Ingress-URLs enthalten ein rotierendes Token und sind für Drittanbieter ungeeignet) |
| Optionen | Der Supervisor schreibt die validierten Optionen nach `/data/options.json` → der Gateway liest diese Datei **optional** und mappt sie auf seine Options-Objekte |
| Überwachung | `watchdog: "http://[HOST]:[PORT:8080]/api/health"` → der Supervisor startet neu, wenn das Gateway hängt |
| Broker | `services: [mqtt:want]` → optional der **interne** Mosquitto der HA-Installation, sonst ein externer Broker |
| Architektur | `aarch64` **und** `amd64` → läuft auf HA-Green, Pi 4/5 und in VMs |

> **Konsequenz für den Build:** Das Image muss **Multi-Arch** (`linux/amd64` **und**
> `linux/arm64`) publiziert werden. Bisher war nur `linux/amd64` vorgesehen
> (siehe [ADR-007](adr-007-ein-container-image.md) und
> [docker.instructions.md](../../.github/instructions/docker.instructions.md)).
> Das ist eine bewusste Erweiterung, weil HA-Green und der Raspberry Pi 5 auf `aarch64` laufen.

> **Warum die Supervisor-Device-Option besser ist als der SPK-Supervisor:** der HA-Supervisor
> löst das „Port unbekannt bei der Installation" **nativ** – mit Dropdown und reboot-stabilem
> Pfad. Der SPK-Supervisor muss das selbst nachbauen (siehe [ADR-008](adr-008-synology-spk.md)).

### E3 – HACS-Integration ohne MQTT (F3)

**Ja**, es gibt eine eigene HA-Integration (`custom_components/comfobox`), die über HACS
installiert wird und ein **Gerät mit allen 127 Entitäten** anlegt – **ohne** MQTT-Broker.

| Punkt | Entscheidung |
|---|---|
| Domain | `comfobox` |
| Transport | **REST/SSE gegen das Gateway** (`/api/items`, `/api/status`, `PUT /api/items/{id}`) |
| `iot_class` | `local_polling` (v1) → später `local_push` mit SSE |
| Authentifizierung | **API-Token (Bearer)**; Cookie-Login als Fallback |
| Gerät | ein Gerät „ComfoBox ELESTA“ (Hersteller Zehnder, Modell ComfoBox Series 5, SW-Version aus `/api/status`) |
| Plattformen | `sensor`, `number`, `select`, `binary_sensor` |

→ Details: [HA-Integration (Spec)](../02-modernisierung/ha-integration.md)

### E4 – Kein direkter BACnet-Zugriff aus HA (F4)

**Nein.** Die Integration spricht mit dem **Gateway**, nicht mit dem Bus.

Begründung:

| Grund | Gewicht |
|---|---|
| **Ein Master** | Zwei gleichzeitige Master sind auf einem MS/TP-Bus nicht zulässig (siehe oben). Die Direktintegration ist damit **nicht mit dem Gateway kombinierbar**. |
| **Eine Quelle der Wahrheit** | Item-Metadaten (Min/Max, Enum-Namen, Schreibbarkeit, Prioritäten) liegen im Gateway-Katalog. Eine Python-Doppelimplementierung divergiert **garantiert** – und divergierende Min/Max bedeuten: Schreibvorgänge außerhalb des gültigen Bereichs. |
| **Schreibschutz** | Das 24-h-Limit und die `InvariantCulture`-Regeln liegen im Gateway. Eine Direktintegration umginge beides → EEPROM-Schutz der ComfoBox verletzt. |
| **Fehlerkanal** | Anforderung 5 verlangt, dass Fehler in **drei** Kanäle fließen (Log, `ComfoBox/Status/Errors`, UI). Eine Direktintegration hat keinen dieser Kanäle. |
| **Aufwand** | Ein MS/TP-Serial-Stack in Python (Framing, Token, Timer, DLL-Kodierung, Reconnect) ist eine **Neuentwicklung** von ca. 600–900 Zeilen – der teuerste Einzelposten des Projekts, obwohl er in C# bereits vorliegt. |

→ Details: [HA-Integration §7 Alternative Direktzugriff](../02-modernisierung/ha-integration.md#7-alternative-direktzugriff-bacnet-in-python--verworfen)

### E5 – Authentifizierung: API-Token

> **Bestätigt:** API-Token (Bearer). Der Cookie-Login bleibt als Fallback für den Browser.

Die Web-UI nutzt **Cookie-Auth** ([ADR-005](adr-005-api-stil.md)). Für die HA-Integration
(die kein Browser ist) kommt ein **API-Token** hinzu:

| Aspekt | Umsetzung |
|---|---|
| Erzeugen | `POST /api/security/tokens` (Admin) → Name + optionale Scopes → Token wird **einmal** im Klartext angezeigt |
| Speicherung | nur der **Hash** (`pbkdf2-sha256$…`) in `config.json` → `security.apiTokens[]` |
| Verwendung | `Authorization: Bearer <token>` |
| Widerruf | `DELETE /api/security/tokens/{id}` |
| Fallback | Cookie-Login bleibt für den Browser |
| Pflicht | sobald das Gateway aus dem LAN heraus erreichbar ist |

## Alternativen

### Zu F2 (HA-Installation)

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **HA-Add-on** ✅ | offizieller Weg, `ingress` eingebaut, `device`-Dropdown löst die Portwahl nativ | – |
| Container in HA-Umgebung „von Hand“ | geht überall | der Nutzer muss Compose-Datei, Ports und Rechte selbst pflegen → unkomfortabel |
| Nur HACS-Integration, ohne Add-on | leicht | das Gateway selbst muss trotzdem irgendwo laufen – ohne Add-on bleibt Stufe 1/2 nötig |

> **Bestätigt:** HA OS / Supervised – damit ist das Add-on der bevorzugte Weg.
> Die manuelle Container-Variante bleibt als Stufe 1/2 für andere Hosts dokumentiert.

### Zu F3 (ohne MQTT)

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **HACS-Integration → Gateway** ✅ | ein Gerät, alle Entitäten, kein Broker nötig, Metadaten aus einer Quelle | – |
| Nur MQTT-Discovery (Status quo) | kein Code | **setzt einen Broker voraus** – genau das wollte der Nutzer vermeiden |
| HA-Discovery + separate manuelle Entitäten | wie Status quo | derselbe Broker-Zwang, plus Doppelpflege |
| Integration → Broker direkt (ohne Gateway) | möglich | dupliziert die Item-Metadaten und braucht trotzdem den Gateway fürs Schreiben |

### Zu F4 (Direktzugriff)

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **Kein Direktzugriff (Gateway)** ✅ | ein Master, eine Wahrheit, Schreibschutz bleibt | – |
| **Direktzugriff per BACnet in Python** | echte Unabhängigkeit vom Gateway | 15–20 PT **zusätzlich**, zweite Implementierung des Protokollstacks, Bus-Konflikt, umgeht den Schreibschutz, kein Fehlerkanal |
| Direktzugriff **und** Gateway, umschaltbar | Flexibilität | der Umschaltpunkt ist fehleranfällig; „welcher Master ist gerade aktiv?" ist eine Frage, die man sich im Heizungsbetrieb nicht stellen will |

> **Bestätigt:** Die Integration spricht ausschließlich mit dem Gateway.
> Der BACnet-Direktzugriff wird **nicht** umgesetzt – auch nicht als Lite-Variante.
> Der Aufwand steckt stattdessen in Add-on, Integration und Tests.

> **Wann ein Direktzugriff trotzdem sinnvoll wäre** (als späte, optionale „Lite"-Variante):
> 1. Der Nutzer betreibt HA **ohne** Docker/Add-on-Möglichkeit (z. B. HA OS auf einem Gerät,
>    auf dem das Add-on nicht läuft) **und** will das Gateway definitiv nicht.
> 2. Der Nutzer braucht nur **Lesen** (z. B. Nur-Lese-Ansicht im Dashboard), nie Schreiben.
> 3. Als **Notfallpfad**, falls das Gateway ausfällt – dann aber nur lesend.
>
> In allen drei Fällen wäre die Lite-Variante **Python + BACnet**, ohne Gateway,
> **nur lesend**, und **niemals parallel** zum Gateway.

## Konsequenzen

### Positiv

- **Ein Image, drei Installationswege** (Compose, SPK, Add-on) → keine drei Code-Pfade
- Der Nutzer wählt die bequemste Stufe – der Rest bleibt identisch
- HA-Nutzer brauchen **keinen** MQTT-Broker: die Integration bringt ihre Entitäten selbst mit
- Ein **Gerät** mit 127 Entitäten in HA (statt 127 lose Entitäten ohne Gerätezuordnung)
- Die Portwahl wird vom Supervisor **nativ** gelöst (Dropdown, reboot-stabil)
- Die Web-UI erscheint als **Panel in HA** (`ingress`) → kein zweiter Tab, kein zweiter Port
- Fehler, Schreibzähler und Health sind in HA sichtbar – Anforderung 5 bleibt erfüllt

### Negativ / Aufwand

- **≈14 PT** zusätzlich (Add-on 3 PT, HACS-Integration 8 PT, Token-API 1 PT, Doku/CI 2 PT)
- **Python** kommt ins Projekt (bisher reine .NET-/TypeScript-Welt) → zweiter Toolchain-Pfad
- Die Integration braucht **Tests** (HA-`pytest`-Harness), sonst wird sie zur
  Wartungsschleife
- `hacs.json`/`repository.yaml` + Release-Automatisierung im CI
- Zwei Repositories nötig: das Haupt-Repo (Integration) und ein **Add-on-Repo**
  (`repository.yaml` im Wurzelverzeichnis) → Release-Pflege für zwei Orte
- Der API-Token ist ein **zweiter** Authentifizierungsweg → mehr Angriffsfläche als
  ein reines Cookie-Modell
- Bei direktem Zugriff auf `/api/items` braucht die Integration einen **Schema-Vertrag**
  mit dem Gateway → Änderungen am DTO sind breaking changes für HA-Nutzer

### Neutral

- Die MQTT-Kompatibilität bleibt **unberührt** – die Integration ist ein **zweiter** Weg
  zum selben Datenbestand, kein Ersatz
- Wer heute MQTT + Discovery nutzt, merkt von der Integration nichts (nur ein zusätzlicher
  Eintrag in der Integrationsliste)
- Der Add-on nutzt dasselbe Image → **kein** zusätzlicher Build, nur ein Manifest und ein
  Multi-Arch-Publish

## Verwandte Dokumente

- [HA-Integration (Spec)](../02-modernisierung/ha-integration.md)
- [Installationsstufen](../03-deployment/installationsstufen.md)
- [ADR-005](adr-005-api-stil.md) (API-Stil) · [ADR-007](adr-007-ein-container-image.md) (ein Image) ·
  [ADR-008](adr-008-synology-spk.md) (SPK)
- `deploy/ha-addon/` · `custom_components/comfobox/`
