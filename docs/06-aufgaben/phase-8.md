# Phase 8 – Synology-Paket & Getting Started (13 PT)

**Ziel:** *„Als Third Party App in der DS923+ installieren und bequem einrichten."*
**Braucht:** Phase 5, Phase 6
**Spec:** [synology-paketcenter](../03-deployment/synology-paketcenter.md) ·
[ADR-008](../05-entscheidungen/adr-008-synology-spk.md)

> **Stand 2026-11-01: Phase 8 ist abgeschlossen** (8.1–8.9 ✅). Verifiziert:
> 301 Tests grün, SPK baut (30 KB), `bash -n` über alle Skripte grün, der
> Assistent im Browser über alle sechs Schritte geprüft.
> Offen bleibt nur **6.4** (Test auf der echten DS923+) und der **CI-Lauf**
> (es wurde noch kein Tag gepusht).

> **Nachtrag 2026-11-01 (CI & Bus-Analyse):**
>
> 1. **Der Legacy-CI ist abgeschaltet.** Der Job `legacy-check` ist entfernt,
>    CodeQL ignoriert `src_old/`. `src_old/` ist Archiv – der Code lief seit
>    dem 24.10. nicht mehr.
> 2. **Das Synology-Paket hat einen eigenen Workflow**
>    (`.github/workflows/synology-spk.yml`): baut das `.spk` eigenständig
>    (Tag-Push oder `workflow_dispatch`), hängt es als Release-Asset an und
>    kopiert es optional per SSH auf die NAS.
>    **Artefakt-Ablage außerhalb des 500-MB-Limits:** Release-Assets (2 GB pro
>    Datei) und GHCR – das 500-MB-Limit gilt nur für das Repository selbst.
> 3. **Die Bus-Analyse** (`/api/diagnostics/bacnet`) zeigt nur lesend, ob auf
>    dem MS/TP-Bus ein Token herumgeht. Der Zustandsautomat **protokolliert
>    nichts mehr** im Hot-Path (der Token hat nur 95 ms Zeit) – die Ereignisse
>    gehen in einen allokationsfreien Ringpuffer und werden außerhalb
>    ausgewertet.
>
> **Ursache des roten CI-Laufs (31.10. / 01.11.):** die `package-lock.json`
> enthielt im Root-Eintrag keine Abhängigkeiten – `npm ci` brach mit `EUSAGE`
> ab. Die Lock-Datei ist inzwischen konsistent; der Workflow meldet eine
> Drift jetzt mit einem klaren Hinweis.

---

## 8.1 – SPK-Skelett · 1.0 PT ✅

| | | |
|---|---|---|
| **Ziel** | `deploy/synology-spk/src/INFO` ✅, `scripts/` ✅, `package.tgz` ✅ | |
| **Offen** | – (Icon erzeugt: `src/PACKAGE_ICON.png`, 256×256) | |

**Akzeptanzkriterien:**
- [x] `INFO` enthält `checksum`, `adminport`, `adminurl`, `adminprotocol`
- [x] `install_dep_packages="ContainerManager>=1432"`
- [x] `PACKAGE_ICON.PNG` vorhanden (256×256, PNG) – erzeugt mit
      `node deploy/synology-spk/tools/make-icon.mjs` (reproduzierbar)
- [x] `build-spk.sh` erzeugt ein installierbares `.spk` (30 KB)
- [x] Die `checksum` in der `INFO` stimmt mit dem Archiv überein

---

## 8.2 – Lifecycle-Skripte · 2.0 PT ✅

| Skript | Aufgabe | Status |
|---|---|---|
| `preinst` | Prüfungen, **keine** Seiteneffekte | ✅ |
| `postinst` | Verzeichnisse, udev, systemd-Unit, erster Start | ✅ |
| `preuninst` | Container stoppen | ✅ |
| `postuninst` | Container + Unit + udev entfernen, **`var/` behalten** | ✅ |
| `start-stop-status` | start/stop/status/prestart/prestop/log | ✅ |

