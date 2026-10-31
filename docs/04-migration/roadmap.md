# Roadmap

## 0. Überblick

```
Phase 0 ──▶ Phase 1 ──▶ Phase 2 ──▶ Phase 3 ──▶ Phase 4 ──▶ Phase 5 ──▶ Phase 6 ──▶ Phase 8 ──▶ Phase 8a–d ──▶ Phase 7
 Repo-       BACnet-     Domain &    MQTT &      REST/      Angular     Docker     Synology-   HA-Add-on    Parallel-
 Hygiene     Portierung  Gateway    HA-Disc.    SSE-API    UI          & NAS      SPK &       + HACS-      betrieb &
 + CI        + Tests     Kern                                               (1 Image)  Assistent   Integration  Umschaltung
 2 PT        8 PT        6 PT       6 PT        4 PT       8 PT        4 PT       13 PT       14 PT         3 PT
```

**Abhängigkeiten:**

| Phase | Braucht |
|---|---|
| 1 | 0 |
| 2 | 1 (Item-Katalog wird parallel zum BACnet-Test aufgebaut) |
| 3 | 2 |
| 4 | 2, 3 |
| 5 | 4 |
| 6 | 3, 4, 5 |
| 8 | 5, 6 |
| 8a–d | 4, 6, 8 |
| 7 | 6 |

---

## Phase 0 – Repo-Hygiene & CI (2 PT)

**Ziel:** Eine belastbare Basis, bevor Code bewegt wird.

| # | Aufgabe | Ergebnis |
|---|---|---|
| 0.1 | `global.json` (SDK-Pinning 10.0.400) | deterministischer Build |
| 0.2 | `Directory.Packages.props` (Central Package Management) | eine Versionsquelle |
| 0.3 | `Directory.Build.props` (Nullable, Warnungen als Fehler) | Qualitäts-Gate |
| 0.4 | `src/ComfoBox.sln` neu anlegen (SDK-Projekte) | `dotnet build` möglich |
| 0.5 | `.gitignore` erweitern (`.env`, `appsettings.override.json`, `*.user`) | keine Secrets im Repo |
| 0.6 | CI: `build-test.yml` + `codeql.yml` | Build & Test bei jedem PR |
| 0.7 | `docs/topics.json` + `MoMA Scan Results.html` als **Fixtures fixieren** | Test-Grundlage |
| 0.8 | `CHANGELOG.md` anlegen | Release-Notizen |

**Abnahme:** `dotnet build` und `dotnet test` laufen in CI grün (leere Testprojekte sind ok).

---

## Phase 1 – BACnet-Portierung & Tests (8 PT)

**Ziel:** Der Protokollstack läuft verhaltensgleich auf .NET 10.

| # | Aufgabe | PT | Ergebnis |
|---|---|---|---|
| 1.1 | Golden-Frames aus `MoMA Scan Results.html` extrahieren | 0.5 | `bacnet-frames.json` |
| 1.2 | Golden-Tests gegen den **alten** Code schreiben | 1.0 | Tests grün auf net452 |
| 1.3 | `ComfoBox.Bacnet` als SDK-Projekt anlegen, `BACnetBase` portieren | 1.0 | Codec grün |
| 1.4 | `BACnetClient` portieren | 1.5 | grün |
| 1.5 | `BACnetTransport` (MSTP + Serial) portieren | 2.0 | grün |
| 1.6 | Pipe-/UDP-/PTP-Transport entfernen | 0.5 | kein Windows-Code mehr |
| 1.7 | `Trace` → `ILogger` (26 Stellen) | 0.5 | diagnostizierbar |
| 1.8 | Timeout-/Reconnect-Härtung + Spin-Loop-Fix (A8) | 1.0 | kein 100 % CPU |

**Abnahme:**
- Golden-Tests grün **ohne** Fixture-Änderung.
- Port öffnen/geschlossen/Reconnect-Test grün.
- `dotnet test` grün.

**Risiko:** Hoch. Deshalb die Golden-Tests **zuerst**.

---

## Phase 2 – Domain & Gateway-Kern (6 PT)

| # | Aufgabe | PT |
|---|---|---|
| 2.1 | `ComfoBox.Domain`: Item-Katalog aus `Items/**` + `topics.json` generieren | 1.5 |
| 2.2 | Werttypen (`AnalogValue`, `EnumValue`) neu, `DateValue`/`CommandValue` entfernen | 1.0 |
| 2.3 | `ComfoBox.Gateway`: Host, DI, Options, Validierung | 1.0 |
| 2.4 | `BacnetDevice`: I/O, `SemaphoreSlim`-Serialisierung, Priorisierung (`Once`/`Primary`/`Secondary`) | 1.5 |
| 2.5 | `ValueCache` | 0.5 |
| 2.6 | Persistenter Schreibzähler (A3) | 0.5 |

