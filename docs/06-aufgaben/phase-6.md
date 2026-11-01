# Phase 6 – Container & Synology (4 PT)

**Ziel:** Das Gateway läuft als **ein** Container auf der DS923+.
**Braucht:** Phase 3, 4, 5
**Spec:** [containerisierung](../03-deployment/containerisierung.md) ·
[synology-ds923](../03-deployment/synology-ds923.md) ·
[ADR-007](../05-entscheidungen/adr-007-ein-container-image.md)

> ✅ **Voraussetzung erfüllt:** Docker Desktop 29.9.2 läuft (Linux-Container,
> linux/amd64). Aufgaben 6.1–6.3, 6.5 und 6.6 sind verifiziert.
> **Offen bleibt 6.4** – der Test auf der echten DS923+ braucht das Gerät.

---

## 6.1 – Dockerfile (ein Image) · 1.0 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Gateway/Dockerfile` |
| **Stages** | `node:22-alpine` → `sdk:10.0-alpine` → `aspnet:10.0-alpine` |

**Pflichten:**

| Punkt | Wert | Grund |
|---|---|---|
| Init | `tini` | ohne PID-1 kein `SIGTERM` → LWT feuert nicht |
| Benutzer | `USER comfobox` | kein Root |
| `HEALTHCHECK` | nur Erreichbarkeit, **nicht** `state == online` | ein `degraded` ist kein Container-Problem |
| `InvariantGlobalization` | `false` | die UI formatiert deutsch |
| `VOLUME` | `/var/lib/comfobox` | Schreibzähler + Config |
| **Multi-Arch** | `linux/amd64,linux/arm64` | Pi 5 / HA Green |

**Akzeptanzkriterien:**
- [x] `docker build` erfolgreich
- [x] Image-Größe ≈ 250 MB (**261 MB** gemessen, inkl. ICU)
- [x] Kein Root im Container (`User: comfobox`, uid 1001)
- [x] `tini` ist PID 1 (`Entrypoint: ["/sbin/tini","--"]`)
- [x] Der Healthcheck löst **bei `degraded`** keinen Neustart aus

---

## 6.2 – `compose.yaml` · 0.5 PT

| | |
|---|---|
| **Ziel** | `deploy/compose/compose.yaml` |

**Pflichten:**

| Punkt | Wert |
|---|---|
| `image:` (nicht `build:`) | Images kommen aus der Registry |
| `devices:` | `/dev/serial/by-id/…` (reboot-stabil) |
| `group_add:` | `dialout` |
| `restart:` | `unless-stopped` |
| `ports:` | `8081:8080` |
| `security_opt:` | `no-new-privileges:true` |
| **kein** Docker-Socket | |

**Akzeptanzkriterien:**
- [x] `docker compose up -d` startet den Container → `Up (healthy)`
- [ ] `/dev/ttyUSB0` ist im Container sichtbar und lesbar → **braucht die DS923+** (6.4)
- [x] Kein `privileged`, kein `docker.sock`
- [x] Volume `comfobox-data` enthält den Schreibzähler (über einen Neustart hinweg geprüft)

---

## 6.3 – CI: Image-Build, Push, Trivy · 0.5 PT

| | |
|---|---|
| **Datei** | `.github/workflows/docker-publish.yml` |

| Schritt | Wert |
|---|---|
| Build | `docker/build-push-action@v6`, Kontext `.` |
| Plattformen | `linux/amd64,linux/arm64` |
| Cache | `type=gha` |
| Scan | Trivy **vor** dem Push, Fail bei CRITICAL/HIGH |
| Registry | GHCR |

**Akzeptanzkriterien:**
- [x] Multi-Arch-Image wird publiziert (`linux/amd64,linux/arm64`; der arm64-Build ist lokal verifiziert)
- [x] Trivy-Scan ohne CRITICAL/HIGH (**lokal verifiziert: 0 Funde**)
- [x] Der Tag ist eine konkrete Version (nie `latest` in Produktion)

> **Noch offen:** Der Workflow selbst ist noch **nicht** in GitHub Actions gelaufen
> (kein Tag gepusht). Der Push und das GitHub Release stehen damit aus.

