# Protokoll

> Chronologische Liste der Sessions. **Nach jeder Session** einen Eintrag ergänzen.
>
> Format: was wurde gemacht, was ist offen, was war überraschend.

## Eintrag Vorlage

````markdown
## 2026-MM-TT – <Titel> (Phase X, Aufgabe X.Y)

**Ergebnis:** <was jetzt funktioniert – konkret, nicht "implemented">

**Geänderte Dateien:**
- `pfad/datei.cs` – was

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → grün
- `dotnet test` → 42 Tests grün

**Offen / nicht gemacht:**
- <was bewusst ausgelassen wurde und warum>

**Überraschungen:**
- <was nicht wie erwartet war – wichtig für die nächste Session>
````

---

## 2026-11-01 – Synology-Paket & Getting-Started-Assistent (Aufgaben 8.1–8.9)

**Ergebnis:** Das Synology-Paket ist **vollständig**: Icon (256×256, reproduzierbar
erzeugt), `build-spk.sh` bricht ohne Icon ab, die **Setup-API** ist fertig
(Passwortregeln serverseitig, MQTT-Test unterscheidet *nicht erreichbar* /
*Authentifizierung fehlgeschlagen* / *keine Zustellung*, Zustand liefert die
aktuellen Werte für den wiederholbaren Assistenten), der **Assistent** läuft
über alle sechs Schritte (Ports werden bei Hotplug aktualisiert, der Broker ist
**optional**, der Neustart wird beobachtet) und der **SPK-Build läuft in der CI**
(Job `spk`, Artefakt + `packages.json` im GitHub Release).

**Geänderte Dateien (neu):**
- `deploy/synology-spk/src/PACKAGE_ICON.png` – das Paket-Icon (256×256)
- `deploy/synology-spk/tools/make-icon.mjs` – erzeugt das Icon reproduzierbar
  (reines Node.js, `zlib` – kein Python, kein ImageMagick)
- `deploy/synology-spk/tools/test-config-parse.sh` – Test für den JSON-Zugriff
  auf die `config.json` (der Parser ist Shell-Code und damit fehleranfällig)
- `src/ComfoBox.Gateway/Security/PasswordPolicy.cs` – die Passwortregeln
  (≥ 12 Zeichen, 3 von 4 Zeichenklassen) **serverseitig**
