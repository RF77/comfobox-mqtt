# ComfoBox Gateway – Synology-Paket (SPK)

Dieses Verzeichnis enthält das **Synology-Paket (SPK)** für den ComfoBox-MQTT-Gateway.
Es ist die bequeme Installationsvariante für die Synology DS923+ (und alle anderen
DSM-NAS mit x86_64/ARM64 ab DSM 7.2.1).

Die vollständige Spezifikation steht in
[`docs/03-deployment/synology-paketcenter.md`](../../docs/03-deployment/synology-paketcenter.md).

---

## Was der Benutzer bekommt

```
Paketcenter → Installieren → Klick
  → Container Manager wird als Abhängigkeit eingerichtet
  → Container startet
  → Browser öffnet automatisch http://<NAS-IP>:8081/
  → Getting-Started-Assistent:
      1. Serielle Ports wählen
      2. BACnet testen (Baudrate, Geräte-ID)
      3. MQTT-Broker eintragen und testen
      4. Benutzer & Passwort für die Web-UI
      5. Fertig → Gateway läuft, Home Assistant entdeckt die Entitäten
```

## Bauen

```bash
./build-spk.sh              # → dist/ComfoBoxGateway_noarch-dsm7_1.0.0-0001.spk
./build-spk.sh 1.1.0 0002  # → dist/ComfoBoxGateway_noarch-dsm7_1.1.0-0002.spk
```

Voraussetzung: nur `tar`, `gzip`, `md5sum` – **kein** Docker, **kein** Synology-Toolkit.

## Installieren

| Weg | Schritte |
|---|---|
| **Manuell** | `.spk` auf das NAS kopieren → Paketcenter → Installieren → Manuelle Installation → Datei wählen |
| **Paketquelle** | `packages.json` aus dem GitHub-Release als Paketquelle in DSM eintragen → Paket erscheint in der Liste |

## Inhalt dieses Verzeichnisses

```
build-spk.sh                       Build-Skript
src/
├── INFO                            Paketmetadaten (adminport → Web-UI, Dependency → Container Manager)
├── scripts/
│   ├── preinst                     Prüfungen vor der Installation
│   ├── postinst                    Verzeichnisse, udev-Regel, systemd-Unit, erster Start
│   ├── preuninst                   Container stoppen
│   ├── postuninst                  Container entfernen, Daten behalten
│   └── start-stop-status           Lifecycle: start / stop / status / prestart / prestop
└── target/
    ├── bin/
    │   ├── supervisor.sh            Rendert compose.yaml, startet Container, überwacht config.json
    │   ├── serial-scan.sh           Enumeriert serielle Ports → ports.json
    │   └── common.sh                Gemeinsame Pfade & Docker-Erkennung
    ├── etc/
    │   ├── comfobox-gateway.service systemd-Unit für den Supervisor
    │   └── 99-comfobox-serial.rules udev-Regel (stabile Symlinks, dialout-Rechte)
    └── compose.template.yaml        Compose-Vorlage mit ${…}-Platzhaltern
```

## Architektur in einem Satz

> Der **Supervisor** auf dem NAS ist die einzige Instanz, die die Compose-Datei rendert –
> deshalb kann der Serial-Port zur Laufzeit gewechselt werden, ohne das Paket neu zu
> installieren. Der Container selbst bekommt **keinen** Docker-Socket und läuft unprivileged.

## Wichtige Pfade auf dem NAS

| Pfad | Inhalt |
|---|---|
| `/var/packages/ComfoBoxGateway/target/` | Programm (wird bei Update ersetzt) |
| `/var/packages/ComfoBoxGateway/var/` | `config.json`, `ports.json`, `compose.yaml`, `logs/`, `writes.json` — **persistent** |
| `/etc/udev/rules.d/99-comfobox-serial.rules` | udev-Regel |
| `/etc/systemd/system/comfobox-gateway.service` | systemd-Unit |

## Sicherheit

- Container läuft **unprivileged** (`USER comfobox`), kein `privileged`
- **Kein** Docker-Socket im Container
- Passwörter liegen ausschließlich in `config.json` (Modus 0600), **nicht** in Env-Variablen
  → erscheinen nicht in `docker inspect`
- Der Assistent öffnet **keine** Firewall-Regeln automatisch

## Bekannte Grenzen

| Grenze | Umgang |
|---|---|
| Erfordert DSM ≥ 7.2.1 | `os_min_ver="7.2-63134"` in der INFO |
| Container Manager muss installiert sein | `install_dep_packages` → DSM installiert ihn bei Bedarf |
| Die Web-UI spricht **HTTP** ohne TLS | im LAN unkritisch; für Zugriff von außen Reverse Proxy nutzen |
| Deinstallation löscht die Container, **nicht** die Daten | Wiederinstallation übernimmt die Konfiguration |

## ⚠️ Fallstrick: `bin/` im Root-`.gitignore`

Das Root-`.gitignore` enthält die Regel `[Bb]in/` und ignoriert damit **jeden** Ordner
namens `bin` — auch `src/target/bin/`. Damit würden die drei Supervisor-Skripte
**stillschweigend nicht versioniert** und das SPK wäre unvollständig.

**Gegenmaßnahme:** `deploy/synology-spk/.gitignore` enthält die Ausnahmen

````
!src/target/bin/
!src/target/bin/**
````

> Git wertet die tiefere `.gitignore` zuerst aus — die Ausnahme schlägt also die
> Root-Regel. Prüfen mit:
> ```bash
> git status --untracked-files=all -- deploy | grep 'target/bin'
> ```
> Fehlt eine der drei `.sh`-Dateien in der Ausgabe, ist die Ausnahme wieder verlorengegangen.
