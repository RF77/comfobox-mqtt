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

---

## Nächste Session

**Aufgabe 0.1** – `src/global.json` anlegen (SDK-Pinning 10.0.400)
→ [phase-0.md](phase-0.md)