- `src/ComfoBox.Gateway.Tests/Api/FakeMqttBroker.cs` – ein minimaler MQTT-5-Broker
  für die Tests (CONNECT/CONNACK, SUBSCRIBE/SUBACK, PUBLISH mit Echo)

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Setup/SetupService.cs` – `GetState` liefert die aktuellen
  Werte; `WriteConfig` prüft Baudrate, Benutzername und Passwort; neue
  `Supervisor`-Sektion (`PassAllSerialDevices`); MQTT-Test mit Rundlauf
- `src/ComfoBox.Gateway/Api/SetupEndpoints.cs` – `apply` wartet den Neustart ab
- `src/ComfoBox.Gateway/Api/Models/SetupDtos.cs` – `SetupCurrentValuesDto`
- `src/ComfoBox.Web/src/app/core/state/setup.store.ts` – Vorausfüllung, Broker
  optional, Passwort-Wiederholung, Neustart beobachten
- `src/ComfoBox.Web/src/app/features/setup/setup-shell.ts` – Schritt ↔ URL synchron
- `src/ComfoBox.Web/src/app/features/setup/steps/serial.html` – Portliste zeigt
  den zuletzt verwendeten Port; Meldung wird **nicht** entfernt (Leave-Animation)
- `deploy/synology-spk/src/target/bin/common.sh` – `json_get` (abschnittsweise)
- `deploy/synology-spk/src/target/bin/supervisor.sh` – liest die echte `config.json`
- `deploy/synology-spk/src/scripts/postinst` – Setup-Modus-`config.json`
- `deploy/synology-spk/build-spk.sh` – Icon Pflicht, Image-Datei im Paket
- `.github/workflows/docker-publish.yml` – Job `spk`, Release-Artefakte
- `docs/03-deployment/synology-paketcenter.md` – `config.json`-Schema korrigiert

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- `dotnet test src/ComfoBox.sln -c Release` → **301 Tests grün**
  (46 BACnet + 30 Domain + 10 Contract + 215 Gateway) – vorher 293, **+8**
- Golden-Fixture `bacnet-frames.json` → SHA256 **unverändert**
  (`F95AA763…CE681B`, 5.657 Bytes)
- `docs/topics.json` → SHA256 **unverändert** (`67D7485B…C1463`)
- `bash deploy/synology-spk/tools/check-shell.sh` → grün (10 Skripte + Parser-Test)
- `bash deploy/synology-spk/build-spk.sh 1.0.0 0001` → `.spk` (30 KB), `checksum`
  in der INFO stimmt mit dem MD5 des Archivs überein
- `node tools/build-docs.mjs` → 12 Seiten, kein Abbruch
- `pwsh -File tools/check-links.ps1` → 639 Links, 0 Probleme
- `npm run lint` + `npm run build -- --configuration production` → grün
- **Im Browser verifiziert** (Gateway lokal auf Port 5199): alle sechs Schritte,
  Direktaufruf einer Schritt-URL, Schrittliste, „Zurück", Browser-Zurück,
  Broker optional (Test-Knopf ohne Host deaktiviert, „Weiter" möglich)

**Offen / nicht gemacht:**
- **6.4** (Test auf der echten DS923+) bleibt blockiert – das Gerät steht hier
  nicht zur Verfügung. Die SPK-Installation ist **nicht** auf echter DSM-Hardware
  geprüft.
- Der CI-Workflow ist **noch nicht gelaufen** (kein Tag gepusht). Der
  SPK-Release steht damit aus.
- **8b.2** (Options-Mapping) und **8d.1** (Token-API in der Oberfläche) sind
  Phase 8a–8d.

**Überraschungen:**
- ⚠️ **Das `config.json`-Schema in der Spec war falsch.** Die Spec zeigte
  Kleinbuchstaben-Abschnitte (`bacnet`, `mqtt`, `web`) – das Gateway liest aber
  die .NET-Options (`Bacnet`, `Mqtt`, `Api`). Der Supervisor hätte nichts
  gefunden. **Die Spec wurde korrigiert** (Kapitel §6.1).
- ⚠️ **`postinst` hätte den Container in eine Neustartschleife geschickt.**
  Die alte Startkonfiguration enthielt `"bacnet": { "port": "" }` und
  `"mqtt": { "brokers": [] }` → `ValidateOnStart` schlägt fehl → der Container
  startet nicht → **der Assistent ist nicht erreichbar**. Jetzt schreibt
  `postinst` nur noch `Api:SetupCompleted`; der Rest kommt aus dem Image.
- ⚠️ **MQTTnet 5 hat keine `PayloadSegment`-Property** (nur `get`) – der
  Rundlauf nutzt `ConvertPayloadToString()`. Und `SubscribeAsync` braucht
  `MqttClientSubscribeOptions`, **kein** `MqttApplicationMessage`.
- ⚠️ **`takeUntilDestroyed()` ohne Argument in `ngOnInit` → NG0203.**
  `ngOnInit` ist kein Injektionskontext – der `DestroyRef` muss übergeben
  werden.
- ⚠️ **Optimus UI hält eine per `@if` entfernte `p-message` im DOM stehen**
  (`p-message-leave-active`). In einem Hintergrund-Tab feuert `animationend`
  nie → die Meldung „Kein serieller Port gefunden" bleibt dauerhaft sichtbar,
  obwohl Ports gefunden wurden. **Regel:** `p-message` nicht per `@if`
  entfernen, sondern den Schweregrad umschalten.
- ⚠️ **Der Browser-Tab im VS-Code-Browser ist nicht sichtbar** → Klicks
  (`page.mouse.click`) erzeugen **kein** DOM-Ereignis. Für UI-Tests dort
  `element.click()` per `page.evaluate` verwenden.
- ⚠️ **`pnpm`/`npm`-Skript `tar` liest `C:/…` als Remote-Host** – im SPK-Build
  deshalb immer `cd <dir> && tar …`.

---

## 2026-11-01 – Container & Synology (Aufgaben 6.1–6.3, 6.5, 6.6)

**Ergebnis:** Das Gateway läuft als **ein** Container-Image (Gateway **+** Angular-UI,
261 MB, kein nginx). `docker build` ist grün, der Container startet **healthy**,
die SPA wird aus dem .NET-Host ausgeliefert, `docker stop` beendet ihn in 0,5 s
sauber per SIGTERM (tini) – und der **24-h-Schreibzähler übersteht einen
Container-Neustart** aus dem Volume. Backup und Restore sind **real getestet**.

**Geänderte Dateien (neu):**
- `src/ComfoBox.Gateway/Dockerfile` – 3 Stages (node → sdk → aspnet), tini als PID 1,
  `USER comfobox`, Healthcheck, VOLUME, ICU für die deutsche Formatierung
- `.dockerignore` – Build-Kontext (Repo-Root als Kontext, weil das Image
  .NET-Projekt **und** Angular braucht)
- `deploy/compose/compose.yaml` – der manuelle Betrieb (parametrisiert über `.env`)
- `deploy/compose/compose.test.yaml` – Override **ohne** Serial-Port (Entwicklerrechner)
- `deploy/compose/.env.example` – die Vorlage ohne Secrets
- `deploy/backup/backup.sh` + `restore.sh` + `backup-cron.sh` – Sicherung inkl. Schreibzähler
- `docs/benutzer/08-monitoring.md` – Monitoring, HA-Alarmierung, Backup (Benutzerdoku)

**Geänderte Dateien (bearbeitet):**
- `.github/workflows/docker-publish.yml` – **ein** Image statt zwei, Multi-Arch
  (`linux/amd64,linux/arm64`), Trivy vor dem Push
- `src/.gitignore` + `.dockerignore` – die Laufzeit-Regeln **verankert**
  (`/ComfoBox.Gateway/logs/` statt `logs/`)
- `src/ComfoBox.Web/package-lock.json` – plattformrein neu erzeugt
- `docs/03-deployment/containerisierung.md`, `konfiguration-und-secrets.md`,
  `docs/02-modernisierung/health-und-status.md` – auf den Ist-Stand gebracht

**Verifikation:**
- `docker build -f src/ComfoBox.Gateway/Dockerfile -t comfobox-gateway:local .` → grün
- Image: **261 MB**, `User: comfobox`, `Entrypoint: ["/sbin/tini","--"]`,
  Healthcheck `curl -fsS http://localhost:8080/api/health`, ICU vorhanden,
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=false`
- **Multi-Arch:** `docker buildx build --platform linux/arm64` → grün (QEMU, ~9 min),
  `uname -m` = `aarch64`, gleiche Eigenschaften (`User`, `tini`, Healthcheck, Volume)
- Trivy-Scan: **0 CRITICAL/HIGH**
- `docker compose up -d` → `Up (healthy)`, `GET /api/health` → `200 {"state":"online"}`
- `GET /` → 200 (1.322 B `index.html`), `GET /config` → 200 (**SPA-Fallback** greift)
- `GET /help/monitoring.html` → 200 (14.132 B) – die neue Seite ist im Bundle
- `docker stop` → **0,52 s**, Log: „Application is shutting down…" → MQTT beendet →
  Polling beendet → Transport gestoppt (Shutdown-Reihenfolge korrekt)
- Schreibzähler: Datei ins Volume gelegt → Container neu gestartet →
  `GET /api/diagnostics/writes` → `{"count":1,…}` → **persistent** ✅
- `docker compose down` (ohne `-v`) → das Volume `comfobox-data` **bleibt** ✅
- Healthcheck bei nicht erreichbarem Broker → Container bleibt **healthy** ✅
- Backup → Zählerdatei gelöscht → Restore → Volume wiederhergestellt → Container neu gestartet ✅
- `node tools/build-docs.mjs` → 12 Seiten, grün
- `pwsh -File tools/check-links.ps1` → 639 Links, **0 Probleme**

**Offen / nicht gemacht:**
- **6.4 (Test auf der DS923+)** – blockiert: die echte Synology und der
  USB-Adapter stehen hier nicht zur Verfügung. Die Prüfliste steht in
  [phase-6.md](phase-6.md); sie muss auf dem NAS abgearbeitet werden.
- Der CI-Workflow wurde auf **ein** Image und Multi-Arch umgestellt, aber
  **nicht ausgeführt** (kein Push auf ein Tag in dieser Session). Der Trivy-Scan
  ist damit noch unverifiziert.

**Überraschungen:**
- ⚠️ **Der Angular-Ordner `features/diagnostics/logs/` war nie im Repository.**
  `src/.gitignore` enthielt `logs/` (ohne Pfadpräfix) – das verschluckt jeden
  Ordner namens `logs`, auch Quellcode. Der CI-Build wäre an derselben Stelle
  gebrochen wie der lokale Docker-Build. **Regel:** Laufzeit-Regeln in
  `.gitignore`/`.dockerignore` immer **verankern** (`/ComfoBox.Gateway/logs/`).
- ⚠️ **`npm ci` scheiterte** – das Lock-File war nicht synchron zu `package.json`
  (`@emnapi/*` in zwei Versionen). Ein Lock-File, das unter Windows mit `npm install`
  erzeugt wurde, ist **nicht** plattformrein. Das Lock-File wurde deshalb im
  Linux-Container neu erzeugt – danach funktionieren **beide** Plattformen.
- ⚠️ **Das Alpine-Basis-Image setzt `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`.**
  Ohne `icu-libs` und ohne explizites `false` bricht die deutsche Formatierung
  in der Oberfläche. Das steht in keinem Dockerfile-Template.
- ⚠️ **`--force-local` ist keine Option für BusyBox-tar** (Alpine, DSM) – und
  GNU tar interpretiert einen Pfad mit Doppelpunkt (`C:/…`) als Remote-Host.
  Die portable Lösung ist `cd <Verzeichnis> && tar xzf <Basisname>`.
- ⚠️ **tar darf die Datei nicht einpacken, die es gerade schreibt** („file changed
  as we read it") – das Archiv gehört in ein **zweites** Verzeichnis.
- ⚠️ Ein temporäres Host-Verzeichnis lässt sich unter Windows **nicht** in den
  Container mounten. Die Backup-Skripte übertragen das Archiv deshalb über
  stdin/stdout statt über einen zweiten Mount.

---

## 2026-11-01 – Serielle Ports zur Laufzeit wählen (Aufgabe 5.13)

**Ergebnis:** Die seriellen Ports werden **gescannt** (Hotplug), der Benutzer
wählt sie in der Oberfläche aus, und die Umstellung ist **sofort** wirksam –
**kein Neustart** des Gateways nötig. Der Hinweis steht prominent auf dem
Dashboard, sobald BACnet nicht verbunden ist, und auf der Konfigurationsseite.
**293 Tests grün** (46 BACnet + 30 Domain + 10 Contract + 207 Gateway).

**Geänderte Dateien (neu – Gateway):**
- `src/ComfoBox.Gateway/Bacnet/SerialPortSettings.cs` – die **laufenden** Parameter (Port + Baudrate)
- `src/ComfoBox.Gateway/Bacnet/GatewaySerialPortFactory.cs` – die Fabrik liest die Parameter bei **jedem** Öffnen
- `src/ComfoBox.Gateway/Bacnet/BacnetSerialManager.cs` – Test + Umstellung (persistieren → Laufzeit → Neuaufbau)
- `src/ComfoBox.Gateway/Hardware/SerialPortScanner.cs` – die Port-Suche (`ports.json`, sonst `GetPortNames()`)
- `src/ComfoBox.Gateway/Api/SerialEndpoints.cs` – `GET /api/config/serial-ports`, `POST /api/config/bacnet/test`, `PUT /api/config/bacnet`

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Bacnet/Bacnet/BacnetMstpTransport.cs` – `RequestReconnect()` bricht den Backoff **ab** (Semaphore-Signal)
- `src/ComfoBox.Bacnet/Serial/ISerialPort.cs` + `SerialPortAdapter.cs` – `PortName` (nur Diagnose)
- `src/ComfoBox.Gateway/GatewayServiceCollectionExtensions.cs` – `SerialPortSettings` + die neue Fabrik
- `src/ComfoBox.Gateway/Api/GatewayApiExtensions.cs` – Scanner + Manager registriert, `MapSerial()`
- `src/ComfoBox.Gateway/Api/StatusEndpoints.cs` + `ConfigEndpoints.cs` – melden den **laufenden** Port
- `src/ComfoBox.Gateway/Setup/SetupService.cs` – nutzt jetzt den Scanner (keine Dopplung)

**Geänderte Dateien (neu – Web):**
- `src/ComfoBox.Web/src/app/core/api/serial.service.ts` – der API-Zugriff
- `src/ComfoBox.Web/src/app/shared/serial-port-picker.ts` + `.html` + `.scss` – die Port-Auswahl (wiederverwendbar)

**Geänderte Dateien (bearbeitet – Web):**
- `src/ComfoBox.Web/src/app/features/config/config.html` + `config.ts` – Port-Auswahl in der BACnet-Karte
- `src/ComfoBox.Web/src/app/features/dashboard/dashboard.html` + `dashboard.ts` + `dashboard.scss` – prominenter Hinweis

**Tests (neu):**
- `src/ComfoBox.Gateway.Tests/Api/SerialApiTests.cs` – 6 Tests (Portliste, Umstellung ohne Neustart, Status, ungültige Werte, Test)
- `src/ComfoBox.Bacnet.Tests/SerialPortBehaviourTests.cs` – `Ein_Angeforderter_Neuaufbau_Ignoriert_Den_Backoff`

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **293 Tests grün, 0 Fehler**
- `npm run lint` (src/ComfoBox.Web) → grün
- `npm run build` (src/ComfoBox.Web) → Produktions-Build erfolgreich
- **Live am Gateway** (Port 5199): `PUT /api/config/bacnet {"port":"COM7","baudrate":9600}` → **204**,
  danach `/api/status` meldet `port: "COM7"` – **ohne Neustart** (der Log zeigt
  den Neuaufbau in derselben Sekunde).

**Offen / nicht gemacht:**
- Der Setup-Assistent nutzt weiterhin seine eigene Port-Auswahl (Schritt 1) –
  er schreibt die Konfiguration am Ende ohnehin mit Neustart. Die neue
  Komponente ist bewusst **nicht** dort eingesetzt, um den Assistenten nicht
  zu verkomplizieren.
- Die SPA-Auslieferung (`wwwroot/`) ist unabhängig davon weiterhin offen
  (Aufgabe 5.8) – der Docker-Stage fehlt ebenfalls (Phase 6).

**Überraschungen:**
- ⚠️ **Ein Portwechsel braucht zwei Dinge**, nicht eines: (a) die
  `SerialPortSettings` (der Transport liest sie erst beim **Öffnen** des
  Ports) und (b) das **Abbruch** des Backoffs. Ohne (b) wartet der Transport
  bis zu 30 Sekunden, bevor er den neuen Port überhaupt versucht – der
  Benutzer hätte „nichts passiert" gedacht.
- ⚠️ **Der aktive Port lässt sich nicht testen**, indem man ihn öffnet – der
  Gateway hält ihn selbst. `BacnetSerialManager.Test()` erkennt den aktiven
  Port und meldet stattdessen den **Buszustand** (über `BacnetDevice`).
  Der Health-Reporter ist dafür zu optimistisch: er meldet „online", obwohl
  die Heizung gar nicht antwortet.
- ⚠️ **`FakeSerialPort` musste `IsOpen` per Default auf `true` setzen** und
  bei geschlossenem Port `SerialReadResult.Closed` liefern – sonst erkennt
  der MS/TP-Automat keinen Portfehler und der A8-Regressionstest wird
  wirkungslos.

---

## 2026-10-31 – Phase 3 abgeschlossen: MQTT-Migration & HA-Discovery (Aufgaben 3.1–3.6)

**Ergebnis:** Der Gateway publiziert alle **127 Topics**, abonniert die
`/Set`-Topics, meldet sich bei Home Assistant an und bleibt MQTT-seitig
stabil. **219 Tests grün** (45 BACnet + 30 Domain + 10 Contract + 134 Gateway).
Die Golden-Fixture und `docs/topics.json` sind **byteweise unverändert**.
M2Mqtt ist **vollständig** aus `src/` verschwunden.

**Geänderte Dateien (neu – Gateway/Mqtt):**
- `src/ComfoBox.Gateway/Mqtt/MqttPayload.cs` – kultur-invariante Zahlen, Enum-Parser **ohne** 0-Fallback (A5, A6)
- `src/ComfoBox.Gateway/Mqtt/MqttTopicMap.cs` – die **einzige** Stelle, an der Topic-Adressen gebildet werden
- `src/ComfoBox.Gateway/Mqtt/MqttConnectionFactory.cs` – Optionen, TLS, Auth, Keepalive, **zwei** LWT-Rollen
- `src/ComfoBox.Gateway/Mqtt/MqttConnectionManager.cs` – Connect/Reconnect/Backoff/Shutdown je Broker
- `src/ComfoBox.Gateway/Mqtt/BrokerConnection.cs` – die Verbindung **eines** Brokers (Haupt- + Will-Verbindung)
- `src/ComfoBox.Gateway/Mqtt/MqttClientRegistry.cs` – Verzeichnis der verbundenen Clients (bricht den DI-Kreis)
- `src/ComfoBox.Gateway/Mqtt/MqttPublisher.cs` – Publish mit QoS/Retain, Fehler pro Broker isoliert
- `src/ComfoBox.Gateway/Mqtt/MqttSubscriber.cs` – `/Set` + `/AsNumber/Set`, Schreiben **nur** über `BacnetDevice`
- `src/ComfoBox.Gateway/Mqtt/MqttStatePublisher.cs` – Item-Werte auf ihre Topics (aus dem Wert-Cache)
- `src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs` – Discovery für alle 127 Items + Gerät + Status
- `src/ComfoBox.Gateway/Mqtt/HomeAssistantBirthHandler.cs` – reagiert auf die Geburt von Home Assistant (`homeassistant/status` = `online`)
- `src/ComfoBox.Gateway/Mqtt/MqttStatusPublisher.cs` – Health-/Status-Topics (online/degraded/offline)
- `src/ComfoBox.Gateway/Mqtt/MqttVersion.cs` – Version (`Ver 1.0.0` / `1.0.0`)
- `src/ComfoBox.Gateway/Mqtt/IMqttPublisher.cs`, `IMqttConnectionHandler.cs` – die Naahte
- `src/ComfoBox.Gateway/GatewayServiceCollectionExtensions.cs` – die gesamte DI-Registrierung (aus `Program.cs` herausgezogen)

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Program.cs` – nur noch Logging + `AddGatewayServices` + `/api/health`
- `src/ComfoBox.Gateway/Configuration/MqttOptions.cs` – `DiscoveryPrefix`, `UseMqttRetain`, `StatusIntervalSeconds`, `AllowUntrustedCertificates`, Client-ID mit Hostname
- `src/ComfoBox.Gateway/Health/HealthState.cs` + `HealthReporter.cs` – Health-Aggregation über die Komponenten
- `src/ComfoBox.Gateway/Health/ErrorCollector.cs` – Ereignis `Reported` (Status-Topic reagiert sofort)
- `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` – meldet den BACnet-Zustand an den Health-Reporter
- `src/ComfoBox.Gateway/appsettings.json` – neuer `Mqtt`-Abschnitt
- `src/ComfoBox.Gateway/ComfoBox.Gateway.csproj` – `InternalsVisibleTo` für die Tests

**Geänderte Dateien (neu – Tests):**
- `src/ComfoBox.Gateway.Tests/MqttPayloadTests.cs` – A5 + A6 (de-DE-Kultur)
- `src/ComfoBox.Gateway.Tests/MqttTopicMapTests.cs` – Topic-Vertrag gegen `docs/topics.json`
- `src/ComfoBox.Gateway.Tests/MqttSubscriberTests.cs` – Schreibpfad **inkl.** Schreibzähler
- `src/ComfoBox.Gateway.Tests/HomeAssistantDiscoveryTests.cs` – Discovery-Schema
- `src/ComfoBox.Gateway.Tests/MqttConnectionIntegrationTests.cs` – **echter** Broker im Prozess, A10-Regression
- `src/ComfoBox.Gateway.Tests/GatewayEndToEndTests.cs` – der komplette Gateway gegen einen Broker
- `src/ComfoBox.Gateway.Tests/HealthAndStatusTests.cs`, `NoopMqttClient.cs`, `RecordingMqttPublisher.cs`
- `src/ComfoBox.Gateway.Tests/HomeAssistantBirthHandlerTests.cs` – die Geburt von Home Assistant
- `src/ComfoBox.Gateway.Tests/HomeAssistantBirthHandlerTests.cs` – die Geburt von Home Assistant

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **219 Tests grün, 0 Fehler**
  (45 BACnet + 30 Domain + 10 Contract + 134 Gateway; 143 aus Phase 2 unverändert grün)
- Golden-Fixture **byte-identisch**:
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B` (5.657 B)
- `docs/topics.json` **byte-identisch**:
  `67D7485BF961B2B6D11BA5E788932056F073B564F52922D2DE8B34DFC10C1463` (33.845 B)
- `git diff --stat` auf beide Fixtures → **leer**
- **3.1** geprüft: kein `M2Mqtt`/`uPLibrary` in `src/**/*.cs` und `*.csproj`
- **3.3** geprüft: kein `System.Diagnostics.Trace`, kein `Console.WriteLine` in `src/**/*.cs`
- **A10** geprüft: `Connected_Wird_Nach_Jedem_Reconnect_Publiziert` (Broker-Neustart im Test)
- **A6** geprüft: `Eine_Ungueltige_Enum_Payload_Schreibt_Keinen_Null_Wert`

**Offen / nicht gemacht:**
- Der **Neustart-Befehl** (`ComfoBox/Command/Restart`) ist noch nicht implementiert –
  er gehört zu Phase 4 (Aufgabe 4.5 `ServiceControl`). Der Topic ist reserviert
  (`MqttTopicMap.RestartCommandTopic`), wird aber noch nicht abonniert.
- Die **Discovery auf die HA-Geburt** (`homeassistant/status` → `online`) ist
  implementiert: `HomeAssistantBirthHandler` abonniert das Topic und wiederholt
  Discovery **und** Zustände.
- Die **eingebettete Discovery der `AsNumber`-Variante** fehlt bewusst –
  die Spec empfiehlt sie nicht per Default (sonst verdoppelt sich die
  Entitätenzahl).
- Die **MQTT-Sicherheit** (TLS-Zertifikate, `RestartToken`) ist noch offen –
  siehe ADR-002/ADR-009.
- **Kein Realtest** gegen eine echte ComfoBox und keinen echten Broker
  (Mosquitto): die Tests laufen gegen den **eingebetteten** MQTTnet-Broker.

**Überraschungen:**
- ⚠️ **MQTT 5 erlaubt nur EINEN Last Will pro Verbindung.** Der Vertrag
  verlangt aber zwei (`Status/Health` **und** `Connected`). Lösung: der
  Gateway hält je Broker **zwei** Verbindungen – die Hauptverbindung (Will auf
  `Status/Health`) und eine reine Will-Verbindung (Will auf `Connected`, sonst
  nichts). Die Will-Verbindung bekommt die Client-ID `{ClientId}-will`.
- ⚠️ **Ein zirkulärer DI-Abhängigkeitspfad hat den Testhost blockiert**
  (nicht abgestürzt – 21 Threads, 0 CPU): `MqttConnectionManager` →
  `IMqttConnectionHandler` → `MqttSubscriber` → `IMqttPublisher` →
  `MqttPublisher` → `IMqttClientProvider` → `MqttConnectionManager`. MS-DI
  erkennt den Kreis **nicht** (der Provider war ein `Func`-Factory), sondern
  blockiert beim Auflösen. **Fix:** `MqttClientRegistry` als eigenes Singleton;
  der Manager pflegt es, der Publisher liest es. → **Merksatz:** Der
  Connection-Manager darf **nie** der `IMqttClientProvider` sein, wenn er
  Handler injiziert bekommt.
- ⚠️ **`dotnet test` braucht bei einem hängenden Testhost einen
  `dotnet-dump collect --process-id <PID> --type Full`** – der Standard-Output
  zeigt nur „Der Testhostprozess ist abgestürzt". Mit `clrstack -all` im Dump
  sieht man den blockierenden Frame. (Tool war schon global installiert.)
- ⚠️ **`Host.CreateDefaultBuilder()` + `ConfigureServices(c => c.AddSingleton(services))`
  registriert den Container als Singleton** – die eigenen Dienste fehlen dann
  („No service for type … has been registered"). Der richtige Weg:
  `new HostBuilder().ConfigureServices((_, c) => c.AddGatewayServices(...))`.
- ⚠️ **`dotnet-dump analyze` kennt die SOS-Kommandos nicht** – `set threads` und
  `~*s` schlagen fehl. Richtig sind `clrthreads` und `clrstack -all`.
- ⚠️ **Ein Test, der den echten `BacnetMstpTransport` startet, blockiert** –
  `SerialPort.Open()` auf einem nicht existierenden Port wirft, der
  Reconnect-Backoff ist 5 s, aber `BacnetMstpTransport.Dispose()` ruft
  `StopAsync().GetAwaiter().GetResult()` → **blockierendes** Dispose. Für
  End-to-End-Tests den `IBacnetClient` per `AddSingleton<IBacnetClient,
  FakeBacnetClient>()` überschreiben.
- ⚠️ **Ein Test, der per `Assert.True(Has(topic))` prüft, muss `topic=payload`
  vergleichen** – die Discovery-Payload ist JSON, `Has(topic)` findet sie nie.
- ⚠️ **`MqttApplicationMessage.PayloadSegment` hat keinen öffentlichen
  Getter** – für die Payload im Test `ConvertPayloadToString()` nehmen.
- ⚠️ **`MqttClientOptionsBuilder.WithTlsOptions` erwartet
  `Func<MqttClientCertificateValidationEventArgs, bool>`** – nicht vier
  Argumente (das war die API von MQTTnet 4).
- ⚠️ **`MqttClientSubscribeOptionsBuilder.WithTopicFilter` nimmt **einen**
  Filter, keine Liste** – im Aufruf auflösen, sonst findet der Overload
  `WithTopicFilter(string)` nicht statt.
- ⚠️ **`MqttClientOptions` hat kein `WithLastWillTopic` für zwei Wills** –
  s. o.
- ⚠️ **`MqttClientFactory.CreateMqttClient()` ist statisch** und existiert
  auch ohne Argument; `MqttServerFactory` (Broker) liegt im transitiven Paket
  `MQTTnet.Server` – das kommt über `MQTTnet.AspNetCore` mit.
- ⚠️ **`MqttApplicationMessageReceivedEventArgs` hat einen 4-Parameter-Konstruktor**
  `(clientId, message, publishPacket, autoAcknowledge)` – für einen Test-Client
  `null!` für die letzten beiden.
- ⚠️ **Die Legacy-Datei `src_old/ComfoBoxMqtt/ComfoBoxMqttClient.cs` benutzt
  `keepAlivePeriod = 2`** (Befund A24) – der neue Wert ist 30 s, wie in der Spec.

---

## 2026-10-31 – Phase 1 abgeschlossen: BACnet-Stack läuft async auf .NET 10 (Aufgaben 1.4–1.8)

**Ergebnis:** Der BACnet-Stack ist vollständig portiert. `BacnetClient` arbeitet
mit `async`/`await` und typisierten Exceptions, der MS/TP-Transport verwaltet
Port, Reconnect und Backoff, der Zustandsautomat läuft 1:1 wie im Legacy –
ohne dessen Spin-Loop. 45 Tests grün (26 Golden + 19 neu), die Golden-Fixture
ist **byteweise unverändert**. Für die Portierung war **kein Docker und keine
ComfoBox/kein Serial-Adapter** nötig: alle Tests laufen offline gegen
In-Memory-Attrappen.

**Geänderte Dateien (neu):**
- `src/ComfoBox.Bacnet/Serial/ISerialPort.cs` – schmale Port-Schnittstelle
- `src/ComfoBox.Bacnet/Serial/SerialReadResult.cs` + `SerialReadStatus.cs` – Lesergebnis
- `src/ComfoBox.Bacnet/Serial/BacnetSerialOptions.cs` – Portname, Baudrate, Timeouts
- `src/ComfoBox.Bacnet/Serial/SerialPortAdapter.cs` – Adapter auf `System.IO.Ports.SerialPort`
- `src/ComfoBox.Bacnet/Serial/ISerialPortFactory.cs` + `SerialPortFactory.cs` – Testnaht
- `src/ComfoBox.Bacnet/Bacnet/BacnetMstpOptions.cs` – Station, MaxMaster, Reconnect-Backoff
- `src/ComfoBox.Bacnet/Bacnet/MstpStateMachine.cs` – **1:1-Port** des Legacy-Automaten
- `src/ComfoBox.Bacnet/Bacnet/MstpOutboundFrame.cs` – Frame in der Sendewarteschlange
- `src/ComfoBox.Bacnet/Bacnet/MstpState.cs` – Zustände (Legacy-Namen + `Fatal`)
- `src/ComfoBox.Bacnet/Bacnet/IBacnetTransport.cs` – Transport-Interface (nur MS/TP)
- `src/ComfoBox.Bacnet/Bacnet/BacnetMstpTransport.cs` – Port, Reconnect, Backoff
- `src/ComfoBox.Bacnet/Bacnet/BacnetFrameReceivedEventArgs.cs` – Frame-Ereignis
- `src/ComfoBox.Bacnet/Bacnet/BacnetClient.cs` – **async** Client
- `src/ComfoBox.Bacnet/Bacnet/BacnetExceptions.cs` – typisierte Fehler
- `src/ComfoBox.Bacnet/Bacnet/BacnetIamEventArgs.cs` – IAm-Ereignis
- `src/ComfoBox.Bacnet.Tests/FakeSerialPort.cs` + `FakeSerialPortFactory.cs`
- `src/ComfoBox.Bacnet.Tests/FakeTransport.cs` – Transport-Attrappe
- `src/ComfoBox.Bacnet.Tests/MstpFramingTests.cs` – Framing + Zustandsautomat
- `src/ComfoBox.Bacnet.Tests/SerialPortBehaviourTests.cs` – **A8-Regression**
- `src/ComfoBox.Bacnet.Tests/BacnetClientTests.cs` – Request/Antwort/Fehler

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Bacnet/ComfoBox.Bacnet.csproj` – `InternalsVisibleTo` für die Tests
- `src/ComfoBox.Bacnet/Serial/SerialPortFactory.cs` – jetzt `ISerialPortFactory`

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **45 Tests grün, 0 Fehler**
  (26 Golden-Tests unverändert + 19 neue Tests)
