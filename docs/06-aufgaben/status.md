# Fortschritt

> **Eine Zeile pro Aufgabe.** Status: `⬜` offen · `🟡` in Arbeit · `✅` fertig · `⛔` blockiert
>
> Diese Datei wird **nach jeder Session** aktualisiert. Wer hier nachschaut, weiß,
> wo das Projekt steht.

## Legende

| Symbol | Bedeutung |
|---|---|
| ⬜ | offen |
| 🟡 | in Arbeit |
| ✅ | fertig **und verifiziert** |
| ⛔ | blockiert (Grund im [Protokoll](protokoll.md)) |

## Phase 0 – Repo-Hygiene & CI (2 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 0.1 | `global.json` (SDK-Pinning 10.0.400) | ✅ | 0.2 |
| 0.2 | `Directory.Packages.props` (Central Package Management) | ✅ | 0.3 |
| 0.3 | `Directory.Build.props` (Nullable, Warnungen als Fehler) | ✅ | 0.2 |
| 0.4 | `src/ComfoBox.sln` anlegen (SDK-Projekte) | ✅ | 0.3 |
| 0.5 | `src/.gitignore` vervollständigen | ✅ | 0.1 |
| 0.6 | CI: `build-test.yml` + `codeql.yml` | ✅ | 0.4 |
| 0.7 | Fixtures fixieren (`docs/topics.json`, `MoMA Scan Results.html`) | ✅ | 0.3 |
| 0.8 | `CHANGELOG.md` anlegen | ✅ | 0.2 |

## Phase 1 – BACnet-Portierung & Golden-Tests (8 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 1.1 | Golden-Frames extrahieren (Oracle über den Legacy-Codec) | ✅ | 0.5 |
| 1.2 | Golden-Tests gegen den **alten** Code (Oracle) | ✅ | 1.0 |
| 1.3 | `ComfoBox.Bacnet` anlegen, `BACnetBase` portieren | ✅ | 1.0 |
| 1.4 | `BACnetClient` portieren | ✅ | 1.5 |
| 1.5 | `BACnetTransport` (MSTP + Serial) portieren | ✅ | 2.0 |
| 1.6 | Pipe-/UDP-/PTP-Transport entfernen | ✅ | 0.5 |
| 1.7 | `Trace` → `ILogger` | ✅ | 0.5 |
| 1.8 | Timeout-/Reconnect-Härtung + Spin-Loop-Fix (A8) | ✅ | 1.0 |

## Phase 2 – Domain & Gateway-Kern (7 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 2.1 | `ComfoBox.Domain`: Item-Katalog aus `src_old/ComfoBoxLib/Items/**` | ✅ | 1.5 |
| 2.2 | Werttypen (`AnalogValue`, `EnumValue`), `DateValue`/`CommandValue` entfernen | ✅ | 1.0 |
| 2.3 | `ComfoBox.Gateway`: Host, DI, Options, Validierung | ✅ | 1.0 |
| 2.4 | `BacnetDevice`: I/O, Serialisierung, Priorisierung | ✅ | 1.5 |
| 2.5 | `ValueCache` | ✅ | 0.5 |
| 2.6 | Persistenter Schreibzähler (A3) | ✅ | 0.5 |
| 2.7 | **Serilog-Setup + In-Memory-Ringbuffer** (1000, konfigurierbar) | ✅ | 1.0 |

## Phase 3 – MQTT-Migration & HA-Discovery (6 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 3.1 | MQTTnet-Client (`MqttConnectionFactory`, `MqttConnectionManager`, `MqttPublisher`) | ✅ | 1.5 |
| 3.2 | Subscriber für `/Set`-Topics | ✅ | 0.5 |
| 3.3 | `MqttPayload` (InvariantCulture) + Enum-Parser ohne 0-Fallback (A5, A6) | ✅ | 0.5 |
| 3.4 | LWT, `Connected`, `Version` | ✅ | 0.5 |
| 3.5 | Home-Assistant-Discovery (Device + Komponenten) | ✅ | 1.5 |
| 3.6 | Health-/Status-Publisher | ✅ | 1.0 |

## Phase 4 – REST/SSE-API (4.5 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 4.1 | Items-Endpunkte (CRUD + Validierung) | ✅ | 1.0 |
| 4.2 | `/api/health`, `/api/status`, `/api/diagnostics` | ✅ | 0.5 |
| 4.3 | SSE-Stream | ✅ | 1.0 |
| 4.4 | `/api/config` (GET/PUT) + Persistenz | ✅ | 0.5 |
| 4.5 | `/api/service/restart` + `ServiceControl` | ✅ | 0.5 |
| 4.6 | OpenAPI/Swagger | ✅ | 0.5 |
| 4.7 | **Setup-API** (`/api/setup/*`, PBKDF2, `config.json`) | ✅ | 1.5 |
| 4.8 | **Token-API** (`/api/security/tokens`) | ✅ | 1.0 |
| 4.9 | **Log-Endpunkte** (`/api/diagnostics/logs`, SSE-Tail) | ✅ | 0.5 |