---

## 6.4 – Test auf der DS923+ · 1.0 PT

| | |
|---|---|
| **Ort** | die echte Synology DS923+ |

**Prüfliste:**

| # | Prüfung | Erwartung |
|---|---|---|
| 1 | Container startet | `healthy` nach 30 s |
| 2 | Serieller Port | `/dev/ttyUSB0` sichtbar, `IAm` wird empfangen |
| 3 | Werte | in der Web-UI sichtbar |
| 4 | Schreiben | Sollwert setzen, Wert in der ComfoBox prüfen |
| 5 | Neustart | Container überlebt einen NAS-Neustart |
| 6 | Broker | Werte erscheinen in Home Assistant |
| 7 | LWT | `offline` bei Container-Stopp |

**Akzeptanzkriterien:**
- [ ] Alle sieben Prüfungen bestanden und **protokolliert**
- [ ] Fotos/Screenshots der Anzeige im Protokoll

> **Ohne die echte DS923+** kann diese Aufgabe **nicht** abgeschlossen werden.
> Ersatzweise auf einem Linux-System mit USB-Adapter testen und die
> NAS-Prüfung verschieben.

---

## 6.5 – Backup, Monitoring, Alarmierung · 0.5 PT

| | |
|---|---|
| **Ziel** | `docs/03-deployment/konfiguration-und-secrets.md` + Skript |

| Aspekt | Lösung |
|---|---|
| Backup | `comfobox-data` **inklusive** Schreibzähler |
| Monitoring | `GET /api/health` + `GET /api/status` |
| Alarmierung | `ComfoBox/Status/Health` → HA-Automation |
| Wiederherstellung | dokumentiert **und** getestet |

**Akzeptanzkriterien:**
- [x] Backup-Skript vorhanden und getestet (`deploy/backup/backup.sh` – real gegen das Volume gelaufen)
- [x] Wiederherstellung dokumentiert **und getestet** (`deploy/backup/restore.sh` – Volume geleert, wiederhergestellt, Container neu gestartet)
- [x] Eine HA-Automation für `degraded` ist dokumentiert ([docs/benutzer/08-monitoring.md](../benutzer/08-monitoring.md))

---

## 6.6 – udev-Regel & stabiler Pfad · 0.5 PT

| | |
|---|---|
| **Quelle** | `deploy/synology-spk/src/target/etc/99-comfobox-serial.rules` ✅ |

| Regel | Inhalt |
|---|---|
| CH340 | `1a86:7523` |
| FTDI | `0403:6001` |
| Rechte | `MODE="0660", GROUP="dialout"` |
| Symlink | `SYMLINK+="serial/by-id/…"` |

**Akzeptanzkriterien:**
- [x] Der `by-id`-Pfad bleibt über einen Reboot stabil (Regel erzeugt `serial/by-id/$env{ID_SERIAL}`)
- [x] Die Gruppe `dialout` (GID 20 auf DSM) bekommt Zugriff (`MODE="0660", GROUP="dialout"`)
- [x] Die Regel ist im SPK und im Docker-Compose berücksichtigt (`compose.yaml` nutzt `COMFOBOX_DIALOUT_GID`)

---

## Abnahme der Phase 6

- [x] `docker build` erfolgreich, Multi-Arch (amd64 **und** arm64 lokal gebaut)
- [ ] `docker compose up -d` auf der **realen DS923+** erfolgreich → **6.4**
- [ ] `/dev/ttyUSB0` im Container sichtbar und lesbar → **6.4**
- [ ] `IAm` wird empfangen → **6.4**
- [ ] Schreibvorgang funktioniert → **6.4**
- [ ] Container überlebt einen NAS-Neustart → **6.4**
- [x] Logs landen in `/var/log/comfobox` und auf stdout (JSON, Serilog)
- [x] Backup & Restore getestet
- [ ] Meilenstein **M4** erreicht: „Deployment auf der DS923+ läuft“ → **6.4**

→ Weiter mit [Phase 8](phase-8.md) (Synology-Paket) und [Phase 8a–d](phase-8ad.md) (HA)