- Fixture-Prüfsumme **unverändert**:
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B` (5.657 Bytes)
- **1.6** geprüft: keine Treffer für `kernel32`, `UdpClient`, `Socket`, `PipeStream`,
  `BacnetIpUdpProtocolTransport`, `BacnetPtpProtocolTransport` in `src/**/*.cs`
- **1.7** geprüft: kein `Trace.` und kein `Console.Write` in `src/**/*.cs`
- **A23** geprüft: kein `ThreadPriority.Highest` im neuen Code

**Offen / nicht gemacht:**
- Der **Gateway-Anschluss** fehlt noch: der neue `BacnetClient` ist nicht in
  `ComfoBox.Gateway` eingebunden (das ist Aufgabe 2.3/2.4 der Phase 2).
- **Segmentierte Antworten** werden zusammengesetzt, aber nicht vollständig
  gegen die SegmentAck-Semantik des Legacy geprüft (Realtest braucht Hardware).
- Der Legacy unterstützt **ReadPropertyMultiple**, **ReadRange**, **File**,
  **SubscribeCOV** – diese Dienste sind **nicht** portiert. Der ComfoBox nutzt
  laut `src_old/ComfoBoxLib` nur `ReadProperty` und `WriteProperty`.
- **Kein Realtest** gegen die ComfoBox (kein Serial-Adapter an dieser Maschine).

**Überraschungen:**
- ⚠️ **`create_file` überschreibt keine vorhandene Datei** – der Versuch,
  `SerialPortFactory.cs` neu anzulegen, schlug mit „File already exists" fehl.
  Lösung: Datei erst löschen (`Remove-Item`) oder mit `replace_string_in_file`
  bearbeiten. **Vorher im Repo prüfen, ob die Datei schon existiert.**
- ⚠️ **Die Antwort-Zuordnung darf nicht über die Adresse laufen.** Der erste
  Entwurf hat die wartende Anfrage über `(Invoke-ID, MAC-Adresse)` identifiziert.
  Bei Broadcast-Antworten (IAm) passt die Absenderadresse aber nicht zur
  Zieladresse – die Anfrage lief ins Timeout. Der Legacy ordnet **nur über die
  Invoke-ID** zu; das ist jetzt so übernommen (`ConcurrentDictionary<byte, …>`).
- ⚠️ **`MSTP.Encode` schreibt die Daten-CRC *hinter* die Nutzdaten.** Wer den
  Frame baut, muss die Nutzdaten **vor** dem Aufruf in den Puffer kopieren –
  sonst überschreibt `Encode` sie mit der CRC. Zwei Tests sind daran initially
  gescheitert.
- ⚠️ **Der Zustandsautomat verschluckt Frames in der Poll-For-Master-Runde.**
  Ein Frame, der nach dem Start des Automaten eintrifft, landet in der
  PFM-Runde und wird verworfen. Für Tests muss der Frame **vor** dem Start
  bereitliegen.
- Der Legacy wartete 250 ms (`T_REPLY_DELAY`) auf eine mögliche Antwort, bevor
  er „Reply Postponed" sendete. Da das Gateway ein reiner Client ist, wird
  „Reply Postponed" sofort gesendet – **das ist eine Abweichung vom Legacy**
  (dokumentiert in `MstpStateMachine`).
- Der Legacy hatte **keinen** `ThreadPriority.Highest`-Ersatz im neuen Code –
  der Transport nutzt `TaskCreationOptions.LongRunning` (eigener Thread,
  Standardpriorität).
- `dotnet test` auf der Solution meldet für `ComfoBox.Domain.Tests`,
  `ComfoBox.Contract.Tests` und `ComfoBox.Gateway.Tests` „kein Test verfügbar" –
  das ist **vorbestehend** (diese Projekte enthalten noch keine Tests) und
  kein Fehler dieser Session.

---

## 2026-10-31 – Phase 1: BACnet-Codec portiert, Golden-Fixture erzeugt (Aufgaben 1.1–1.3, 1.6, 1.7)

**Ergebnis:** Der BACnet-Codec läuft verhaltensgleich auf .NET 10. 25 Golden-Frames,
erzeugt vom **Legacy-Codec selbst**, werden vom portierten Codec byteweise
reproduziert – 26 Tests grün. Der Codec liegt als
`src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` vor (Fremdcode, MIT, 1:1 portiert).

**Geänderte Dateien:**
- `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` – **neu**: 1:1-Port von `BACnetBase.cs`
  (7.471 Zeilen), Namespace `ComfoBox.Bacnet`, ohne `BVLC` (BACnet/IP) und `PTP`
- `src/ComfoBox.Bacnet/Diagnostics/BacnetLog.cs` – **neu**: `ILogger`-Fassade für den
  statischen Fremdcode (Befund A16)
- `src/ComfoBox.Bacnet/Bacnet/HEADER-LIZENZ.txt` – **neu**: MIT-Lizenz + Portierungsprotokoll
- `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json` – **neu**: 25 Golden-Frames
- `src/ComfoBox.Bacnet.Tests/BacnetCodecGoldenTests.cs` + `BacnetFrames.cs` – **neu**
- `tools/port-bacnet/port-bacnet-base.mjs` – **neu**: mechanischer Port
- `tools/port-bacnet/oracle/` – **neu**: Referenz-Oracle (kompiliert den Legacy-Codec)
- `tools/port-bacnet/README.md` – **neu**
- `src/ComfoBox.Bacnet/ComfoBox.Bacnet.csproj` – `NoWarn` + `EnableNETAnalyzers=false`
  für den Fremdcode

**Verifikation:**
- `cd src && dotnet build ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- `cd src && dotnet test ComfoBox.sln -c Release` → **26 Tests grün** (1 Projekt, 25 Frames)
- Jeder Frame wird dekodiert und mit dem Erwartungswert der Fixture verglichen
- Die Adress-Frames werden **erneut kodiert** und byteweise verglichen

