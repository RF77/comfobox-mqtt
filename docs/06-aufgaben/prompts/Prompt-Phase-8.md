---
title: Prompt-Phase-8
description: Kopierfertiger Session-Prompt für die Phase 8 (Synology-Paket & Assistent)
order: 8
---

# Prompt – Phase 8: Synology-Paket & Getting-Started-Assistent

> Kopierfähiger Prompt für die Session **nach** Phase 6.
>
> ⚠️ **Erst verwenden, wenn Phase 6 abgeschlossen ist.** Voraussetzung ist ein
> grüner `dotnet build src/ComfoBox.sln -c Release`, `dotnet test`
> (293 Tests grün) und ein **laufender Docker-Daemon**.

---

## Prompt (zum Kopieren)

````text
Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze die **Phase 8** des Aufgaben-Dokuments um: das Synology-Paket (SPK)
und den Getting-Started-Assistenten.

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md                     ← die Projektregeln
2. .github/instructions/docker.instructions.md        ← die Docker-/SPK-Regeln
3. .github/instructions/csharp.instructions.md        ← die C#-Regeln
4. .github/instructions/angular.instructions.md       ← die Angular/Optimus-Regeln
5. .github/instructions/mqtt.instructions.md          ← die MQTT-Regeln (Topic-Vertrag!)
6. docs/06-aufgaben/README.md                        ← wie eine Session abläuft
7. docs/06-aufgaben/status.md                        ← Stand nach Phase 6
8. docs/06-aufgaben/phase-8.md                       ← die Aufgaben 8.1 bis 8.9
9. docs/06-aufgaben/phase-6.md                       ← was aus Phase 6 schon da ist
10. docs/03-deployment/synology-paketcenter.md       ← die verbindliche SPK-Spec
11. docs/03-deployment/installationsstufen.md        ← die vier Installationswege
12. docs/03-deployment/containerisierung.md          ← Dockerfile + Compose (Ist-Stand!)
13. docs/05-entscheidungen/adr-008-synology-spk.md    ← SPK + Assistent
14. docs/02-modernisierung/datenmodell-und-api.md    ← die API für den Assistenten
15. docs/05-entscheidungen/adr-012-api-zugriffskontrolle.md ← Bearer/Basic/LAN-Modus

Ziel der Phase 8: Der Benutzer installiert das Paket im DSM-Paketcenter mit
**einem Klick**, der Browser öffnet sich, und ein Assistent führt ihn in
sechs Schritten durch Serial-Port → BACnet → MQTT → Sicherheit → fertig. Der
Supervisor auf dem Host rendert die Compose-Datei und ersetzt den Container,
wenn sich die Konfiguration ändert.

Stand: **Phase 6 ist bis auf Aufgabe 6.4 abgeschlossen** (6.1, 6.2, 6.3, 6.5,
6.6 ✅, 6.4 ⛔ – der Test auf der echten DS923+ braucht das Gerät). Aus Phase 5
ist 5.9 (Getting-Started-Assistent) noch offen. Das SPK-Skelett (8.1–8.6) steht
bereits; offen sind 8.7 (Setup-API), 8.8 (Assistent) und 8.9 (SPK-Build in CI).

Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- ✅ `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- ✅ `dotnet test src/ComfoBox.sln -c Release` → **293 Tests grün**
  (46 BACnet + 30 Domain + 10 Contract + 207 Gateway). Das ist die Messlatte:
  sie müssen **ohne** Änderung grün bleiben.
- ✅ Die Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  ist **eingefroren** (SHA256
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B`,
  5.657 Bytes). Nicht verändern, nicht neu erzeugen.
- ✅ `docs/topics.json` ist **unverändert** (SHA256
  `67D7485BF961B2B6D11BA5E788932056F073B564F52922D2DE8B34DFC10C1463`) und wird
  vom Contract-Test Feld für Feld gegen den Domain-Katalog geprüft.
- ✅ **Das Container-Image ist gebaut und verifiziert** (261 MB, ein Image für
  Gateway + UI, tini als PID 1, USER comfobox, Healthcheck, VOLUME
  /var/lib/comfobox). `deploy/compose/compose.yaml` ist die **manuelle**
  Compose-Datei; `deploy/synology-spk/src/target/compose.template.yaml` ist die
  **gerenderte** Vorlage für das SPK (Platzhalter ${IMAGE}, ${WEB_PORT},
  ${SERIAL_DEVICES}, ${DIALOUT_GID}, ${DATA_DIR}, ${LOG_DIR}).
- ✅ `deploy/synology-spk/src/target/bin/` enthält schon `common.sh`,
  `serial-scan.sh` (→ ports.json) und `supervisor.sh` (rendert die Compose-Datei,
  Watch-Loop auf config.json/ports.json). Die udev-Regel
  `src/target/etc/99-comfobox-serial.rules` ist da (CH340 1a86:7523, FTDI
  0403:6001, GROUP=dialout, MODE=0660, by-id-Symlink).