## Phase 5 – Angular/Optimus UI (9.5 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 5.1 | Angular-22-Projekt, `ng add @openng/optimus-ui`, Preset Aura | ✅ | 0.5 |
| 5.2 | API-Client, Signal-Stores, SSE-Client | ✅ | 1.0 |
| 5.3 | Dashboard (Health, Verbindungen, Kacheln) | ✅ | 1.5 |
| 5.4 | Item-Browser + Schreiben (Analog/Enum) | ✅ | 2.0 |
| 5.5 | Konfigurationsseite | ✅ | 1.0 |
| 5.6 | Diagnoseseite | ✅ | 0.5 |
| 5.7 | Responsive, A11y, i18n (de) | ✅ | 0.5 |
| 5.8 | SPA-Auslieferung aus dem .NET-Host + Docker-Stage | ✅ | 0.5 |
| 5.9 | **Getting-Started-Assistent** (6 Schritte) | ✅ | 2.5 |
| 5.10 | **Login** + Cookie-Auth | ✅ | 1.0 |
| 5.11 | **Hilfe-Bereich** (`/help/:slug`) + Suche | ✅ | 1.5 |
| 5.12 | **Log-Viewer** (Live-Tail, Filter, Export) | ✅ | 1.5 |
| 5.13 | **Serielle Ports zur Laufzeit wählen** (ohne Neustart) | ✅ | 1.0 |

## Phase 6 – Container & Synology (4 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 6.1 | Dockerfile **ein** Image (Angular + .NET + Runtime, tini, Healthcheck) | ✅ | 1.0 |
| 6.2 | `compose.yaml` (mit `devices:`) | ✅ | 0.5 |
| 6.3 | CI: Image-Build + Push + Trivy (**Multi-Arch**) | ✅ | 0.5 |
| 6.4 | Test auf der DS923+ | ⛔ | 1.0 |
| 6.5 | Backup/Restore, Monitoring, Alarmierung | ✅ | 0.5 |
| 6.6 | udev-Regel + stabiler `by-id`-Pfad | ✅ | 0.5 |

## Phase 8 – Synology-Paket & Getting Started (13 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 8.1 | SPK-Skelett (`INFO`, `package.tgz`, `scripts/`, **Icon**) | ✅ | 1.0 |
| 8.2 | Lifecycle-Skripte (Exit-Codes 0/1/2/3/4/150) | ✅ | 2.0 |
| 8.3 | `common.sh` | ✅ | 1.0 |
| 8.4 | `serial-scan.sh` (→ `ports.json`) | ✅ | 1.0 |
| 8.5 | `supervisor.sh` (Compose rendern, Watch-Loop) | ✅ | 2.5 |
| 8.6 | udev-Regel + systemd-Unit + `by-id`-Symlink | ✅ | 1.0 |
| 8.7 | Setup-API | ✅ | 2.0 |
| 8.8 | Getting-Started-Assistent | ✅ | 2.5 |
| 8.9 | SPK-Build in CI + Release-Artefakt | ✅ | 0.5 |

## Phase 8a–8d – Home-Assistant-Ökosystem (14 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 8a.1 | HACS-Integration: `manifest.json`, `hacs.json`, Config Flow | ✅ | 1.5 |
| 8a.2 | API-Client (`aiohttp`, Bearer-Token, Fehlerklassifizierung) | ✅ | 1.0 |
| 8a.3 | Coordinator (10-s-Intervall, `unavailable` statt Flackern) | ✅ | 1.0 |
| 8a.4 | Plattformen `sensor`/`number`/`select`/`switch`/`binary_sensor` | ✅ | 2.5 |
| 8a.5 | Services + Übersetzungen | ✅ | 0.5 |
| 8a.6 | Tests (Config Flow ✅ · Client, Coordinator, Mapping offen) | 🟡 | 1.5 |
| 8b.1 | HA-Add-on-Manifest | ✅ | 1.0 |
| 8b.2 | Options-Mapping (`/data/options.json` → Gateway-Options) | ⬜ | 1.0 |
| 8b.3 | Add-on-Doku + Übersetzungen (de/en) | ✅ | 1.0 |
| 8c.1 | Multi-Arch-Publish (`amd64` + `arm64`) + HACS-Release | ⬜ | 2.0 |
| 8d.1 | Token-API im Gateway | ⬜ | 1.0 |