**Abnahme:** Der Gateway liest alle 127 Items und schreibt in den Cache – **noch ohne MQTT**.

---

## Phase 3 – MQTT-Migration & HA-Discovery (6 PT)

| # | Aufgabe | PT |
|---|---|---|
| 3.1 | MQTTnet-Client (`MqttConnectionFactory`, `MqttConnectionManager`, `MqttPublisher`) | 1.5 |
| 3.2 | Subscriber für `/Set`-Topics | 0.5 |
| 3.3 | `MqttPayload` (InvariantCulture) + Enum-Parser ohne 0-Fallback (A5, A6) | 0.5 |
| 3.4 | LWT, `Connected`, `Version` (Kompatibilität) | 0.5 |
| 3.5 | Home-Assistant-Discovery (Device + Komponenten) | 1.5 |
| 3.6 | Health-/Status-Publisher | 1.0 |

**Abnahme:**
- **Contract-Test grün**: alle Topics aus `docs/topics.json` werden publiziert bzw. abonniert.
- Discovery-Payloads erscheinen in HA.

---

## Phase 4 – REST/SSE-API (4 PT)

| # | Aufgabe | PT |
|---|---|---|
| 4.1 | Items-Endpunkte (CRUD + Validierung) | 1.0 |
| 4.2 | `/api/health`, `/api/status`, `/api/diagnostics` | 0.5 |
| 4.3 | SSE-Stream | 1.0 |
| 4.4 | `/api/config` (GET/PUT) + Persistenz | 0.5 |
| 4.5 | `/api/service/restart` + `ServiceControl` | 0.5 |
| 4.6 | OpenAPI/Swagger | 0.5 |

---

## Phase 5 – Angular/Optimus UI (8 PT)

| # | Aufgabe | PT |
|---|---|---|
| 5.1 | Angular 22 Projekt, `ng add @openng/optimus-ui`, Preset Aura | 0.5 |
| 5.2 | API-Client, Signal-Stores, SSE-Client | 1.0 |
| 5.3 | Dashboard (Health, Verbindungen, Kacheln) | 1.5 |
| 5.4 | Item-Browser + Schreiben (Analog/Enum) | 2.0 |
| 5.5 | Konfigurationsseite | 1.0 |
| 5.6 | Diagnoseseite | 0.5 |
| 5.7 | Responsive, A11y, i18n (de) | 0.5 |
| 5.8 | **SPA-Auslieferung aus dem .NET-Host** (`wwwroot` + `MapFallbackToFile`) + Docker-Stage | 0.5 |

> **Kein nginx-Container** – Gateway und UI teilen sich ein Image
> (→ [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md)).

---

## Phase 6 – Container & Synology (4 PT)

| # | Aufgabe | PT |
|---|---|---|
| 6.1 | Dockerfile **ein** Image (Angular-Stage + .NET-Stage + Runtime, tini, Healthcheck) | 1.0 |
| 6.2 | `compose.yaml` (mit `devices:`, **einer** Service) | 0.5 |
| 6.3 | CI: Image-Build + Push + Trivy | 0.5 |
| 6.4 | **Test auf der DS923+** (Serial-Durchreichung, Broker, HA) | 1.0 |
| 6.5 | Backup/Restore, Monitoring, Alarmierung | 0.5 |
| 6.6 | udev-Regel + stabiler `by-id`-Pfad für den Adapter | 0.5 |

---

## Phase 7 – Parallelbetrieb & Umschaltung (3 PT)

| # | Aufgabe | PT |
|---|---|---|
| 7.1 | Neues Gateway mit `BaseTopic=ComfoBox2` parallel starten | 0.5 |
| 7.2 | Payload-Vergleichstool (`old` vs. `new`) | 1.0 |
| 7.3 | HA-Discovery mit `ComfoBox2` testen | 0.5 |
| 7.4 | Umschaltung auf `ComfoBox`, alte Entitäten in HA aufräumen | 0.5 |
| 7.5 | Alten Dienst stilllegen, Doku aktualisieren | 0.5 |

---

## Phase 8 – Synology-Paket & Getting Started (13 PT)