**Offen / nicht gemacht:**
- **1.4 `BACnetClient`** (2.098 Zeilen) – offen
- **1.5 `BACnetTransport`** (2.005 Zeilen, MSTP + Serial) – offen
- **1.8** Timeout-/Reconnect-Härtung – offen (setzt 1.4/1.5 voraus)
- 1.6 und 1.7 sind nur für den **Codec** erledigt; die Transports (Pipe/UDP/PTP)
  und deren `Trace`-Aufrufe folgen mit 1.4/1.5.

**Überraschungen:**
- ⚠️ **`docs/Mono/MoMA Scan Results.html` enthält KEINE BACnet-Frames.** Die Datei ist
  ein **Mono-Missing-Analysis-Report** (welche BCL-Methoden in Mono fehlen):
  0 Hex-Dumps, keine „I-Am", kein „NPDU", keine „Who-Is".
  Prüfsumme des Originals: `012C9862185C83B6E3432BDABD3C8C39F5EC57884CD837F92E399BFB2C14822F`
  (unverändert). → **Die Prämisse der Aufgabe 1.1 war falsch.** Die Golden-Frames
  stammen stattdessen vom Legacy-Codec selbst (Referenz-Oracle).
- Der Legacy-Codec ist **nicht** standalone: `BACnetBase.cs` referenziert
  `BacnetIpUdpProtocolTransport` aus `BACnetTransport.cs` (nur für den
  BACnet/IP-BVLC-Code). Für den Oracle genügt ein Stub.
