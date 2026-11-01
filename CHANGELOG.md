# Changelog

Alle wesentlichen Änderungen an diesem Projekt.

Das Format orientiert sich an [Keep a Changelog](https://keepachangelog.com/de/1.1.0/),
die Versionierung an [Semantic Versioning](https://semver.org/lang/de/).

## [Unreleased]

### Added
- Phase 0: `src/global.json` (SDK-Pinning auf 10.0.400)
- Phase 0: `src/Directory.Packages.props` (Central Package Management)
- Phase 0: `src/Directory.Build.props` (Nullable, `TreatWarningsAsErrors`, C# 14, Lock-Dateien)
- Phase 0: `src/ComfoBox.sln` mit sieben SDK-Projekten
  (`ComfoBox.Domain`, `ComfoBox.Bacnet`, `ComfoBox.Gateway` + vier Testprojekte)
- Phase 0: CI-Workflow `.github/workflows/build-test.yml` (Build, Test, Coverage, NuGet-Vulnerability-Scan)
- Phase 0: Generator `tools/generate-topics/generate-topics.mjs` – reproduziert
  `docs/topics.json` byteidentisch (127 Items) und prüft sie in CI
- Phase 1: `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` – 1:1-Port des Legacy-Codecs
  (Fremdcode, MIT; Herkunft und Änderungen in `Bacnet/HEADER-LIZENZ.txt`)
- Phase 1: Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  mit 25 Frames, erzeugt vom Legacy-Codec über `tools/port-bacnet/oracle/`
- Phase 1: `ComfoBox.Bacnet.Diagnostics.BacnetLog` – `ILogger`-Fassade für den
  statischen Fremdcode (ersetzt `System.Diagnostics.Trace`, Befund A16)
- Phase 1: `tools/port-bacnet/port-bacnet-base.mjs` – mechanischer, idempotenter Port
- Phase 1: `ComfoBox.Bacnet.BacnetClient` – **async** BACnet-Client
  (`ReadPropertyAsync`, `WritePropertyAsync`, `WhoIsAsync`, `IamReceived`-Ereignis)
- Phase 1: typisierte BACnet-Fehler (`BacnetTimeoutException`, `BacnetDeviceErrorException`,
  `BacnetDeviceAbortException`, `BacnetDecodeException`) statt `Exception("…")`
- Phase 1: `ComfoBox.Bacnet.BacnetMstpTransport` – MS/TP-Transport mit Port-Verwaltung,
  Reconnect und exponentiellem Backoff (5 s → 30 s)
- Phase 1: `ComfoBox.Bacnet.Serial` – schmale Port-Schnittstelle (`ISerialPort`,
  `ISerialPortFactory`, `SerialPortAdapter`) als Testnaht
- Phase 1: `MstpStateMachine` – 1:1-Port des Legacy-MS/TP-Automaten
- Phase 1: 19 neue Tests (MS/TP-Framing, Serial-Verhalten inkl. A8-Regression,
  Client-Request/Antwort/Fehler) → 45 Tests grün
- Phase 2: `ComfoBox.Domain` – der **Item-Katalog mit 127 Items**
  (`ItemDefinition`, `ItemValueKind`, `RefreshPriority`, `ItemCatalog`),
  deklarativ und ohne Reflection
- Phase 2: `ComfoBox.Domain.Items.Catalog` + `ItemEnums` werden aus dem
  Legacy-Quelltext **generiert** (`tools/generate-topics/generate-topics.mjs
  --catalog`) – inklusive BACnet-ID, Einheit, Grenzen, Schreibbarkeit,
  Expert-Modus, **Priorität** und der exakten Enum-Namen
- Phase 2: `ComfoBox.Domain.Values` – `ItemValue`, `AnalogValue`, `EnumValue`
  (kultur-invariant, `InvariantCulture`)
- Phase 2: `ComfoBox.Gateway` – Host mit DI, Options-Bindung und
  **Fail-Fast-Validierung** (`ValidateOnStart`)
- Phase 2: `ComfoBox.Gateway.Bacnet.BacnetDevice` – **ein** `SemaphoreSlim` für
  alle Buszugriffe, Priorisierung (`Once`/`Primary`/`Secondary`), Timeout je
  Item, Fehlerbehandlung ohne Abbruch des Zyklus
- Phase 2: `ComfoBox.Gateway.Bacnet.PollPlan` – welcher Zyklus welche Items liest
- Phase 2: `ComfoBox.Gateway.ValueCache` – letzter Wert + Zeitstempel,
  `IsStale` (3 Poll-Zyklen), SSE-Abonnenten
- Phase 2: `ComfoBox.Gateway.Protection.WriteCounter` – **persistenter**
  24-h-Schreibzähler (JSON-Datei, atomar geschrieben)
- Phase 2: `ComfoBox.Gateway.Health.ErrorCollector` – Fehler-Senke (letzte 50)
- Phase 2: `ComfoBox.Gateway.Logging.InMemoryLogSink` – Serilog-Sink mit
  In-Memory-Ringpuffer (1000 Einträge) für den Log-Viewer der Web-UI
- Phase 2: `ComfoBox.Gateway.Logging.SecretRedactor` – Passwörter und Tokens
  werden **vor** dem Speichern maskiert
- Phase 2: `ComfoBox.Bacnet.IBacnetClient` – Testnaht, damit der Gateway ohne
  Hardware getestet werden kann
- Phase 2: 98 neue Tests (30 Domain, 10 Contract, 58 Gateway) → **143 Tests grün**
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttConnectionFactory` – MQTTnet-5-Optionen
  (TLS, Auth, Keepalive 30 s, Protokoll v5, **zwei** Last Wills)
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttConnectionManager` + `BrokerConnection` –
  Connect/Reconnect mit exponentiellem Backoff, orderly Shutdown je Broker
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttClientRegistry` – Verzeichnis der
  verbundenen Clients (bricht den zirkulären DI-Pfad)
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttPublisher` – Publish mit wählbarem
  QoS/Retain; ein defekter Broker blockiert die anderen nicht
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttSubscriber` – `/Set`- und
  `/AsNumber/Set`-Topics; das Schreiben läuft **ausschließlich** über
  `BacnetDevice` (der 24-h-Zähler kann nicht umgangen werden)
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttPayload` – kultur-invariante Zahlen und
  ein Enum-Parser **ohne** 0-Fallback
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttTopicMap` – die einzige Stelle, an der
  Topic-Adressen gebildet werden
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttStatePublisher` – die Item-Werte auf
  ihren Topics (aus dem Wert-Cache, inkl. `AsNumber` bei Enums)
- Phase 3: `ComfoBox.Gateway.Mqtt.HomeAssistantDiscovery` – Discovery für alle
  127 Items, das Gerät und die Status-Entitäten (retained, QoS 1)
- Phase 3: `ComfoBox.Gateway.Mqtt.HomeAssistantBirthHandler` – reagiert auf die
  „Geburt" von Home Assistant (`homeassistant/status` = `online`) und wiederholt
  Discovery und Zustände
- Phase 3: `ComfoBox.Gateway.Mqtt.MqttStatusPublisher` – `Status/Health`,
  `Status/Uptime`, `Status/Writes`, `Status/Bacnet`, `Status/Mqtt`,
  `Status/LastError`, `Status/Errors`
- Phase 3: `ComfoBox.Gateway.Health.HealthReporter` – Health-Aggregation über
  die Komponenten (Mqtt, Bacnet) mit `online`/`degraded`/`offline`
- Phase 3: `ComfoBox.Gateway.GatewayServiceCollectionExtensions` – die gesamte
  DI-Registrierung (aus `Program.cs` herausgezogen, damit sie testbar ist)
- Phase 3: 76 neue Tests (Payload, Topic-Vertrag, Subscriber, Discovery,
  Health, MQTT-Integration gegen einen eingebetteten Broker, End-to-End) →
  **219 Tests grün**
- Phase 4: `ComfoBox.Gateway.Api.GatewayApiExtensions` – `AddGatewayApi()` (DI)
  und `MapGatewayApi()` (Routen) – die REST-Schnittstelle liegt **nicht** in
  `Program.cs`, damit die Tests sie isoliert aufbauen können
- Phase 4: Item-Endpunkte – `GET /api/items`, `GET /api/items/{id}`,
  `GET /api/items/group/{group}`, `PUT /api/items/{id}` mit den Statuscodes
  204/400/403/409/422/502; **jeder** Schreibvorgang läuft über
  `BacnetDevice.WriteAsync` (der 24-h-Zähler ist nicht umgehbar)
- Phase 4: `GET /api/health`, `GET /api/status`, `GET /api/diagnostics/errors`,
  `GET /api/diagnostics/writes` – die Feldnamen von `/api/status` sind
  **Vertrag** (die Home-Assistant-Integration liest sie)
- Phase 4: SSE-Stream `GET /api/events` mit den Ereignissen `snapshot` und
  `value`
- Phase 4: `GET`/`PUT /api/config` – `GET` liefert die Konfiguration **ohne**
  Passwörter (nur `passwordSet`), `PUT` schreibt nach `config.json`
  (Modus 0600)
- Phase 4: `POST /api/service/restart` → 202; der Neustart läuft **in-process**
  (`IHostApplicationLifetime.StopApplication()`) – kein Docker-Socket nötig
- Phase 4: OpenAPI/Swagger unter `/swagger` (nur im Development-Modus) –
  Swashbuckle **10.3.1**
- Phase 4: Setup-API `/api/setup/*` – Zustand, serielle Ports (aus
  `ports.json`), freier Web-Port, BACnet-/MQTT-Test mit maschinenlesbarem
  `hint`, Konfiguration schreiben (Passwort **serverseitig** gehasht),
  `apply` → Neustart
- Phase 4: Token-API `/api/security/tokens` – der Klartext ist **einmal**
  sichtbar, gespeichert wird nur der PBKDF2-Hash (`pbkdf2-sha256$600000$…`)
- Phase 4: Log-Endpunkte `/api/diagnostics/logs` (Filter level/source/search/
  since/limit/order, Obergrenze 1000), `/stream` (SSE-Tail mit Initial-
  Snapshot, max. 5 gleichzeitige Streams) und `DELETE`
- Phase 4: `ComfoBox.Gateway.Security.ApiAccessGuard` – die Zugriffskontrolle
  (Bearer-Token, Basic-Auth des Administrators, LAN-Modus) – siehe
  [ADR-012](docs/05-entscheidungen/adr-012-api-zugriffskontrolle.md)
- Phase 4: `ComfoBox.Gateway.Storage.AtomicFile` – atomisches Schreiben mit
  Retry (Windows sperrt die Datei unter Umständen für einen Moment)
- Phase 4: `Gateway:DeviceId` (Standard `comfobox1`) und
  `Gateway:RestartEnabled` als neue Einstellungen; `Api:RequireAuthentication`,
  `Api:SuggestedUiPort`, `Api:BacnetTestTimeoutSeconds`,
  `Api:MqttTestTimeoutSeconds` als neuer `Api`-Abschnitt
- Phase 4: 59 neue Tests (Items, Status, SSE, Config, Neustart, Token, Setup,
  Log) gegen einen **echten** Kestrel-Testhost → **278 Tests grün**
- Phase 5 (Aufgabe 5.13): **Serielle Ports zur Laufzeit wählen** – die Ports
  werden **gescannt** (Hotplug: `ports.json` des Supervisors, sonst
  `SerialPort.GetPortNames()`), der Benutzer wählt sie in der Oberfläche aus,
  und die Umstellung ist **sofort** wirksam – **kein Neustart** nötig.
  Neuer Endpunkt-Satz: `GET /api/config/serial-ports`,
  `POST /api/config/bacnet/test`, `PUT /api/config/bacnet`
- Phase 5 (Aufgabe 5.13): `ComfoBox.Gateway.Bacnet.SerialPortSettings` – die
  **laufenden** Parameter der Schnittstelle; die Port-Fabrik liest sie bei
  jedem Öffnen, deshalb genügt ein Neuaufbau der Verbindung
- Phase 5 (Aufgabe 5.13): `ComfoBox.Gateway.Bacnet.BacnetSerialManager` –
  prüft einen Port und stellt die Schnittstelle um (Konfiguration schreiben →
  Laufzeit übernehmen → BACnet-Stack neu aufbauen)
- Phase 5 (Aufgabe 5.13): `ComfoBox.Gateway.Hardware.SerialPortScanner` – die
  Port-Suche; der Setup-Assistent benutzt jetzt dieselbe Quelle
- Phase 5 (Aufgabe 5.13): `app-serial-port-picker` in der Weboberfläche –
  eingesetzt auf der Konfigurationsseite und **prominent auf dem Dashboard**,
  sobald BACnet nicht verbunden ist
- Phase 5 (Aufgabe 5.13): 7 neue Tests (Port-Umstellung ohne Neustart,
  Status, Validierung, Sofort-Reconnect) → **293 Tests grün**
- Phase 6 (Aufgabe 6.1): `src/ComfoBox.Gateway/Dockerfile` – **ein** Image für
  Gateway **und** Weboberfläche (ADR-007), 3 Stages (node → sdk → aspnet),
  `tini` als PID 1, `USER comfobox`, ICU für die deutsche Formatierung,
  Healthcheck (nur Erreichbarkeit) und `VOLUME /var/lib/comfobox`
- Phase 6 (Aufgabe 6.1): `.dockerignore` – der Build-Kontext ist das
  Repository-Wurzelverzeichnis (das Image braucht .NET **und** Angular)
- Phase 6 (Aufgabe 6.2): `deploy/compose/compose.yaml` – der manuelle Betrieb,
  parametrisiert über `.env` (Vorlage `.env.example`), mit `devices:`,
  `group_add`, `security_opt` und **ohne** Docker-Socket
- Phase 6 (Aufgabe 6.2): `deploy/compose/compose.test.yaml` – Override
  **ohne** Serial-Port (Entwicklerrechner ohne `/dev/ttyUSB0`)
- Phase 6 (Aufgabe 6.3): CI-Workflow `docker-publish.yml` auf **ein** Image
  und **Multi-Arch** (`linux/amd64,linux/arm64`) umgestellt; Trivy-Scan vor
  dem Push
- Phase 6 (Aufgabe 6.3): CI-Job „container" in `build-test.yml` – baut das
  Image und prüft **echt**: kein Root, tini als PID 1, die Angular-UI im
  Image, Health-Endpunkt, SPA-Auslieferung und das saubere Herunterfahren
- Phase 6 (Aufgabe 6.5): `deploy/backup/backup.sh`, `restore.sh` und
  `backup-cron.sh` – die Sicherung enthält das Volume `comfobox-data` mit dem
  **24-h-Schreibzähler**; die Wiederherstellung ist getestet
- Phase 6 (Aufgabe 6.5): Benutzerdoku `docs/benutzer/08-monitoring.md` –
  Health überwachen, HA-Alarmierung für `degraded`/`offline`, Backup und
  Wiederherstellung

- Phase 8 (Aufgabe 8.1): **Paket-Icon** `deploy/synology-spk/src/PACKAGE_ICON.png`
  (256×256) – reproduzierbar erzeugt mit
  `node deploy/synology-spk/tools/make-icon.mjs` (nur Node.js, kein Python)
- Phase 8 (Aufgabe 8.7): `GET /api/setup/state` liefert zusätzlich die aktuell
  gespeicherten Werte (`current`) – damit ist der Assistent **wiederholbar**,
  ohne dass der Benutzer alles neu eingeben muss
- Phase 8 (Aufgabe 8.7): das Passwort des Administrators wird **serverseitig**
  geprüft (`PasswordPolicy`: mindestens 12 Zeichen, 3 von 4 Zeichenklassen);
  ein Verstoß ergibt **400**, nicht einen stillen Durchlass
- Phase 8 (Aufgabe 8.7): der MQTT-Test unterscheidet jetzt drei Fälle –
  `mqtt_unreachable`, `mqtt_auth_failed` (der Broker lehnt die Anmeldung ab)
  und `mqtt_publish_failed` (der Broker stellt keine Nachrichten zu); er prüft
  zusätzlich mit einem Publish/Subscribe-Rundlauf
- Phase 8 (Aufgabe 8.7): die Konfiguration enthält einen neuen Abschnitt
  `Supervisor` mit `PassAllSerialDevices` – der Supervisor des NAS rendert
  daraus den `devices`-Block der Compose-Datei
- Phase 8 (Aufgabe 8.8): der Assistent lädt die gespeicherten Werte vor
  (Port, Baudrate, Broker, Benutzername), aktualisiert die Portliste alle 5 s
  (Hotplug), lässt den **Broker optional** und beobachtet den Neustart
  (`GET /api/health`), bis der Gateway wieder antwortet
- Phase 8 (Aufgabe 8.8): die Portliste zeigt den zuletzt verwendeten Port
  an, auch wenn das Betriebssystem ihn nicht mehr kennt
- Phase 8 (Aufgabe 8.9): CI-Job `spk` in `docker-publish.yml` – baut das
  Synology-Paket, erzeugt die `packages.json` (Paketquelle für das
  DSM-Paketcenter) und legt beides im GitHub Release ab
- Phase 8: `deploy/synology-spk/tools/test-config-parse.sh` – Test für den
  JSON-Zugriff des Supervisors auf die `config.json`
- Phase 8a (Aufgabe 8a.6): **35 Tests** der HA-Integration (`pytest`) –
  API-Client (Mock), Coordinator (Normalfall, Timeout, 401) und das
  Entity-Mapping (Item → Plattform, Min/Max, Enum-Namen, Unique-Ids, Gerät)
- Phase 8b (Aufgabe 8b.2): `AddOnOptionsProvider` – liest
  `/data/options.json` des HA-Supervisors und mappt die Optionen auf die
  Gateway-Konfiguration (`Bacnet:Port`, `Bacnet:Baudrate`,
  `Gateway:BaseTopic`, `Mqtt:Brokers:0:*`, `Mqtt:EnableDiscovery`,
  `Logging:Level`)
- Phase 8b (Aufgabe 8b.2): neue Optionen `Gateway:AddOnOptionsPath` (Pfad
  der `options.json`), `Mqtt:EnableDiscovery` und `Logging:Level`
- Phase 8c (Aufgabe 8c.1): Workflow `.github/workflows/ha-release.yml` –
  prüft die Versionen (Tag ↔ `manifest.json` ↔ `config.yaml` ↔ Image-Tag),
  validiert mit `hacs/action` und `hassfest` und erzeugt die Releases für
  die Integration (HACS) und das Add-on
- Phase 8d (Aufgabe 8d.1): `GET /api/config` meldet jetzt auch
  `Mqtt:EnableDiscovery`; die Oberfläche zeigt den Schalter in der
  MQTT-Karte
- **BACnet-Bus-Analyse** (nur lesend): `GET /api/diagnostics/bacnet`,
  `POST`/`DELETE /api/diagnostics/bacnet/observe`. Sie zeigt, ob auf dem
  MS/TP-Bus ein Token herumgeht – Zähler, gesehenen Stationen, den
  Ereignisverlauf und eine Bewertung in einem Satz
- `ComfoBox.Bacnet.IMstpObserver` + `MstpEventRecorder` – ein
  **allokationsfreier** Ringpuffer für die MS/TP-Ereignisse. Der
  Zustandsautomat meldet Token-Übernahme, Weitergabe, PFM-Runden und Frames
  **ohne** Log und **ohne** Sperre
- `ComfoBox.Gateway.Bacnet.BacnetBusAnalyzer` – wertet den Ringpuffer
  **außerhalb** des Bus-Takts aus (eigener Thread, 1 s-Takt) und schreibt
  dort erst das Log
- Neue Oberflächen-Seite „BACnet-Bus" in der Diagnose: Beobachtung
  starten/beenden, Zähler und Ereignistabelle
- `BacnetPollingService.SetPaused()` – während einer Beobachtung pausiert
  das Polling, damit der Token ungestört am Ring entlangläuft
- CI-Workflow `.github/workflows/synology-spk.yml` – baut das Synology-Paket
  **eigenständig** (Tag-Push oder `workflow_dispatch`), hängt es als
  Release-Asset an und kopiert es optional per SSH auf die NAS

### Changed
- **Der Legacy-Code wird von der CI nicht mehr angefasst:** der Job
  `legacy-check` ist entfernt, CodeQL ignoriert `src_old/` (`paths-ignore`).
  Grund: `src_old/` ist Archiv (net452, C# 6) – der Code lief seit dem
  24.10. nicht mehr und ist nur noch Referenz
- CI: das Synology-Paket wird nicht mehr im `docker-publish.yml` gebaut, sondern
  im eigenen Workflow `synology-spk.yml` – die Release-Notizen bleiben dadurch
  an einer Stelle
- **Kein Logging mehr im Hot-Path des MS/TP-Automaten.** Der Token hat nur
  95 ms Zeit bis zur Übernahme; ein Log-Eintrag an dieser Stelle hätte den
  Token verloren und den Bus blockiert. Die Zustände werden nun gesammelt
  und außerhalb ausgewertet
- Die Artefakte liegen **außerhalb** des 500-MB-Limits des GitHub-Free-
  Kontos: Release-Assets (2 GB pro Datei) und GHCR als Registry-Speicher
- CI: `dotnet` läuft jetzt in `src/` – dadurch greift `global.json` (SDK ist gepinnt)
- Phase 8b (Aufgabe 8b.2): die `options.json` des HA-Add-ons hat jetzt
  **Vorrang vor den Umgebungsvariablen** (und vor der `config.json`).
  Grund: das Image setzt `COMFOBOX_Bacnet__Port=/dev/ttyUSB0` als Vorgabe –
  wäre die Umgebungsvariable stärker, wäre die Portwahl des Add-ons
  wirkungslos
- Phase 8d (Aufgabe 8d.1): **ein ungültiges `Authorization`-Header wird
  immer mit `401` beantwortet** – auch auf den offenen Endpunkten. Ohne
  Header bleibt der LAN-Modus unverändert. Grund: die HA-Integration prüft
  ihren Token über `GET /api/status`; ein falscher Token darf dort nicht als
  gültig durchgehen (→ ADR-012)
- Phase 8d (Aufgabe 8d.1): das Image des HA-Add-ons trägt jetzt den
  **Versions-Tag** (`ghcr.io/rf77/comfobox-gateway:1.0.0`) – ohne ihn findet
  der Supervisor das Image nicht
- Phase 8c (Aufgabe 8c.1): das Image wird zusätzlich mit dem Versions-Tag
  **ohne** „v" publiziert – das ist der Tag, den der HA-Supervisor zieht
- Phase 8: **die `config.json` des Synology-Pakets folgt dem Schema der
  .NET-Konfiguration** (`Bacnet`, `Mqtt`, `Api`, `Gateway`, `Supervisor`).
  Die Spec zeigte vorher die Kleinbuchstaben-Abschnitte – damit hätte der
  Supervisor nichts gefunden (siehe Protokoll 2026-11-01)
- Phase 8: `postinst` schreibt nur noch `Api:SetupCompleted` in die
  Startkonfiguration. Die alte Datei enthielt einen leeren `Port` und eine
  leere Broker-Liste → `ValidateOnStart` schlug fehl → der Container startete
  nicht → der Assistent war nicht erreichbar
- Phase 8: das Image des Pakets steht in `target/image` (schreibt
  `build-spk.sh`) – das SPK kann nie ein falsches Image ziehen
- Phase 8: `build-spk.sh` bricht **ohne** Icon ab (das Paketcenter braucht
  `PACKAGE_ICON.PNG`)
- CI: der Job „Legacy-Projekte (net452) kompilieren weiterhin" wurde durch eine
  Archiv-Prüfung ersetzt – `src_old/` wird **nicht** gebaut (Projektregel §5)
- Phase 1: der BACnet-Codec enthält kein BACnet/IP (`BVLC`) und kein PTP mehr –
  der Stack läuft ausschließlich über MS/TP
- Phase 1: der neue BACnet-Code nutzt durchgängig `ILogger` statt `Trace`/`Console`
- Phase 3: **M2Mqtt ist vollständig entfernt** – der MQTT-Client ist MQTTnet 5
  (`5.2.0.1603`), async, mit TLS und Authentifizierung (ADR-002)
- Phase 3: QoS wurde von „Subscribe 2 / Publish 0" auf „Subscribe 1 /
  Publish 0|1" geändert – kompatibel, weil QoS vom Empfänger bestimmt wird
- Phase 3: die Client-ID ist jetzt `comfobox-gateway-{Hostname}` (stabil)
  statt `ComfoBox{DateTime.Ticks}` (jede Sitzung war eine neue)
- Phase 3: `Mqtt:ClientId`, `Mqtt:DiscoveryPrefix`, `Mqtt:UseMqttRetain` und
  `Mqtt:StatusIntervalSeconds` sind konfigurierbar
- Phase 5 (Aufgabe 5.13): **`Bacnet:Port` und `Bacnet:Baudrate` sind jetzt ohne
  Neustart änderbar** – als einzige Einstellung. Vorher galten sie als
  „nur Konfigurationsdatei"
- Phase 5 (Aufgabe 5.13): `GET /api/status` und `GET /api/config` melden den
  **laufenden** Port (nicht mehr den aus der Konfigurationsdatei)
- Phase 7 (Aufgabe 7.0): **Simulationsbetrieb** – `Bacnet:Simulation=true`
  ersetzt den BACnet-Client durch einen, der ohne Gerät antwortet
  (deterministische Werte aus dem Item-Katalog, optional über
  `Bacnet:SimulationValuesPath`). `/api/status` meldet `"simulated": true`
  (ADR-013)
- Phase 7 (Aufgabe 7.0): **`tools/compare-payloads/`** – der
  Payload-Vergleich aus Aufgabe 7.2: live (`--old mqtt://…`) oder gegen eine
  Aufzeichnung (`--old-file`), mit `record` zum Aufzeichnen. Zahlen mit
  Toleranz 0.01, Enums **exakt**, Exit-Code ≠ 0 bei Abweichungen
- Phase 7 (Aufgabe 7.0): `Mqtt:DiscoveryIdentifier` – die Kennung der
  Home-Assistant-Discovery ist konfigurierbar (Standard unverändert
  `comfobox`). Für den Parallelbetrieb müssen zwei Gateways verschiedene
  Kennungen haben
- Phase 7 (Aufgabe 7.0): die **wirksame MQTT-Client-ID** enthält jetzt die
  Geräte-ID (`{ClientId}-{DeviceId}`)

### Fixed
- **Die CI lief seit Phase 0 nie – der Lauf war „rot", ohne dass ein Job
  gestartet wurde.** GitHub hatte `build-test.yml` und `codeql.yml` schon bei
  der **YAML-Validierung** abgelehnt (0 Jobs, 0 Sekunden):
  - `build-test.yml`: `if: hashFiles(...)` auf **Job-Ebene** – `hashFiles` ist
    in einer Job-Bedingung nicht erlaubt („Unrecognized function")
  - `codeql.yml`: `paths-ignore` auf **Top-Level** („Unexpected value") und
    `schedule:` fälschlich unter `pull_request:` verschachtelt
  - Ein Lauf mit **0 Jobs** ist **nie** ein Code-Problem – immer zuerst die
    Annotation des Laufs lesen
- **Die CI lief nur auf `main`** – auf dem Entwicklungsbranch `Upgrade` lief
  gar nichts. `Upgrade` steht jetzt in der Branch-Liste, CodeQL zusätzlich
  mit `workflow_dispatch`
- **Der Generator war plattformabhängig:** ein verbatim-Stringliteral über
  mehrere Zeilen enthält die *physischen* Zeilenenden der Quelldatei (CRLF
  unter Windows, LF unter Linux). Der Fixture-Check schlug deshalb unter
  Linux fehl (19 Abweichungen). Die Zeilenenden werden jetzt **immer** auf
  CRLF normalisiert – die Fixture bleibt byteweise unverändert
- **Ein nicht vorhandener Serial-Port wurde unter Linux als
  „permission_denied" gemeldet.** .NET wirft unter Unix eine
  `UnauthorizedAccessException`, wenn die **Gerätedatei fehlt** – nicht nur,
  wenn sie dem Benutzer nicht gehört. Die Zuordnung prüft jetzt, ob es die
  Datei überhaupt gibt
- **Der Coverage-Collector brachte eine `netstandard`-Referenz in die
  Domäne.** coverlet schreibt die instrumentierten Assemblies mit einem
  Verweis auf die .NET-Standard-Fassade um – ein Werkzeug-Artefakt, keine
  Abhängigkeit der Domäne
- **Zwei Tests waren Wettläufe** und liefen auf dem CI-Läufer in den Timeout
  (`ValueCacheTests` nach 10 s, `BacnetClientTests.IAm_Loest_Das_Ereignis_Aus`).
  Beide warten jetzt **ereignisbasiert** statt auf eine feste Zeit
- **Der Container-Check prüfte den Docker-Health-Status zu früh:** der
  Endpunkt antwortet nach ~2 s, der Status steht erst nach dem periodischen
  Healthcheck (Intervall 30 s) auf „healthy". Der Schritt wartet jetzt aktiv
- **Der Test-Schritt meldete nur „Exit code 1".** Er nennt jetzt den
  fehlgeschlagenen Test als Annotation, in der Job-Zusammenfassung und im
  hochgeladenen Log
- **Die Reiter der Diagnose-Seite ließen sich nicht umschalten.** `p-tabs`
  hatte ein statisches `value="checks"` – der Klick auf einen anderen Reiter
  änderte nichts. Die Auswahl hängt jetzt an einem Signal (`[(value)]`)
- **Ein per `@if` ersetzter Zweig blieb als sichtbarer „Waisen"-Zweig im DOM.**
  `p-tabpanel` projiziert seinen Inhalt per `ng-template` + `*ngTemplateOutlet`;
  ein ersetzter `@if`/`@else`-Zweig wird **nicht** aus dem DOM entfernt. Der
  Ladehinweis „Die Bus-Analyse wird geladen …" blieb deshalb dauerhaft
  sichtbar, obwohl die Analyse längst fertig war. Der Hinweis wird jetzt
  **per CSS-Klasse** geschaltet (`display: none`), nicht per Kontrollfluss
- **Der CI-Lauf war rot, ohne dass der Code defekt war:** die
  `package-lock.json` des Angular-Projekts enthielt im Root-Eintrag **keine
  Abhängigkeiten** – `npm ci` bricht in diesem Fall mit `EUSAGE` ab und der
  Job „Build (Angular)" wurde rot. Die Lock-Datei ist konsistent; der
  Workflow meldet eine Drift jetzt mit einem klaren Hinweis
- Phase 7 (Aufgabe 7.0): **zwei Gateways am selben Broker haben sich gegenseitig
  verdrängt** – beide meldeten sich mit `comfobox-gateway-{Hostname}` an, der
  Broker trennte die ältere Sitzung (*Session taken over*) und deren Last-Will
  meldete das andere Gateway als `offline`. Die wirksame Client-ID enthält jetzt
  die Geräte-ID
- Phase 7 (Aufgabe 7.0): die Kennung der Home-Assistant-Discovery war eine
  Konstante (`comfobox`) → zwei Instanzen publizierten auf denselben
  Discovery-Topics und hätten sich die Entitäten gegenseitig überschrieben.
  Die Kennung ist jetzt konfigurierbar (`Mqtt:DiscoveryIdentifier`)
- Phase 8a (Aufgabe 8a.6): die Plattformen `sensor`, `number`, `select` und
  `switch` brachen mit `AttributeError: 'GatewayStatus' object has no
  attribute 'is_read_only'` ab – sie iterierten über **alle** Einträge des
  Coordinator-Caches, auch über den reservierten Status-Eintrag
  `__status__`. Sie überspringen ihn jetzt (und benutzen die Entitätsklasse
  der Plattform)
- Phase 8a (Aufgabe 8a.6): die Entitäten der Plattformen waren **keine**
  `SensorEntity`/`NumberEntity`/`SelectEntity`/`SwitchEntity` – sie hatten
  damit weder einen Zustand noch die Plattform-Eigenschaften
- Phase 8a (Aufgabe 8a.6): der Service `comfobox.set_value` ermittelte die
  Item-Id durch Abschneiden des Entity-Präfixes. Die Item-Ids des Gateways
  enthalten aber einen **Punkt** (`zone.set_point_normal`), die `entity_id`
  einen Unterstrich → jeder Schreibvorgang über den Service scheiterte mit
  `404 item_not_found`. Der Service liest jetzt die Item-Id aus dem
  Entity-Registry (über die `unique_id`)
- Phase 8d (Aufgabe 8d.1): die Icon-Buttons der Oberfläche (Token widerrufen,
  Navigation, Abmelden, neu laden, Eintrag kopieren) hatten **keinen
  zugänglichen Namen**: `aria-label` als Attribut auf `<p-button>` erreicht
  den inneren `<button>` nicht. Optimus erwartet die Eigenschaft `ariaLabel`
- Phase 8: der Assistent zeigte nach einem Klick auf die Schrittliste oder den
  Browser-Zurück den **falschen Schritt** (Fortschritt und „Weiter" liefen auseinander);
  jetzt wird der Zustand bei jeder Navigation mit der URL synchronisiert
- Phase 8: ein **direkter Aufruf** von `/setup/serial` (z. B. als Lesezeichen)
  startete immer bei Schritt 1 – die erste Navigation ist vor `ngOnInit`
  abgeschlossen und kommt als Ereignis nie an
- Phase 8: die Meldung „Kein serieller Port gefunden" blieb stehen, obwohl
  Ports gefunden wurden – Optimus UI hält eine per `@if` entfernte `p-message`
  per Leave-Animation im DOM (in einem Hintergrund-Tab feuert `animationend` nie)
- Phase 8: `POST /api/setup/apply` stieß den Neustart an, ohne ihn
  abzuwarten – der Endpunkt wartet jetzt, bis der Host beendet ist
- Phase 6: **`src/.gitignore` verschluckte den Angular-Quellordner
  `src/ComfoBox.Web/src/app/features/diagnostics/logs/`** – die Regel `logs/`
  (ohne Pfadpräfix) traf auch jeden Ordner namens `logs` im Quellcode. Der
  LogViewer war dadurch **nicht im Repository** und der CI-Build wäre an
  derselben Stelle gebrochen wie der lokale Docker-Build. Die Laufzeit-Regeln
  sind jetzt verankert (`/ComfoBox.Gateway/logs/`). Dasselbe gilt für
  `.dockerignore`
- Phase 6: **`package-lock.json` war nicht synchron zu `package.json`**
  (`@emnapi/*` in zwei Versionen) – `npm ci` schlug deshalb fehl. Das
  Lock-File wurde plattformrein (unter Linux) neu erzeugt; danach
  funktionieren **beide** Plattformen
- Phase 6: **das Alpine-Basis-Image setzt
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`** – ohne `icu-libs` und ohne
  explizites `false` bricht die deutsche Formatierung in der Oberfläche
- Phase 5 (Aufgabe 5.13): ein angeforderter Neuaufbau der seriellen
  Schnittstelle wartet nicht mehr den Backoff (bis 30 s) ab – der Port wird
  **sofort** neu geöffnet
- Phase 4: **Dark-Mode der Weboberfläche** – der Seitenhintergrund blieb weiß,
  weil `--p-surface-50` ein Palette-Token ist und im Dark-Mode nicht
  umgeschaltet wird. Er kommt jetzt über `--cb-page-background` aus
  `light-dark()` und ist im Dark-Mode einen Tick heller als die Karten
- Phase 4: **Dark-Mode der Weboberfläche** – die Hilfeseiten, die Auswahlfelder
  und der Log-Viewer benutzten hellen Palette-Tokens (weißer Text auf hellem
  Grund). Sie nutzen jetzt semantische Tokens (`--p-content-hover-background`,
  `--p-content-border-color`, `--p-form-field-background`)
- Phase 3: **Befund A10** – nach **jedem** Reconnect wird `ComfoBox/Connected`
  mit `1` publiziert. Der Legacy tat das nur beim ersten Connect, danach sah
  Home Assistant dauerhaft „offline"
- Phase 3: **Befund A5** – alle Zahlen-Payloads sind kultur-invariant
  (`21.5`, niemals `21,5`). Auf deutschen Systemen wurden Schreibvorgänge
  dadurch verworfen oder als `235` interpretiert
- Phase 3: **Befund A6** – eine ungültige Enum-Payload wird abgelehnt und
  gemeldet. Der Legacy schrieb in diesem Fall den Wert `0`
- Phase 3: **Befund A9** – die Reconnect-Logik ist eine ereignisbasierte
  Schleife mit Backoff und `CancellationToken` statt der Rekursion des Legacy
- Phase 3: **Befund A11** – beim Shutdown wird erst `offline` publiziert, dann
  getrennt; der Last Will wird nicht fälschlich ausgelöst
- Phase 3: **Befund A24** – das Keep-Alive ist 30 s statt 2 s (der Legacy hat
  damit den Broker laufend getötet)
- Phase 3: **Befund A25** – TLS, Benutzername und Passwort sind pro Broker
  konfigurierbar; ein defekter Broker blockiert die anderen nicht
- Phase 1: **Befund A8** – ein Serial-Portfehler löste im Legacy einen
  Tight-Spin-Loop mit 100 % CPU aus. Der neue Zustandsautomat endet bei einem
  Portfehler, der Transport verbindet sich mit Backoff neu (5 s → 30 s)
- Phase 1: **Befund A19** – die Sende-Warteschlange des Legacy war eine
  ungeschützte `LinkedList`, auf die Aufrufer und Zustandsautomat gleichzeitig
  zugriffen. Jetzt `ConcurrentQueue`
- Phase 1: **Befund A23** – der MS/TP-Thread läuft mit `ThreadPriority.Highest`.
  Der neue Transport nutzt `TaskCreationOptions.LongRunning` (Standardpriorität)
- Phase 1: die Invoke-ID Vergabe im Client war nicht threadsicher (`m_invoke_id++`)
  → jetzt `Interlocked`
- Phase 2: **Befund A15** – die Prioritäts-Attribute (`[Once]`, `[Primary]`,
  `[Secondary]`) wurden im Legacy gelesen und **verworfen**; alle Items bekamen
  `RefreshPriority.None`. Der neue Katalog wertet sie aus, der Poll-Plan liest
  primäre Items in jedem und sekundäre Items nur alle 5 Zyklen
- Phase 2: **Befund A22** – der Legacy zählte `CurrentNumberOfWrites++` auch bei
  **fehlgeschlagenen** Schreibvorgängen. Der neue Zähler reserviert vor dem
  Schreiben und gibt die Reservierung bei einem Fehler wieder frei
- Phase 2: die Item-Id folgt der Regel camelCase → snake_case
  (`zone.set_point_normal`) – die Beispiele in der Spec wurden angepasst

## [0.1.0] – 2026-10-31

### Added
- Vollständige Spezifikation (`docs/02-modernisierung/`)
- 11 Entscheidungsprotokolle (ADR-001 … ADR-011)
- Aufgaben-Dokument `docs/06-aufgaben/` mit Phasen 0–8d
- Benutzerdokumentation (`docs/benutzer/`) mit Build-Skript
- Home-Assistant-Integration (HACS) und HA-Add-on-Manifest
- Synology-SPK-Paket mit Lifecycle-Skripten und host-seitigem Supervisor

### Changed
- Legacy-Bestand von `src/` nach `src_old/` verschoben (Archiv, wird nicht gebaut)

[Unreleased]: https://github.com/example/comfobox-mqtt/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/example/comfobox-mqtt/releases/tag/v0.1.0