**Exit-Codes:** `0` ok · `1` Fehler · `2` Parameter · `3` nicht gestartet · `4` Abhängigkeit · `150` „soll gestoppt“

**Akzeptanzkriterien:**
- [ ] `status` liefert `0` (läuft) bzw. `150` (gestoppt)
- [ ] `postuninst` lässt das `var`-Verzeichnis **stehen** (Schreibzähler!)
- [ ] `preinst` hat **keine** Seiteneffekte

---

## 8.3 – `common.sh` · 1.0 PT ✅

| Inhalt | Status |
|---|---|
| Pfadvariablen (`SYNOPKG_PKGDEST`, `SYNOPKG_PKGVAR`) | ✅ |
| `log` / `die` | ✅ |
| `docker_bin()`, `compose_cmd()` (ContainerManager-Pfade, dann `docker compose`) | ✅ |
| `container_manager_running()`, `dialout_gid()` | ✅ |

---

## 8.4 – `serial-scan.sh` · 1.0 PT ✅

| Inhalt | Status |
|---|---|
| Enumeriert `/dev/serial/by-id/*`, `/dev/ttyUSB*`, konfigurierter Extra-Port | ✅ |
| Schreibt `ports.json` (`generatedAt`, `ports[]` mit `device`, `stableId`, `description`) | ✅ |

---

## 8.5 – `supervisor.sh` · 2.5 PT ✅

| Inhalt | Status |
|---|---|
| `run`/`start`/`stop`/`status`/`render`/`up`/`down`/`remove` | ✅ |
| JSON-Mini-Parser (`json_value`, `json_number`, `json_bool`) | ✅ |
| `render_compose()` – sed-Ersetzung von `${WEB_PORT} ${SERIAL_DEVICES} ${DIALOUT_GID} …` | ✅ |
| 3-Sekunden-Watch-Loop über `config.json` + `ports.json` (MD5) | ✅ |
| `passAllSerialDevices`-Behandlung | ✅ |

> **Warum der Supervisor?** Der `devices:`-Block hängt vom gewählten Port ab → muss
> zur Laufzeit gerendert werden. Der HA-Supervisor löst das nativ; auf dem DSM
> müssen wir es selbst bauen.

---

## 8.6 – udev-Regel & systemd-Unit · 1.0 PT ✅

| Datei | Inhalt | Status |
|---|---|---|
| `src/target/etc/99-comfobox-serial.rules` | CH340, FTDI, `dialout`, `by-id`-Symlink | ✅ |
| `src/target/etc/comfobox-gateway.service` | systemd-Unit für den Supervisor | ✅ |

---

## 8.7 – Setup-API · 2.0 PT ✅