> **Hinweis zu 8d.1:** Die Endpunkte `/api/security/tokens` **existieren bereits**
> (`src/ComfoBox.Gateway/Api/TokenEndpoints.cs`, `ApiSecurityStore.CreateToken`) –
> offen ist die **Anbindung an die Oberfläche** (Token erzeugen, Name, widerrufen)
> und die Verifikation der HA-Integration damit.

## Phase 7 – Parallelbetrieb & Umschaltung (3 PT)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| 7.1 | Neues Gateway mit `BaseTopic=ComfoBox2` parallel starten | ⬜ | 0.5 |
| 7.2 | Payload-Vergleichstool (`old` vs. `new`) | ⬜ | 1.0 |
| 7.3 | HA-Discovery mit `ComfoBox2` testen | ⬜ | 0.5 |
| 7.4 | Umschaltung auf `ComfoBox`, HA-Entitäten aufräumen | ⬜ | 0.5 |
| 7.5 | Alten Dienst stilllegen, Doku aktualisieren | ⬜ | 0.5 |

## Dokumentation (parallel, nicht in der PT-Summe)

| # | Aufgabe | Status | PT |
|---|---|---|---|
| D.1 | Ist-Zustand analysieren | ✅ | – |
| D.2 | Spezifikation (5 Bereiche, 10 ADRs) | ✅ | – |
| D.3 | SPK-Installations-Spec | ✅ | – |
| D.4 | Getting-Started-Spec (UI + API) | ✅ | – |
| D.5 | HA-Ökosystem (Add-on + HACS-Integration) | ✅ | – |
| D.6 | **Benutzerdokumentation** (11 Seiten + Build-Skript) | ✅ | 7 |
| D.7 | Aufgaben-Dokument (dieses Verzeichnis) | ✅ | – |
| D.8 | Link-Validator (`tools/check-links.ps1`) | ✅ | – |
| D.9 | **Logging-Spec + ADR-011** (Serilog, Ringbuffer, Log-Viewer) | ✅ | 2 |

## Summe

| Bereich | PT |
|---|---|
| Implementierung (Phase 0–8d) | 64 |
| Dokumentation (D.6 + D.9) | 9 |
| **Gesamt** | **73** |

> **Fertig:** 2 PT (Phase 0) + 8 PT (Phase 1, Aufgaben 1.1–1.8) + 7 PT (Phase 2, Aufgaben 2.1–2.7) + 6 PT (Phase 3, Aufgaben 3.1–3.6) + 4.5 PT (Phase 4, Aufgaben 4.1–4.9) + 10.5 PT (Phase 5, Aufgaben 5.1–5.13) + 3 PT (Phase 6, Aufgaben 6.1–6.3 + 6.5 + 6.6) + 13 PT (Phase 8, Aufgaben 8.1–8.9) von 64 PT.
>
> **Stand 2026-11-01:** **301 Tests grün** (46 BACnet + 30 Domain + 10 Contract + 215 Gateway).
> Das Container-Image (`comfobox-gateway`, 261 MB) ist gebaut – **amd64 und arm64**,
> der Container läuft **healthy**, Trivy meldet 0 Funde, und Backup/Restore sind
> real getestet. Das **Synology-Paket** ist vollständig (Icon, Setup-API,
> Assistent, CI-Build) – offen bleibt nur der Test auf der echten DS923+
> (Aufgabe **6.4**).

## Nächste Aufgabe

> **Phase 8a–8d – Home-Assistant-Ökosystem** (14 PT, davon 8.5 PT offen)
> Ort: [phase-8ad.md](phase-8ad.md)
>
> | # | Aufgabe | Status | PT |
> |---|---|---|---|
> | 8a.6 | HA-Integrationstests (API-Client, Coordinator, Entity-Mapping) | 🟡 | 1.5 |
> | 8b.2 | Options-Mapping (`/data/options.json` → Gateway-Options) | ⬜ | 1.0 |
> | 8c.1 | Multi-Arch-Publish + HACS-Release | ⬜ | 2.0 |
> | 8d.1 | Token-API in der Oberfläche | ⬜ | 1.0 |
>
> **Wichtig:** Der Multi-Arch-Publish (8c.1) hängt an der CI – der
> Image-Tag muss zum Release-Tag passen, sonst findet der HA-Supervisor das
> Image nicht.
> → Details: [phase-8ad.md](phase-8ad.md)
