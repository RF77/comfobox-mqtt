Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze **Phase 8a–8d** des Aufgaben-Dokuments um: das Home-Assistant-Ökosystem
(HACS-Integration, Add-on, Multi-Arch-Release, Token-API).

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md                     ← die Projektregeln
2. .github/instructions/csharp.instructions.md         ← die C#-Regeln
3. .github/instructions/angular.instructions.md        ← die Angular/Optimus-Regeln
4. .github/instructions/mqtt.instructions.md           ← die MQTT-Regeln (Topic-Vertrag!)
5. .github/instructions/docker.instructions.md        ← die Docker-/Add-on-Regeln
6. docs/06-aufgaben/README.md                          ← wie eine Session abläuft
7. docs/06-aufgaben/status.md                          ← Stand nach Phase 8
8. docs/06-aufgaben/phase-8ad.md                       ← die Aufgaben 8a.1–8d.1
9. docs/06-aufgaben/protokoll.md                       ← die Fallstricke aus Phase 8
10. docs/02-modernisierung/ha-integration.md           ← die HA-Integration
11. docs/03-deployment/installationsstufen.md          ← die vier Installationswege
12. docs/05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md
13. docs/02-modernisierung/datenmodell-und-api.md      ← die API (inkl. /api/security/tokens)

Ziel der Phase 8a–8d: Die HA-Integration läuft ohne MQTT-Broker, das Add-on
installiert per Klick, das Image ist multi-arch publiziert, und die Token-API
ist in der Oberfläche bedienbar.

Stand: **Phase 8 ist abgeschlossen** (8.1–8.9 ✅). Aus Phase 6 ist **6.4**
(test auf der echten DS923+) blockiert. Aus Phase 8a–8d sind offen:
**8a.6** (HA-Tests: API-Client, Coordinator, Entity-Mapping), **8b.2**
(Options-Mapping `/data/options.json` → Gateway-Options), **8c.1**
(Multi-Arch-Publish + HACS-Release) und **8d.1** (Token-API in der Oberfläche).

Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- ✅ `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- ✅ `dotnet test src/ComfoBox.sln -c Release` → **301 Tests grün**
  (46 BACnet + 30 Domain + 10 Contract + 215 Gateway). Das ist die Messlatte:
  sie müssen **ohne** Änderung grün bleiben.
- ✅ Die Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  ist **eingefroren** (SHA256
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B`,
  5.657 Bytes). Nicht verändern, nicht neu erzeugen.
- ✅ `docs/topics.json` ist **unverändert** (SHA256
  `67D7485BF961B2B6D11BA5E788932056F073B564F52922D2DE8B34DFC10C1463`) und wird
  vom Contract-Test Feld für Feld gegen den Domain-Katalog geprüft.
- ✅ Das Container-Image ist gebaut und verifiziert (261 MB, ein Image für
  Gateway + UI, tini als PID 1, USER comfobox, Healthcheck, VOLUME
  /var/lib/comfobox). `deploy/compose/compose.yaml` ist die **manuelle**
  Compose-Datei; `deploy/synology-spk/src/target/compose.template.yaml` ist die
  **gerenderte** Vorlage für das SPK.
- ✅ Die **Setup-API** ist fertig: `src/ComfoBox.Gateway/Setup/SetupService.cs`
  und `src/ComfoBox.Gateway/Api/SetupEndpoints.cs`. Der Assistent ist
  **fertig** (sechs Schritte, Vorausfüllung, Broker optional, Neustart wird
  beobachtet). Das SPK ist **vollständig** inkl. Icon und CI-Job `spk`.
- ✅ Die **Token-API existiert bereits**: `src/ComfoBox.Gateway/Api/TokenEndpoints.cs`
  und `ApiSecurityStore.CreateToken` (Format `cbk_<id>_<secret>`, PBKDF2-Hash).
  Offen ist 8d.1 nur die **Anbindung an die Oberfläche** + Verifikation mit
  der HA-Integration.
- ✅ **Die HA-Integration ist weitgehend fertig**: `custom_components/comfobox/`
  (api.py, coordinator.py, sensor/number/select/switch/binary_sensor, services.py,
  config_flow.py, manifest.json, Übersetzungen). Die Config-Flow-Tests sind grün
  (3/3), `ruff` ist sauber.
- ⚠️ **Die HA-Integration spricht mit dem Gateway, nicht mit dem Bus.**
  Es gibt **nie** zwei gleichzeitige Master auf dem MS/TP-Bus.