- `Services.DecodeIamBroadcast` gibt bei Erfolg **0** zurück (`return offset - org_offset`),
  nicht die Länge – eine Legacy-Eigenheit, die der Golden-Test festschreibt.
- Der Legacy-Codec braucht **7.500 Zeilen** `#nullable disable` – mit
  `Nullable=enable` + `TreatWarningsAsErrors` sind das ~40 Fehler.
- Die .NET-Analyzer monieren im Fremdcode bewusste Eigenheiten des BACnet-Standards
  (CA1707 Unterstriche, CA1069 doppelte Enum-Werte, CA1051 sichtbare Felder,
  CA1711 Namenskonvention) → für das Projekt deaktiviert.
- `CA1707` moniert auch **Testmethodennamen** mit Unterstrichen – die sind hier
  Haustil (vgl. `csharp.instructions.md`) → `NoWarn` in den Testprojekten.
- Der Legacy-Codec liest über den Puffer hinaus (`ASN1.decode_*` ohne Längenprüfung):
  der Oracle hat deshalb einen 1-KB-Puffer, der Test exakt 12 Byte. Ergebnis gleich –
  aber der Test ist strenger.

---

## 2026-10-31 – Phase 0: Repo-Hygiene & CI (Aufgaben 0.1–0.8)

**Ergebnis:** Das Fundament steht. `src/` enthält eine gepinnte .NET-10-Solution
mit sieben SDK-Projekten, zentrale Paketverwaltung, Qualitäts-Gate
(`TreatWarningsAsErrors`), Lock-Dateien, CI mit Build/Test/Fixture-Prüfung und
einen `CHANGELOG.md`. Der Generator für die Topic-Fixture reproduziert
`docs/topics.json` **byteidentisch** (127 Items) – die Fixture ist damit
maschinell abgesichert.

**Geänderte Dateien:**
- `src/global.json` – SDK-Pinning 10.0.400 (`rollForward: latestFeature`)
- `src/Directory.Packages.props` – 20 Pakete zentral gepinnt
  (`CentralPackageTransitivePinningEnabled`)
- `src/Directory.Build.props` – `net10.0`, C# 14, `Nullable`, `TreatWarningsAsErrors`,
  `GenerateDocumentationFile`, `RestorePackagesWithLockFile`
- `src/ComfoBox.sln` + 7 `.csproj` – Domain/Bacnet/Gateway + 4 Testprojekte
- `CHANGELOG.md` – neu
- `tools/generate-topics/generate-topics.mjs` + `README.md` – Generator & Prüfmodus
- `.github/workflows/build-test.yml` – `working-directory: src`, `global-json-file`,
  Lock-Dateien, Job „Fixture prüfen", Legacy-Job → Archiv-Prüfung