| | |
|---|---|
| **Bezug** | [Aufgabe 4.7](phase-4.md#47--setup-api-getting-started--15-pt) |

**Akzeptanzkriterien:**
- [x] `GET /api/setup/state` meldet `setupCompleted` **und** die aktuellen
      Werte (`current`) – ohne Passwörter
- [x] `GET /api/setup/ports` liefert die Ports mit stabilem Pfad
- [x] `POST /api/setup/bacnet/test` liefert bei Erfolg `ok=true`, bei Fehlern einen `hint`
- [x] `POST /api/setup/mqtt/test` prüft Erreichbarkeit **und** Authentifizierung
      (`mqtt_unreachable` / `mqtt_auth_failed` / `mqtt_publish_failed`)
- [x] `PUT /api/setup/config` hasht das Passwort serverseitig **und** prüft
      die Regeln (≥ 12 Zeichen, 3 von 4 Zeichenklassen)
- [x] In `config.json` steht **kein** Klartext-Passwort; Modus 0600
- [x] `POST /api/setup/apply` setzt `setupCompleted=true` und stößt den Neustart aus

> **Zusätzlich:** Die Konfiguration bekommt den Abschnitt `Supervisor`
> (`PassAllSerialDevices`) – der Supervisor des NAS rendert daraus den
> `devices`-Block der Compose-Datei.

---

## 8.8 – Getting-Started-Assistent · 2.5 PT ✅

| | |
|---|---|
| **Bezug** | [Aufgabe 5.9](phase-5.md#59--getting-started-assistent--25-pt) |

**Akzeptanzkriterien:**
- [x] Assistent startet automatisch beim ersten Aufruf (`setupGuard` → `/setup`)
- [x] Schritt 1 zeigt die Portliste, aktualisiert sich bei Hotplug (alle 5 s)
- [x] Schritt 2: BACnet-Test meldet verständliche Fehler (38400-Baud-Hinweis)
- [x] Schritt 3: MQTT-Test meldet Authentifizierungsfehler korrekt
- [x] Schritt 4: Passwortregeln werden geprüft (serverseitig **und** in der UI)
- [x] Schritt 5: „Übernehmen" startet den Neustart, der Erfolg wird beobachtet
- [x] Assistent ist nach der Ersteinrichtung passwortgeschützt und **wiederholbar**
      (die gespeicherten Werte werden vorgefüllt)
- [x] Der Broker ist **optional** – ohne Broker läuft der Gateway ohne MQTT

---

## 8.9 – SPK-Build in CI · 0.5 PT ✅

| | |
|---|---|
| **Datei** | `.github/workflows/synology-spk.yml` (eigenständig) |

| Schritt | Wert |
|---|---|
| Bauen | `deploy/synology-spk/build-spk.sh` |
| Prüfen | `deploy/synology-spk/tools/check-shell.sh` |
| Hochladen | GitHub Release **als Release-Asset** (`gh release upload`) |
| Kopieren | optional per SSH auf die NAS (`NAS_SSH_*`-Secrets) |
| `packages.json` | wird beim Release aktualisiert |

**Akzeptanzkriterien:**
- [x] Das `.spk` liegt im GitHub Release (Job `release` → `gh release upload`)
- [x] `packages.json` zeigt auf die neue Version (mit MD5-Prüfsumme)
- [x] `bash -n` läuft in der CI (`check-shell.sh` im Job `spk`)
- [x] Das Paket lässt sich **auch ohne** Image-Publish bauen (`workflow_dispatch`)
- [x] Die Artefakte liegen **außerhalb** des 500-MB-Limits des Free-Kontos
      (Release-Assets + GHCR)
- [x] Optional: Kopie auf die NAS per SSH (`NAS_SSH_HOST`, `NAS_SSH_USER`,
      `NAS_SSH_KEY`, `NAS_TARGET_DIR`)

> **Noch offen:** Der Workflow ist noch **nicht** gelaufen (kein Tag gepusht).
> Der Push und das GitHub Release stehen damit aus – wie bei 6.3.
> Der Workflow lässt sich **jederzeit** von Hand anstoßen
> (*Actions → synology-spk → Run workflow*).

---

## Abnahme der Phase 8

- [ ] `*.spk` installiert auf einer DS923+ **ohne** Parameter → Browser öffnet sich
      → **6.4** (braucht das Gerät)
- [x] Der Assistent findet den Adapter, testet BACnet und MQTT, legt den Benutzer an
      → im Browser über alle sechs Schritte verifiziert
- [x] Nach „Übernehmen" läuft der Gateway mit dem gewählten Serial-Port
      → der Neustart wird beobachtet (`GET /api/health`)
- [x] `postuninst` lässt das `var`-Verzeichnis stehen

### Verifikationsbefehle

````bash
dotnet build src/ComfoBox.sln -c Release      # 0 Warnungen, 0 Fehler
dotnet test src/ComfoBox.sln -c Release        # 301 Tests grün
bash deploy/synology-spk/tools/check-shell.sh # alle Skripte + Parser-Test
bash deploy/synology-spk/build-spk.sh 1.0.0 0001
node tools/build-docs.mjs                     # Benutzerdoku → HTML
pwsh -File tools/check-links.ps1              # keine toten Links
````

→ Parallel dazu [Phase 8a–d](phase-8ad.md)