- ⚠️ **Der 24-h-Schreibzähler ist heilig.** Kein Schreibpfad darf ihn umgehen –
  auch nicht die HA-Integration (sie schreibt immer über `PUT /api/items/{id}`).
- ⚠️ **Der Topic-Vertrag ist unverhandelbar:** Topic-Adressen und Payload-Formate
  bleiben gültig. Enum-Namen nicht umbenennen.
- ⚠️ **Der Container darf KEINEN Docker-Socket bekommen.**
- ⚠️ **`postuninst` darf das `var`-Verzeichnis nicht löschen** – sonst ist der
  Schreibzähler weg.
- ⚠️ **Lifecycle-Exit-Codes**: 0 ok · 1 Fehler · 2 Params · 3 nicht gestartet ·
  4 Abhängigkeit · **150 „soll gestoppt"**.
- ⚠️ **`devices:` ist zur Installzeit nicht bekannt** – der Supervisor rendert
  den Block aus der Portwahl im Assistenten.
- ⚠️ **Die `config.json` hat Vorrang vor Env-Variablen**
  (`GatewayConfiguration.AddGatewaySources`). Passwörter dürfen **nie** in
  `docker inspect` landen.
- ⚠️ **`src_old/` ist Archiv**: lesen ist erlaubt und erwünscht, ändern nie.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root.
- `create_file` **überschreibt keine vorhandene Datei** – bei "File already
  exists" die Datei erst löschen (`Remove-Item`) oder mit
  `replace_string_in_file` bearbeiten.
- `TreatWarningsAsErrors=true` – jeder Analyzer-Fehler stoppt den Build.

Fallstricke aus Phase 8 (bitte beachten – alle wurden teuer):
- ⚠️ **Das `config.json` des SPK folgt dem Schema der .NET-Konfiguration**
  (`Bacnet`, `Mqtt`, `Api`, `Gateway`, `Supervisor`) – **nicht** den
  Kleinbuchstaben-Abschnitten, die vorher in der Spec standen. Der Gateway
  bindet die Datei über `Microsoft.Extensions.Configuration` (case-insensitiv),
  aber ein **leerer** `Bacnet:Port` oder eine **leere** `Mqtt:Brokers`-Liste
  lässt `ValidateOnStart` fehlschlagen → der Container startet nicht →
  der Assistent ist nicht erreichbar. Der `postinst` schreibt deshalb **nur**
  `Api:SetupCompleted`.
- ⚠️ **MQTTnet 5 hat keine `PayloadSegment`-Property** (nur `get`) – der
  Rundlauf nutzt `ConvertPayloadToString()`. `SubscribeAsync` braucht
  `MqttClientSubscribeOptions` (mit `MqttClientSubscribeOptionsBuilder`),
  **kein** `MqttApplicationMessage`. Für einen Publish/Subscribe-Test in den
  Gateway-Tests gibt es `src/ComfoBox.Gateway.Tests/Api/FakeMqttBroker.cs`
  (CONNECT/CONNACK, SUBSCRIBE/SUBACK, PUBLISH mit Echo) – **wiederverwenden**,
  nicht neu bauen.
- ⚠️ **`takeUntilDestroyed()` ohne Argument in `ngOnInit` → NG0203.**
  `ngOnInit` ist kein Injektionskontext – der `DestroyRef` muss übergeben
  werden (`inject(DestroyRef)` im Feld).
- ⚠️ **Optimus UI hält eine per `@if` entfernte `p-message` im DOM stehen**
  (`p-message-leave-active`). In einem Hintergrund-Tab feuert `animationend`
  nie → die Meldung bleibt dauerhaft sichtbar. **Regel:** `p-message` nicht per
  `@if` entfernen, sondern den Schweregrad (`[severity]`) umschalten.
- ⚠️ **Der Browser-Tab im VS-Code-Browser ist nicht sichtbar** → Klicks
  (`page.mouse.click`, `locator.click()`) erzeugen **kein** DOM-Ereignis.
  Für UI-Tests dort `element.click()` per `page.evaluate` verwenden.
- ⚠️ **`pnpm`/`npm`-Skript `tar` liest `C:/…` als Remote-Host** – im SPK-Build
  immer `cd <dir> && tar …`.
- ⚠️ **Der Browser „klickte" nicht" – nicht in Panik verfallen:** Der
  Assistent funktioniert. `page.evaluate(() => el.click())` löst aus.
- ⚠️ **Laufzeit-Regeln in `.gitignore`/`.dockerignore` immer verankern.**
  Ein unbestimmtes `logs/` verschluckt den Angular-Quellordner
  `src/ComfoBox.Web/src/app/features/diagnostics/logs/`. Richtig:
  `/ComfoBox.Gateway/logs/`.
