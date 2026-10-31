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

## Nächste Session

**Aufgabe 3.1** – MQTTnet-Client (`MqttConnectionFactory`, `MqttConnectionManager`, `MqttPublisher`)
→ [phase-3.md](phase-3.md)
→ **Prompt:** [prompts/Prompt-Phase-3.md](prompts/Prompt-Phase-3.md)