- ✅ Die **Setup-API** ist teilweise da: `src/ComfoBox.Gateway/Setup/SetupService.cs`
  (Ports, BACnet-Test, MQTT-Test, Config schreiben, PBKDF2-Hash) und
  `src/ComfoBox.Gateway/Api/SetupEndpoints.cs` (`/api/setup/state`,
  `/api/setup/ports`, `/api/setup/ports/suggest`, `/api/setup/bacnet/test`,
  `/api/setup/mqtt/test`, `/api/setup/config`, `/api/setup/apply`).
  **Prüfe zuerst, was davon wirklich fertig ist** – 8.7 ist laut status.md noch
  ⬜.
- ✅ Der Assistent hat im Frontend schon ein Gerüst:
  `src/ComfoBox.Web/src/app/features/setup/steps/` (welcome, serial, bacnet,
  mqtt, security, …) und die Routen `setup-routes`.
- ⚠️ **Der Container darf KEINEN Docker-Socket bekommen.** Der Supervisor läuft
  auf dem **Host** und rendert die Compose-Datei. `postuninst` darf das
  `var/`-Verzeichnis **nicht** löschen – sonst ist der Schreibzähler weg.
- ⚠️ **Lifecycle-Exit-Codes**: 0 ok · 1 Fehler · 2 Params · 3 nicht gestartet ·
  4 Abhängigkeit · **150 „soll gestoppt"** (wichtig für den Paketcenter-Status).
- ⚠️ **`devices:` ist zur Installzeit nicht bekannt** – der Supervisor rendert
  den Block aus der Portwahl im Assistenten.
- ⚠️ **Die `config.json` hat Vorrang vor Env-Variablen** (`GatewayConfiguration.
  AddGatewaySources`). Passwörter dürfen **nie** in `docker inspect` landen.
- ⚠️ **`src_old/` ist Archiv**: lesen ist erlaubt und erwünscht, ändern nie.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root.
- `create_file` **überschreibt keine vorhandene Datei** – bei "File already
  exists" die Datei erst löschen (`Remove-Item`) oder mit
  `replace_string_in_file` bearbeiten.
- `TreatWarningsAsErrors=true` – jeder Analyzer-Fehler stoppt den Build.

Fallstricke aus Phase 6 (bitte beachten – alle wurden teuer):
- ⚠️ **Laufzeit-Regeln in `.gitignore`/`.dockerignore` immer verankern.**
  Ein unbestimmtes `logs/` verschluckt den Angular-Quellordner
  `src/ComfoBox.Web/src/app/features/diagnostics/logs/` – der LogViewer war
  dadurch **nie im Repository** und der CI-Build wäre gebrochen. Richtig:
  `/ComfoBox.Gateway/logs/`.
- ⚠️ **Das Alpine-Basis-Image setzt
  `DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true`.** Ohne `icu-libs
  icu-data-full` und ohne explizites `false` bricht die deutsche Formatierung
  in der Oberfläche.
- ⚠️ **`--force-local` ist keine BusyBox-tar-Option** (Alpine, DSM) – und GNU
  tar liest `C:/…` als Remote-Host. Portable Form:
  `cd <verzeichnis> && tar xzf <basisname>`.
- ⚠️ **tar darf die Datei nicht einpacken, die es gerade schreibt** („file
  changed as we read it") → Archiv in ein **zweites** Verzeichnis.
- ⚠️ **Ein temporäres Host-Verzeichnis lässt sich unter Windows nicht in den
  Container mounten** → Archive per stdin/stdout übertragen, nicht per
  zweitem Mount.
- ⚠️ **`set -u` + `[ "${VAR:-14}" -gt 0 ]` → „unbound variable"**. Erst in eine
  eigene Variable kopieren.
- ⚠️ **Ein Lock-File, das unter Windows per `npm install` erzeugt wurde, ist
  nicht plattformrein** (`@emnapi/*` in zwei Versionen → `npm ci` schlägt
  fehl). Das Lock-File im Linux-Container neu erzeugen – dann gehen beide
  Plattformen.
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

Regeln für diese Session:
- Nur die Phase 8 umsetzen, nichts darüber hinaus.
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Die 293 grünen Tests aus Phase 6 müssen grün bleiben.
- **Der Topic-Vertrag ist unverhandelbar:** Topic-Adressen und Payload-Formate
  bleiben gültig. Enum-Namen nicht umbenennen.
- **Der Schreibzähler (24 h) ist heilig.** Kein Schreibpfad darf ihn umgehen –
  auch nicht der Assistent.
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
1. Führe alle Verifikationsbefehle aus phase-8.md aus und zeige das Ergebnis.
   Der wichtigste ist: `dotnet test src/ComfoBox.sln -c Release` ist grün, die
   Golden-Fixture ist byte-identisch (Prüfsumme vergleichen!),
   `bash -n` über alle SPK-Skripte ist grün und
   `node tools/build-docs.mjs` bricht nicht ab.
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 8a–8d:
   HA-Ökosystem), in derselben Form wie dieser Prompt – inklusive der
   Fallstricke, die in dieser Session aufgetreten sind.
````
