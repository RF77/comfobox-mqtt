# Phase 8 – Synology-Paket & Getting Started (13 PT)

**Ziel:** *„Als Third Party App in der DS923+ installieren und bequem einrichten."*
**Braucht:** Phase 5, Phase 6
**Spec:** [synology-paketcenter](../03-deployment/synology-paketcenter.md) ·
[ADR-008](../05-entscheidungen/adr-008-synology-spk.md)

> **Der grüne Teil ist schon da:** Die Shell-Skripte liegen unter
> `deploy/synology-spk/` und sind `bash -n`-geprüft. Offen sind das **Icon**, die
> **Setup-API**, der **Assistent** und die **CI-Integration**.

---

## 8.1 – SPK-Skelett · 1.0 PT (🟡 teilweise fertig)

| | |
|---|---|
| **Ziel** | `deploy/synology-spk/src/INFO` ✅, `scripts/` ✅, `package.tgz` ✅ |
| **Offen** | **`PACKAGE_ICON.PNG`**, `src/PACKAGE_ICON.PNG` |

**Akzeptanzkriterien:**
- [ ] `INFO` enthält `checksum`, `adminport`, `adminurl`, `adminprotocol`
- [ ] `install_dep_packages="ContainerManager>=1432"`
- [ ] `PACKAGE_ICON.PNG` vorhanden (256×256, PNG) – **fehlt noch**
- [ ] `build-spk.sh` erzeugt ein installierbares `.spk`
- [ ] Die `checksum` in der `INFO` stimmt mit dem Archiv überein

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

## 8.7 – Setup-API · 2.0 PT ⬜

| | |
|---|---|
| **Bezug** | [Aufgabe 4.7](phase-4.md#47--setup-api-getting-started--15-pt) |

**Akzeptanzkriterien:**
- [ ] `GET /api/setup/state` meldet `setupCompleted`
- [ ] `GET /api/setup/ports` liefert die Ports mit stabilem Pfad
- [ ] `POST /api/setup/bacnet/test` liefert bei Erfolg `ok=true`, bei Fehlern einen `hint`
- [ ] `POST /api/setup/mqtt/test` prüft Erreichbarkeit **und** Authentifizierung
- [ ] `PUT /api/setup/config` hasht das Passwort serverseitig
- [ ] In `config.json` steht **kein** Klartext-Passwort; Modus 0600
- [ ] `POST /api/setup/apply` setzt `setupCompleted=true` und stößt den Neustart aus

---

## 8.8 – Getting-Started-Assistent · 2.5 PT ⬜

| | |
|---|---|
| **Bezug** | [Aufgabe 5.9](phase-5.md#59--getting-started-assistent--25-pt) |

**Akzeptanzkriterien:**
- [ ] Assistent startet automatisch beim ersten Aufruf
- [ ] Schritt 1 zeigt die Portliste, aktualisiert sich bei Hotplug
- [ ] Schritt 2: BACnet-Test meldet verständliche Fehler (38400-Baud-Hinweis)
- [ ] Schritt 3: MQTT-Test meldet Authentifizierungsfehler korrekt
- [ ] Schritt 4: Passwortregeln werden geprüft
- [ ] Schritt 5: „Übernehmen" startet den Neustart, der Erfolg wird beobachtet
- [ ] Assistent ist nach der Ersteinrichtung passwortgeschützt und **wiederholbar**

---

## 8.9 – SPK-Build in CI · 0.5 PT ⬜

| | |
|---|---|
| **Datei** | `.github/workflows/docker-publish.yml` (Ergänzung) |

| Schritt | Wert |
|---|---|
| Bauen | `deploy/synology-spk/build-spk.sh` |
| Prüfen | `deploy/synology-spk/tools/check-shell.sh` |
| Hochladen | GitHub Release als Artefakt |
| `packages.json` | wird beim Release aktualisiert |

**Akzeptanzkriterien:**
- [ ] Das `.spk` liegt im GitHub Release
- [ ] `packages.json` zeigt auf die neue Version
- [ ] `bash -n` läuft in der CI

---

## Abnahme der Phase 8

- [ ] `*.spk` installiert auf einer DS923+ **ohne** Parameter → Browser öffnet sich
- [ ] Der Assistent findet den Adapter, testet BACnet und MQTT, legt den Benutzer an
- [ ] Nach „Übernehmen" läuft der Gateway mit dem gewählten Serial-Port
- [ ] `postuninst` lässt das `var`-Verzeichnis stehen

→ Parallel dazu [Phase 8a–d](phase-8ad.md)