**Verifikation:**
- `cd src && dotnet --version` → `10.0.400`
- `cd src && dotnet build ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- `cd src && dotnet test ComfoBox.sln -c Release` → grün (0 Tests, erlaubt)
- `node tools/generate-topics/generate-topics.mjs --check` → „Fixture ist deckungsgleich (127 Items)"
- Regenerierte Datei vs. committete Fixture → identisch bis auf **eine** dokumentierte
  Abweichung (`ComfoBox/Warmwater/DoHeatWaterNow`: `DateTime?` → `float?`, 3 Zeichen)
- `ComfoBox.Domain` → keine `PackageReference` (framework-frei)
- Projektreferenzen: Bacnet → Domain, Gateway → Domain + Bacnet, keine Zyklen
- Kein `<PackageReference>` mit `Version=`-Attribut → kein `NU1008`
- `Class1.cs` / `UnitTest1.cs` entfernt → kein `CS1591`

**Offen / nicht gemacht:**
- Die CI-Pipeline selbst wurde **nicht** ausgeführt (kein Push in dieser Session) –
  der Workflow ist aber gegen die lokalen Kommandos verifiziert.
- `packages.lock.json` sind generiert, aber noch nicht committet.

**Überraschungen:**
- `dotnet new web` liefert im .NET-10-SDK Pakete, die der Web-SDK bereits mitbringt →
  `NU1510` (Warnung als Fehler). Betroffen: `Microsoft.Extensions.Hosting`,
  `Microsoft.Extensions.Configuration.Json`, `…EnvironmentVariables` → entfernt.
- `dotnet new sln` erzeugt standardmäßig **`.slnx`** → `--format sln` ist Pflicht.
- Die Template-`PackageReference` enthalten `Version=` → `NU1008` mit CPM.
- `Swashbuckle.AspNetCore 9.0.6` existiert (die Spec nennt „9.x").
- **Die Legacy-Dateien sind Windows-1252-kodiert**, nicht UTF-8 – ein UTF-8-Leser
  erzeugt `�` in Umlauten. Der Generator dekodiert sie mit einer eigenen CP1252-Tabelle.
- `\w` in JavaScript matcht **nur ASCII** – Bezeichner wie `Gebäudeträgheit` brauchen
  `\p{L}` mit dem `u`-Flag.
- In `Config.cs` stehen **auskommentierte** Items (`States/Outputs/R2`, `R6`, `R8`, `R9`) –
  ein Parser ohne Kommentar-Stripping zählt sie mit.
- `docs/topics.json` hat ein **UTF-8-BOM** → `JSON.parse` braucht vorher ein `.replace(/^﻿/, '')`.
- Die Fixture stammt von 2017 und ist seitdem **nicht** mehr deckungsgleich mit dem
  Legacy-Katalog (ein Werttyp, vier auskommentierte Items). Die Fixture bleibt
  unverändert – sie ist der Vertrag.

---

## 2026-10-31 – Spezifikation, SPK-Skelett, HA-Ökosystem, Benutzerdoku (Phasen 0–8d, D.1–D.8)

**Ergebnis:** Die gesamte Spezifikation liegt vor. `src_old/` ist vom Altbestand getrennt,
das SPK baut, die HA-Integration und das Add-on sind als Skelett vorhanden, die
Benutzerdoku erzeugt HTML.

**Geänderte Dateien:**
- `src/` → `src_old/` verschoben (`git mv`, 100 Dateien, Historie erhalten)
- `src/README.md` + `src/.gitignore` – Zielstruktur der Neuzuimplementierung
- `docs/` – 10 neue Dokumente (ADRs 007–010, Specs, Installationsstufen)
- `deploy/synology-spk/` – SPK-Skelett, alle 9 Shell-Skripte `bash -n`-geprüft
- `deploy/ha-addon/` – Add-on-Manifest mit `device(subsystem=tty)`, `ingress`, `watchdog`
- `custom_components/comfobox/` – 11 Python-Module + Tests
- `hacs.json` – HACS-Metadaten
- `docs/benutzer/` – 11 Seiten mit Front-Matter
- `tools/build-docs.mjs` – Markdown → HTML mit Link-Umschreibung
- `tools/check-links.ps1` – Link-/Anker-Validator (437 Links, 0 Fehler)
- **Indizes**: `docs/README.md`, `docs/02-modernisierung/README.md`,
  `docs/05-entscheidungen/README.md` um `06-aufgaben`, `docs/benutzer/` und ADR-010 ergänzt
- `.github/instructions/docs.instructions.md` – neue Abschnitte „Benutzerdokumentation"
  (Front-Matter, Tonfall) und „Aufgaben-Dokument" (Statuslogik)
- `docs/02-modernisierung/ui-angular-optimus.md` – neuer §13 „Hilfe-Bereich" (Route `/help`,
  öffentlich ohne Anmeldung, Suchindex, Sicherheits-Betrachtung)
- `src/README.md` – Verweise auf Benutzerdoku, Doku-Build und Aufgaben-Dokument
- `.gitignore` (Root) – Python-Regeln ergänzt (`__pycache__/` u. a.)

**Verifikation:**
- `bash -n` über alle 9 SPK-Skripte → grün
- `build-spk.sh` → `ComfoBoxGateway_noarch-dsm7_1.0.0-0001.spk`
- `check-links.ps1` → 84 Dateien, 534 Links, 0 Probleme
- `build-docs.mjs` → 11 HTML-Seiten + `index.json`
- `git check-ignore -v` → `.env`, `appsettings.override.json`, `*.user`, `node_modules/`,
  `dist/`, `__pycache__/` werden ignoriert; `git ls-files deploy/synology-spk/src/target/bin/`
  → 3 `.sh`-Dateien (Aufgabe **0.5** damit verifiziert)

**Offen / nicht gemacht:**
- **Kein Python auf der Maschine** → die HA-Integration wurde **manuell reviewed**,
  aber **nicht** von `pytest`/`ruff` verifiziert. Drei echte Fehler wurden dabei
  gefunden und behoben (unausgewerteter `await` beim Service-Setup, fehlerhafte
  Content-Type-Prüfung, `config_entry`-Zugriff im Unique-ID).
- **Kein Docker-Daemon** → die Container-Aufgaben sind nicht verifizierbar.
- Die **Solution** (`src/ComfoBox.sln`) existiert noch nicht → Aufgabe 0.4.
- Die **Golden-Tests** existieren noch nicht → Aufgabe 1.2 ist die **Voraussetzung**
  für die gesamte Portierung.

**Überraschungen:**
- `git mv src src_old` scheitert an einem gesperrten Verzeichnis (`ComfoBoxMqtt`) –
  Windows-Handle. Workaround: Unterverzeichnisse einzeln verschieben.
- Das Root-`.gitignore` hat `[Bb]in/` und hätte `deploy/synology-spk/src/target/bin/*.sh`
  ignoriert → Ausnahme in `deploy/synology-spk/.gitignore` ergänzt.
- Die Benutzerdoku-Links mussten **im HTML** auf `.html` umgeschrieben werden –
  der Build erledigt das jetzt automatisch.
- **GitHub-Anker-Regel:** jedes Leerzeichen → **ein** Bindestrich, Whitespace-Runs bleiben.
  „## 4.7 – Setup-API (Getting Started) · 1.5 PT“ → `#47--setup-api-getting-started--15-pt`.
  Der Validator bildet das jetzt genauso ab; vier Anker im Aufgaben-Dokument waren falsch.
- `docs/benutzer/03-Installation.md` → `03-installation.md` (Kleinschreibung konsistent).

---

## 2026-10-31 – Python-Toolchain, HA-Tests grün, Logging-Spec (D.9, Aufgabe 8a.6)

**Ergebnis:** Python 3.12 ist installiert, die HA-Integrationstests laufen grün (3/3),
`ruff` ist sauber. Die Logging-Entscheidung (Serilog + In-Memory-Ringbuffer mit
1000 Einträgen für den Log-Viewer) ist als Spec + ADR dokumentiert.

**Geänderte Dateien:**
- `pytest.ini` (**neu**) – `asyncio_mode = auto`, `asyncio_default_fixture_loop_scope`,
  `testpaths` auf `custom_components/comfobox/tests`
- `custom_components/comfobox/tests/conftest.py` – zwei Windows-Workarounds (siehe unten)
- `docs/02-modernisierung/logging.md` (**neu**) – Serilog, Ringpuffer, API, Log-Viewer
- `docs/05-entscheidungen/adr-011-logging-serilog-und-ringbuffer.md` (**neu**)
- `docs/02-modernisierung/dotnet-migration.md` – §5 um Ringpuffer-Sink erweitert
- `docs/02-modernisierung/ui-angular-optimus.md` – §7.1 Log-Viewer, DoD +2
- `docs/02-modernisierung/datenmodell-und-api.md` – 3 Log-Endpunkte
- `docs/02-modernisierung/health-und-status.md` – Abgrenzung Fehler- vs. Log-Puffer
- `docs/03-deployment/konfiguration-und-secrets.md` – `Logging:RingBuffer:*`
- `docs/06-aufgaben/phase-2.md` – **2.7** Serilog + Ringpuffer (1.0 PT)
- `docs/06-aufgaben/phase-4.md` – **4.9** Log-Endpunkte + SSE-Tail (0.5 PT)
- `docs/06-aufgaben/phase-5.md` – **5.12** Log-Viewer (1.5 PT)
- `docs/06-aufgaben/status.md` – neue Zeilen, Summe 67 → **72 PT**
- `.github/instructions/csharp.instructions.md` – §5.1 Serilog & Ringpuffer
- `docs/benutzer/06-bedienung.md` – Abschnitt „Das Protokoll"
- `docs/benutzer/09-fehlersuche.md` – „Was im Protokoll nachsehen"
- `docs/06-aufgaben/prompts/Prompt-Phase-0.md` (**neu**) – kopierfertiger Session-Prompt
- `docs/06-aufgaben/prompts/Prompt-Phase-1.md` (**neu**) – Prompt für die nächste Session
- `docs/06-aufgaben/README.md` – §3.4 „Der nächste Prompt" (neue Konvention)

**Verifikation:**
- `python --version` → 3.12.10 (via `winget`, `--scope user`, **ohne Admin**)
- `python -m pytest custom_components/comfobox/tests -v` → **3 passed**
- `python -m ruff check custom_components/comfobox` → **All checks passed**
- `pwsh -File tools/check-links.ps1` → 84 Dateien, 0 Probleme

**Offen / nicht gemacht:**
- **Kein Docker-Daemon** → Container-Tasks weiterhin nicht verifizierbar.
- Die HA-Integration hat nur **3 Tests** (Config Flow). Coordinator, API-Client und
  die Plattform-Mappings (`sensor`/`number`/`select`/`switch`) sind **noch nicht**
  getestet – das ist der Rest von Aufgabe 8a.6.
- Die **Solution** (`src/ComfoBox.sln`) existiert noch nicht → Aufgabe 0.4.

**Überraschungen:**
- `pytest-socket` blockiert jede Socket-Erstellung. Home Assistants Test-Plugin tut das
  in `pytest_runtest_setup` – und **darum** scheitert unter Windows schon die
  Event-Loop-Erstellung (asyncio braucht unter Windows ein TCP-Socketpaar als
  Self-Pipe, unter Linux genügt ein AF_UNIX-Socket, den HA erlaubt).
  - **Fehlversuch 1:** Fixture `socket_enabled` → wirkungslos, weil `event_loop`
    **modulscoped** ist und die Fixture daher **vor** den function-scoped Fixtures
    entsteht.
  - **Fehlversuch 2:** `hookwrapper` → zu spät. Der Code hinter `yield` läuft erst
    nach dem kompletten Setup; der eingebaute `pytest_runtest_setup` von pytest baut
    aber genau dort die Fixtures auf.
  - **Lösung:** `pytest_socket.disable_socket` im conftest **ersetzen**. HA ruft es
    ueber das Modulattribut auf → der Patch greift unabhängig von der Hook-Reihenfolge.
    `connect()` bleibt auf `127.0.0.1` beschraenkt.
- `aiodns` (HA setzt den DNS-Resolver fest auf `AsyncResolver`) verlangt unter Windows
  einen `SelectorEventLoop`, HA nutzt dort den Proactor → `RuntimeError: aiodns needs
  a SelectorEventLoop on Windows`. **Lösung:** im Test `ThreadedResolver` statt
  `AsyncResolver` (`homeassistant.helpers.aiohttp_client.AsyncResolver`). Im
  **Produktivbetrieb** (Linux) bleibt aiodns unveraendert.
- **Drei .NET-10-Fallen reproduziert und in `phase-0.md` dokumentiert:**
  1. `dotnet new sln` erzeugt standardmäßig **`.slnx`** → `--format sln` ist Pflicht,
     sonst passen alle Build-Befehle der Spec nicht.
  2. Die Templates schreiben `Version="…"` in jedes `<PackageReference>` → mit
     `ManagePackageVersionsCentrally=true` bricht der Restore mit **NU1008**.
  3. `GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true` macht **CS1591**
     (fehlender XML-Kommentar) zum Fehler – die Template-Dateien `Class1.cs` /
     `UnitTest1.cs` müssen weg.

---

## 2026-10-31 – Phase 2 abgeschlossen: Domain & Gateway-Kern (Aufgaben 2.1–2.7)

**Ergebnis:** Die Domäne kennt jetzt alle **127 Items** mit allen Metadaten
(BACnet-ID, Typ, Einheit, Grenzen, Schreibbarkeit, Enum-Namen, **Priorität**).
Der Gateway startet, liest den BACnet-Bus, hält die Werte im Cache, schützt
das EEPROM mit einem **persistenten** 24-h-Zähler und protokolliert über
Serilog in einen In-Memory-Ringpuffer. **143 Tests grün** (45 BACnet + 30 Domain
+ 10 Contract + 58 Gateway). Die Golden-Fixture und `docs/topics.json` sind
**byteweise unverändert**.

**Geänderte Dateien (neu – Domain):**
- `src/ComfoBox.Domain/Items/ItemDefinition.cs` – das Item (Id, Gruppe, BACnet-ID, Kind, Min/Max, Einheit, ReadOnly, Expert-Modus, Priorität, Enum-Werte)
- `src/ComfoBox.Domain/Items/ItemValueKind.cs` – `Analog` / `Enum` / `Date`
- `src/ComfoBox.Domain/Items/RefreshPriority.cs` – `None` / `Once` / `Primary` / `Secondary`
- `src/ComfoBox.Domain/Items/Catalog.cs` – **generiert** (127 Items)
- `src/ComfoBox.Domain/Items/ItemEnums.cs` – **generiert** (4 Enums, 70 Namen)
- `src/ComfoBox.Domain/Items/IItemCatalog.cs` + `ItemCatalog.cs` – Lookup nach Id/Topic-Pfad/Gruppe
- `src/ComfoBox.Domain/Values/ItemValue.cs`, `AnalogValue.cs`, `EnumValue.cs` – kultur-invariant

**Geänderte Dateien (neu – Gateway):**
- `src/ComfoBox.Gateway/Configuration/{Gateway,Bacnet,Mqtt,Logging}Options.cs` – Bindung + Validierung
- `src/ComfoBox.Gateway/Logging/{InMemoryLogSink,LogEntry,SecretRedactor}.cs` – Ringpuffer (1000) + Maskierung
- `src/ComfoBox.Gateway/ValueCache.cs` – letzter Wert + Zeitstempel, `IsStale`, SSE-Abonnenten
- `src/ComfoBox.Gateway/Health/ErrorCollector.cs` – Fehler-Senke (50)
- `src/ComfoBox.Gateway/Protection/WriteCounter.cs` – persistent, atomar, 24-h-Fenster
- `src/ComfoBox.Gateway/Bacnet/BacnetDevice.cs` – **ein** SemaphoreSlim, Priorisierung, Timeout je Item
- `src/ComfoBox.Gateway/Bacnet/BacnetPollingService.cs` + `PollPlan.cs` – Poll-Zyklen
- `src/ComfoBox.Bacnet/Bacnet/IBacnetClient.cs` – Testnaht (Gateway-Test gegen Attrapp)

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Program.cs` – Serilog, DI, Options-Validierung, `/api/health`
- `src/ComfoBox.Gateway/appsettings.json` – Abschnitte `Gateway`, `Bacnet`, `Mqtt`, `Logging`
- `src/Directory.Packages.props` – `Serilog.Extensions.Hosting` gepinnt
- `tools/generate-topics/generate-topics.mjs` – Prioritäten, Expert-Modus, Enums, `--catalog`, `--catalog-check`

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → **0 Warnungen, 0 Fehler**
- `dotnet test src/ComfoBox.sln -c Release` → **143 Tests grün, 0 Fehler**
- Golden-Fixture **byte-identisch**:
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B` (5.657 B)
- `docs/topics.json` **unverändert** (`git status` leer) –
  der Contract-Test vergleicht den Katalog Feld für Feld dagegen
- `node tools/generate-topics/generate-topics.mjs --check` → deckungsgleich (127 Items)
- `dotnet restore src/ComfoBox.sln --locked-mode` → grün (Lock-Dateien aktualisiert)
- **Smoke-Test:** Gateway gestartet, `/api/health` → `{"status":"online", …}`,
  Serilog schreibt JSON-Datei (`gateway-20261031.jsonl`) mit `SourceContext`

**Offen / nicht gemacht:**
- **Kein MQTT** – Phase 3. Die Werte liegen nur im Cache.
- **Keine REST-API außer `/api/health`** – Phase 4.
- Die **zwei Legacy-DateItems** (`States/Controller/InitialOperationDate`,
  `LastParameterChangeDate`) werden gelesen, ihr Wert bleibt aber leer
  (Befund A20). Ihr Item bleibt im Katalog, damit die Topics unverändert sind.
- Die **Item-Id** folgt der Regel camelCase → snake_case
  (`zone.set_point_normal`). Die Spec nennt im Beispiel `zone.setpoint_normal` –
  das ist nicht generisch herstellbar; die Spec-Beispiele wurden angepasst.
- Die **Enum-Beschreibungen** (z. B. „Keine Energieerzeugung") sind **nicht**
  im Katalog – nur die Namen. Für die UI (Phase 5) ggf. nachziehen.

**Überraschungen:**
- ⚠️ **Der Altbestand ist nicht einheitlich kodiert.** Die Item-Dateien
  (`Zone`, `States`, `Config`, `Tests`, `Warmwater`) sind **Windows-1252**,
  die Enum-Dateien und `Time.cs` dagegen **UTF-8**. Ein blindes CP1252-Lesen
  zerlegt „ä" (0xC3 0xA4) in zwei Zeichen – der Enum-Name wurde zu
  `rmequellenstoerung` statt `Wärmequellenstoerung`. Der Generator erkennt
  die Kodierung jetzt **automatisch** (gültiges UTF-8 → UTF-8, sonst CP1252).
- ⚠️ **`Interlocked.Read` gibt es nur für `long`.** Für den `int`-Schreiber
  muss `Volatile.Read(ref _writeIndex)` verwendet werden (Compile-Fehler
  „Konvertierung von `ref int` in `ref readonly long`").
- ⚠️ **Serilog 4 kennt am `LogEvent` keine `LoggerName`-Eigenschaft.** Die
  Logger-Kategorie steckt in der Property `SourceContext`.
- ⚠️ **`ScalarValue.ToString()` gibt Strings in Anführungszeichen zurück**
  (`"broker:1883"`). Für die Anzeige muss `ScalarValue.Value` genommen werden.
- ⚠️ **`CA1848` (LoggerMessage-Delegaten) und `CA1873` sind in der Solution
  aktiv** (`AnalysisLevel=latest-recommended`) und widersprechen dem in
  `csharp.instructions.md` festgelegten Stil. Sie werden im Gateway-Projekt
  per `NoWarn` unterdrückt – mit Begründung im csproj.
- ⚠️ **`CA1716` verbietet `Set` als Schnittstellenmember.** `IValueCache.Set`
  heißt jetzt `IValueCache.Update`.
- ⚠️ **`CA1001`**: eine Klasse mit `SemaphoreSlim` muss `IDisposable` sein –
  `BacnetDevice` implementiert es jetzt.
- ⚠️ **XML-Kommentare sind auf lokalen Funktionen in Top-Level-Statements
  nicht erlaubt** (CS1587) → normale `//`-Kommentare verwenden.
- ⚠️ **`using N;` importiert keine verschachtelten Namespaces.** Aus
  `ComfoBox.Gateway.Tests` ist `Serialize.ASN1` nicht auflösbar – stattdessen
  `using ComfoBox.Bacnet.Serialize;` und `ASN1` direkt verwenden.
- ⚠️ **DataAnnotations validieren verschachtelte Objekte nicht.** Der
  `RingBuffer` muss separat geprüft werden.

---

## 2026-10-31 – Phase 4 abgeschlossen: REST/SSE-API (Aufgaben 4.1–4.9)

**Ergebnis:** Der Gateway hat die **komplette** REST-/SSE-Schnittstelle:
Items lesen und schreiben, Health/Status/Fehler, Live-Werte per SSE,
Konfiguration, Neustart, Setup-Assistent, API-Token und Log-Viewer.
**278 Tests grün** (45 BACnet + 30 Domain + 10 Contract + 193 Gateway).
Beide Fixtures sind **byteweise unverändert**. Die 21 Endpunkte stehen im
OpenAPI-Dokument (`/swagger`, nur im Development-Modus).

**Geänderte Dateien (neu – API):**
- `src/ComfoBox.Gateway/Api/GatewayApiExtensions.cs` – `AddGatewayApi()` (DI) + `MapGatewayApi()` (Routen) + `MapGatewaySwagger()`
- `src/ComfoBox.Gateway/Api/ItemsEndpoints.cs` – `GET /api/items`, `/api/items/{id}`, `/api/items/group/{group}`, `PUT /api/items/{id}` (204/400/403/409/422/502)
- `src/ComfoBox.Gateway/Api/StatusEndpoints.cs` – `/api/health`, `/api/status`, `/api/diagnostics/errors`, `/api/diagnostics/writes`
- `src/ComfoBox.Gateway/Api/EventsEndpoints.cs` – SSE `/api/events` (`snapshot` + `value`)
- `src/ComfoBox.Gateway/Api/ConfigEndpoints.cs` – `/api/config` (GET ohne Passwörter, PUT → `config.json`)
- `src/ComfoBox.Gateway/Api/ServiceEndpoints.cs` – `POST /api/service/restart` (202)
- `src/ComfoBox.Gateway/Api/SetupEndpoints.cs` – `/api/setup/*` (state, ports, Tests, config, apply)
- `src/ComfoBox.Gateway/Api/TokenEndpoints.cs` – `/api/security/tokens` (erzeugen/listen/widerrufen)
- `src/ComfoBox.Gateway/Api/LogEndpoints.cs` – `/api/diagnostics/logs` (Filter), `/stream` (SSE-Tail), `DELETE`
- `src/ComfoBox.Gateway/Api/Models/*.cs` – die DTOs (Feldnamen sind **Vertrag**)
- `src/ComfoBox.Gateway/Api/ApiJson.cs`, `ApiErrors.cs`, `SseWriter.cs` – JSON-Optionen, Fehlerkanal, SSE-Helfer

**Geänderte Dateien (neu – Sicherheit, Setup, Persistenz):**
- `src/ComfoBox.Gateway/Security/PasswordHasher.cs` – PBKDF2-HMAC-SHA256, 600.000 Iterationen
- `src/ComfoBox.Gateway/Security/ApiToken.cs` + `ApiSecurityStore.cs` – Token-Bestand in `config.json` (nur Hash), Setup-Flag, Administrator
- `src/ComfoBox.Gateway/Security/ApiAccessGuard.cs` – die Zugriffskontrolle (Bearer, Basic, LAN-Modus)
- `src/ComfoBox.Gateway/Setup/SetupService.cs` – Ports, BACnet-/MQTT-Test, Konfiguration schreiben
- `src/ComfoBox.Gateway/Configuration/ConfigStore.cs` – `config.json` (Modus 0600), atomar
- `src/ComfoBox.Gateway/Configuration/ApiOptions.cs` – `Api:RequireAuthentication` u. a.
- `src/ComfoBox.Gateway/Configuration/GatewayConfiguration.cs` – die **Präzedenz** der Konfigurationsquellen
- `src/ComfoBox.Gateway/Storage/AtomicFile.cs` – atomares Schreiben mit Retry (Windows-Sperre)
- `src/ComfoBox.Gateway/ServiceControl.cs` – Neustart in-process + Marker-Datei
- `src/ComfoBox.Gateway/Logging/LogStreamBroker.cs` – die Verteilung der Log-Einträge (max. 5 Streams)
- `src/ComfoBox.Gateway/RuntimeInfo.cs` – Startzeitpunkt und Uptime

**Geänderte Dateien (bearbeitet):**
- `src/ComfoBox.Gateway/Program.cs` – Konfigurationsquellen, `AddGatewayApi()`, `MapGatewayApi()`, Swagger nur im Development-Modus
- `src/ComfoBox.Gateway/GatewayServiceCollectionExtensions.cs` – `startBackgroundServices` (Testnaht), `LoggingOptions` als Singleton
- `src/ComfoBox.Gateway/Bacnet/BacnetDevice.cs` – `IsConnected` + `LastError` (für `/api/status`)
- `src/ComfoBox.Gateway/Configuration/GatewayOptions.cs` – `DeviceId`, `RestartEnabled`
- `src/ComfoBox.Gateway/Logging/InMemoryLogSink.cs` – Ereignis `EntryAdded` (Live-Tail)
- `src/ComfoBox.Gateway/Protection/WriteCounter.cs` – `AtomicFile` (Retry gegen die Windows-Dateisperre)
- `src/Directory.Packages.props` – Swashbuckle **10.3.1** (Microsoft.OpenApi 2.x, .NET 10-kompatibel)
- `src/ComfoBox.Gateway/ComfoBox.Gateway.csproj` – `Microsoft.AspNetCore.OpenApi` entfernt (Kollision mit Swashbuckle 9)

**Geänderte Dateien (neu – Tests, 59 Stück):**
- `src/ComfoBox.Gateway.Tests/Api/GatewayApiFixture.cs` – **echter** Kestrel-Testhost auf einem freien Port
- `src/ComfoBox.Gateway.Tests/Api/ItemsApiTests.cs` – 16 Tests (Schreibpfade, Statuscodes, A5/A6)
- `src/ComfoBox.Gateway.Tests/Api/StatusApiTests.cs` – 6 Tests (Feldnamen von `/api/status` **exakt**)
- `src/ComfoBox.Gateway.Tests/Api/EventsApiTests.cs` – 2 Tests (SSE `snapshot` + `value`)
- `src/ComfoBox.Gateway.Tests/Api/ConfigApiTests.cs` – 4 Tests (ohne Passwörter, Persistenz)
- `src/ComfoBox.Gateway.Tests/Api/ServiceApiTests.cs` – 3 Tests (202, Marker-Datei, deaktivierter Neustart)
- `src/ComfoBox.Gateway.Tests/Api/TokenApiTests.cs` – 7 Tests (einmaliger Klartext, nur Hash, 401, Widerruf)
- `src/ComfoBox.Gateway.Tests/Api/SetupApiTests.cs` – 9 Tests (PBKDF2, Apply, Schutz nach der Ersteinrichtung)
- `src/ComfoBox.Gateway.Tests/Api/LogApiTests.cs` – 8 Tests (Filter kombiniert, Limit, SSE-Tail, max. 5 Streams)

**Verifikation:**
- `dotnet build src/ComfoBox.sln -c Release` → grün, 0 Warnungen, 0 Fehler
- `dotnet test src/ComfoBox.sln -c Release` → **278 Tests grün**
- Golden-Fixture `bacnet-frames.json` → SHA256 `F95AA763…CE681B`, 5.657 Bytes (**unverändert**)
- `docs/topics.json` → SHA256 `67D7485B…C1463`, 33.845 Bytes (**unverändert**)
- `tools/check-links.ps1` → 617 Links, 0 Probleme
- **Smoke-Test** gegen den **laufenden** Gateway: `/api/health`, `/api/status`,
  `/api/setup/state`, `/api/items/{id}`, `/api/diagnostics/writes` → 200,
  `/swagger/v1/swagger.json` → 200 mit allen 21 Endpunkten

**Offen / nicht gemacht:**
- `POST /api/service/reconnect/bacnet` und `.../mqtt` (aus der Spec §3.1) fehlen
  noch – der Neustart deckt beide Fälle ab (Aufgabe 5.x nachziehen).
- `PUT /api/config` schreibt die Werte in die `config.json`; sie werden beim
  **nächsten Start** wirksam (die Options sind unveränderliche Schnappschüsse).
  Die Oberfläche bietet deshalb den Neustart an.
- `GET /api/items/{id}` liefert den **gecachten** Wert, keinen blockierenden
  BACnet-Read (der würde den Bus belegen). Der Wert kommt über `/api/events`.
- Die Web-UI, der Cookie-Login (5.10) und die Token-Verwaltung in der
  Oberfläche sind Phase 5 bzw. 8d.

**Überraschungen:**
- ⚠️ **Ein nicht registrierter Options-Typ hat JEDE Anfrage mit HTTP 500
  beantwortet.** `LoggingOptions` war nur über `IOptions<T>` gebunden, nicht
  als Singleton – die Minimal-API hat den Parameter deshalb für einen
  **Request-Body** gehalten: „Body was inferred but the method does not allow
  inferred body parameters". Der Fehler tritt **erst beim ersten Request** auf
  (die Endpunkte werden lazy gebaut) und sieht wie ein Serverabsturz aus.
  **Merksatz:** Jeder Typ, der als Parameter eines Minimal-API-Endpunkts
  auftaucht, muss als konkreter Dienst registriert sein.
- ⚠️ **`RemoveAll<IHostedService>()` zerstört die Pipeline des Webhosts.** Der
  `GenericWebHostService` (er startet Kestrel) ist selbst ein `IHostedService`.
  Nach dem Entfernen antwortete der Server mit einem leeren 500.
  **Merksatz:** Für Tests den Schalter `AddGatewayServices(..., startBackgroundServices:
  false)` benutzen – nicht `RemoveAll<IHostedService>()`.
- ⚠️ **Swashbuckle 9 kollidiert mit .NET 10.** `Microsoft.AspNetCore.OpenApi`
  10.0.14 braucht `Microsoft.OpenApi` 2.x, Swashbuckle 9 braucht 1.6.x. Umstieg
  auf Swashbuckle **10.3.1** und `Microsoft.AspNetCore.OpenApi` aus dem Projekt
  entfernt.
- ⚠️ **Ein SSE-Stream ohne Daten schickt keine Kopfzeilen.** Der Browser
  (und `HttpCompletionOption.ResponseHeadersRead`) wartet dann bis zum ersten
  Eintrag. `SseWriter.PrepareAsync` flusht die Kopfzeilen deshalb sofort.
- ⚠️ **`File.Move(…, overwrite: true)` scheitert unter Windows sporadisch**
  („Access to the path is denied") – ein Virenscanner sperrt die Datei für
  einen Moment. `AtomicFile.WriteAllText` wiederholt den Vorgang jetzt
  sechsmal (25 ms Abstand). Der Schreibzähler schrieb sonst bei 100 schnellen
  Writes einmal ins Leere.
- ⚠️ **PBKDF2 mit 600.000 Iterationen kostet ≈ 0,3 s.** Ein Token enthält
  deshalb seine Id im Klartext-Präfix (`cbk_<id>_<secret>`) – gesucht wird
  über die Id, verifiziert wird nur **ein** Hash. Das Verifikations-Ergebnis
  wird zusätzlich für kurze Zeit zwischengespeichert (bei jeder Änderung des
  Token-Bestands wird der Cache verworfen → ein widerrufener Token wirkt
  sofort nicht mehr).
- ⚠️ **`dotnet test` zeigt bei einem Fehler im Testhost keinen Body.** Ein
  leerer 500 ohne Exception-Text kostete Zeit. Abhilfe: den Testhost mit
  `dotnet test --logger "console;verbosity=detailed"` laufen lassen – dann
  steht die Server-Exception im Log (`fail: Microsoft.AspNetCore.Server.Kestrel[13]`).

---

## Nächste Session

**Aufgabe 5.1** – Angular-22-Projekt, `ng add @openng/optimus-ui`, Preset Aura
→ [phase-5.md](phase-5.md)
→ **Prompt:** [prompts/Prompt-Phase-5.md](prompts/Prompt-Phase-5.md)