> Ziel: *„Als Third Party App in der DS923+ installieren und bequem einrichten."*
> → [ADR-008](../05-entscheidungen/adr-008-synology-spk.md),
> [Synology-Paketcenter](../03-deployment/synology-paketcenter.md)
| # | Aufgabe | PT | Ergebnis |
|---|---|---|---|
| 8.1 | SPK-Skelett (`INFO`, `package.tgz`, `scripts/`, Icon) | 1.0 | `ComfoBoxGateway_noarch-dsm7_<ver>.spk` |
| 8.2 | Lifecycle-Skripte (`preinst`, `postinst`, `preuninst`, `postuninst`, `start-stop-status`) | 2.0 | korrekte Exit-Codes, Paketcenter-Status |
| 8.3 | `common.sh` (Pfade, Logging, `compose_cmd`-Discovery) | 1.0 | gemeinsame Basis |
| 8.4 | `serial-scan.sh` (Port-Enumeration → `ports.json`) | 1.0 | Portliste für den Assistenten |
| 8.5 | `supervisor.sh` (Compose rendern, Watch-Loop, `up`/`down`) | 2.5 | `devices:` folgt der Portwahl |
| 8.6 | udev-Regel + systemd-Unit + `by-id`-Symlink | 1.0 | stabile Pfade |
| 8.7 | Setup-API (`/api/setup/*`, PBKDF2, `config.json`) | 2.0 | Konfiguration ohne Neustart |
| 8.8 | **Getting-Started-Assistent** (6 Schritte in der UI) | 2.5 | Portwahl, Tests, Benutzer, Passwort |
| 8.9 | SPK-Build in CI + Release-Artefakt | 0.5 | `.spk` im GitHub Release |

**Abnahme:**
- `*.spk` installiert auf einer DS923+ **ohne** Parameter → Browser öffnet sich → Assistent startet.
- Der Assistent findet den Adapter, testet BACnet und MQTT, legt den Benutzer an.
- Nach „Übernehmen" läuft der Gateway mit dem gewählten Serial-Port.
- `postuninst` lässt das `var`-Verzeichnis (Schreibzähler) stehen.

---

## Phase 8a–8d – Home-Assistant-Ökosystem (14 PT)

> Ziel: *„Alles in HA installierbar, Plugin via HACS, Zugriff ohne MQTT-Konfiguration."*
> → [ADR-009](../05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md),
> [HA-Integration](../02-modernisierung/ha-integration.md),
> [Installationsstufen](../03-deployment/installationsstufen.md)

| # | Aufgabe | PT | Ergebnis |
|---|---|---|---|
| 8a.1 | HACS-Integration: `manifest.json`, `hacs.json`, Config Flow | 1.5 | `custom_components/comfobox` |
| 8a.2 | API-Client (`aiohttp`, Bearer-Token, Fehlerklassifizierung) | 1.0 | `api.py` |
| 8a.3 | Coordinator (10-s-Intervall, `unavailable` statt Flackern) | 1.0 | `coordinator.py` |
| 8a.4 | Plattformen `sensor`/`number`/`select`/`switch`/`binary_sensor` | 2.5 | 131 Entitäten |
| 8a.5 | Services (`set_value`, `restart`, `read_all`) + Übersetzungen | 0.5 | `services.py`, `strings.json` |
| 8a.6 | Tests (Config Flow, Client, Coordinator, Mapping) | 1.5 | `pytest` grün |
| 8b.1 | HA-Add-on-Manifest (`config.yaml`, `device(subsystem=tty)`, `ingress`, `watchdog`) | 1.0 | `deploy/ha-addon/` |
| 8b.2 | Options-Mapping (`/data/options.json` → Gateway-Options) | 1.0 | `AddOnOptionsProvider.cs` |
| 8b.3 | Add-on-Doku + Übersetzungen (de/en) | 1.0 | `DOCS.md`, `translations/` |
| 8c.1 | Multi-Arch-Publish (`amd64` + `arm64`) + HACS-Release | 2.0 | Release-Artefakte |
| 8d.1 | Token-API im Gateway (`/api/security/tokens`, PBKDF2-Hash) | 1.0 | Token-Erzeugung in der UI |

**Abnahme:**
- `hacs.json` vorhanden, HACS findet die Integration.
- Ein Gerät „ComfoBox ELESTA" mit allen 127 Items + 4 Sonder-Entitäten.
- **Kein** MQTT-Broker nötig für die Entitäten.
- Add-on installierbar, serieller Port per Dropdown wählbar, UI als Panel in HA.
- Image für `linux/amd64` **und** `linux/arm64` publiziert.

---

## Meilensteine

| Meilenstein | Phase | Kriterium |
|---|---|---|
| **M1** | Ende Phase 1 | BACnet verhaltensgleich auf .NET 10 |
| **M2** | Ende Phase 3 | Gateway publiziert alle 127 Topics + Discovery |
| **M3** | Ende Phase 5 | UI bedient die Anlage |
| **M4** | Ende Phase 6 | Deployment auf der DS923+ läuft |
| **M5** | Ende Phase 8 | Installation per Klick + Assistent läuft auf der DS923+ |
| **M6** | Ende Phase 7 | Alte Installation abgeschaltet |

## Reihenfolge-Constraints (nicht verhandelbar)

1. **Golden-Tests vor der Portierung.** Ohne Test-Fixture kein M1.
2. **Contract-Test vor Phase 3 Ende.** Ohne grünen Contract-Test keine HA-Discovery.
3. **Kein Umschalten ohne Parallelbetrieb.** Der alte Dienst bleibt bis Phase 7 aktiv.
4. **Persistenter Schreibzähler vor dem ersten produktiven Schreibvorgang** (Phase 2).
