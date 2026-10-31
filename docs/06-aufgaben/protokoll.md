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

## Nächste Session

**Aufgabe 0.1** – `src/global.json` anlegen (SDK-Pinning 10.0.400)
→ [phase-0.md](phase-0.md)