- ⚠️ **Das Alpine-Basis-Image setzt
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`.** Ohne `icu-libs
  icu-data-full` und ohne explizites `false` bricht die deutsche Formatierung.
- ⚠️ **`--force-local` ist keine BusyBox-tar-Option** (Alpine, DSM) – portable
  Form: `cd <verzeichnis> && tar xzf <basisname>`.
- ⚠️ **tar darf die Datei nicht einpacken, die es gerade schreibt** → Archiv in
  ein **zweites** Verzeichnis.
- ⚠️ **Ein temporäres Host-Verzeichnis lässt sich unter Windows nicht in den
  Container mounten** → Archive per stdin/stdout übertragen.
- ⚠️ **`set -u` + `[ "${VAR:-14}" -gt 0 ]` → „unbound variable"**. Erst in eine
  eigene Variable kopieren.
- ⚠️ **Ein Lock-File, das unter Windows per `npm install` erzeugt wurde, ist
  nicht plattformrein** (`@emnapi/*` in zwei Versionen → `npm ci` schlägt
  fehl). Das Lock-File im Linux-Container neu erzeugen.
- ⚠️ **`dotnet new sln` erzeugt `.slnx`** → `--format sln` ist Pflicht.
- ⚠️ **Templates schreiben `Version="…"` ins `<PackageReference>`** → mit
  Central Package Management bricht der Restore mit **NU1008**.
- ⚠️ **`GenerateDocumentationFile=true` + `TreatWarningsAsErrors=true`** macht
  **CS1591** zum Fehler → Template-Dateien `Class1.cs`/`UnitTest1.cs` löschen.
- ⚠️ **`RestorePackagesWithLockFile` + TFM nur in `Directory.Build.props`** →
  **NETSDK1013**. Das TFM muss **in der csproj** stehen.
- ⚠️ **Ein nicht registrierter Typ als Endpunkt-Parameter ⇒ HTTP 500.**
  Minimal API hält einen unauflösbaren Parameter für einen Request-Body.
- ⚠️ **Der Setup-Assistent schreibt `config.json` mit dem Passwort-Hash**
  (PBKDF2, 600.000 Iterationen). Niemals Klartext in Dateien oder Logs.
- ⚠️ **Python 3.12 ist installiert** (`winget --scope user`), die HA-Tests laufen
  grün. Unter Windows: `pytest-socket` muss im `conftest.py` entschärft
  werden (`pytest_socket.disable_socket`), und `aiodns` braucht einen
  `ThreadedResolver` (statt `AsyncResolver`) im Test.

Regeln für diese Session:
- Nur die Phase 8a–8d umsetzen, nichts darüber hinaus.
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Die 301 grünen Tests aus Phase 8 müssen grün bleiben.
- **Der Topic-Vertrag ist unverhandelbar:** Topic-Adressen und Payload-Formate
  bleiben gültig. Enum-Namen nicht umbenennen.
- **Der Schreibzähler (24 h) ist heilig.** Kein Schreibpfad darf ihn umgehen –
  auch nicht der Assistent und auch nicht die HA-Integration.
- **UI-Texte deutsch, Code-Bezeichner englisch.**
- Nach jeder Aufgabe: Status in docs/06-aufgaben/status.md setzen
  (⬜ offen / 🟡 in Arbeit / ✅ fertig und verifiziert / ⛔ blockiert).
- Am Ende: Eintrag in docs/06-aufgaben/protokoll.md (Datum, Aufgabe, Ergebnis,
  Verifikation, offene Punkte, Überraschungen).
- Wenn eine neue Entscheidung nötig ist: ADR anlegen und docs/README.md sowie
  docs/05-entscheidungen/README.md aktualisieren.
- Keine Secrets, keine Passwörter in Dateien oder Logs.
- Sprache: Code-Bezeichner englisch, Kommentare und Doku deutsch.

Am Ende der Session:
1. Führe alle Verifikationsbefehle aus phase-8ad.md aus und zeige das Ergebnis.
   Der wichtigste ist: `dotnet test src/ComfoBox.sln -c Release` ist grün, die
   Golden-Fixture ist byte-identisch (Prüfsumme vergleichen!),
   `pytest custom_components/comfobox/tests -v` und
   `ruff check custom_components/comfobox` sind grün und
   `node tools/build-docs.mjs` bricht nicht ab.
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 7:
   Parallelbetrieb & Umschaltung), in derselben Form wie dieser Prompt –
   inklusive der Fallstricke, die in dieser Session aufgetreten sind.
