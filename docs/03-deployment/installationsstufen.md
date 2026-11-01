# Installationsstufen

> *„Die einen sollen das einfach normal selber das Image installieren können, oder eben
> zusätzlich in Synology."* → vier Stufen, **ein** Image.

## 1. Zweck

Es gibt **mehrere** Wege, das Gateway zu installieren – abhängig davon, was der Nutzer
betreibt und wie bequem es für ihn sein soll. Entscheidend ist:

> **Alle Stufen nutzen dasselbe Image.** Es gibt keinen zweiten Code-Pfad.

## 2. Die vier Stufen im Vergleich

| | **Stufe 1**<br>Docker Compose | **Stufe 2**<br>Synology SPK | **Stufe 3**<br>HA-Add-on | **Stufe 4**<br>HA-Integration |
|---|---|---|---|---|
| **Artefakt** | `compose.yaml` | `*.spk` | `config.yaml` (Add-on) | `custom_components/comfobox` |
| **Installation** | `docker compose up -d` | Paketcenter → *Installieren* | HACS → Add-on Store → *Installieren* | HACS → *Hinzufügen* |
| **Zielgruppe** | alle mit Docker | DS923+-Nutzer | HA-OS-/Supervised-Nutzer | alle HA-Nutzer |
| **MQTT-Broker nötig?** | nein (optional) | nein (optional) | nein (optional) | **nein** |
| **Serial-Port wählen** | von Hand in der YAML | **Assistent** | **Dropdown** (Supervisor) | – |
| **Web-UI** | `http://host:8081` | `http://nas:8081` | **Panel in HA** | – |
| **Aktualisierung** | `pull && up -d` | Paketcenter | Add-on-Store | HACS |
| **Einrichtung** | von Hand | **6-Schritte-Assistent** | Options-Formular | – |
| **Doku** | [containerisierung](containerisierung.md) | [synology-paketcenter](synology-paketcenter.md) | [ha-addon](../../deploy/ha-addon/README.md) | [ha-integration](../02-modernisierung/ha-integration.md) |

### Entscheidungsbaum

```
                    Wo soll das Gateway laufen?
                                │
        ┌───────────────────────┼───────────────────────┐
        │                       │                       │
   auf der DS923+          in Home Assistant        irgendwo mit Docker
        │                       │                       │
   ┌────┴────┐            ┌────┴────┐                   │
   │         │            │         │                   │
 will ich  will ich    will ich  will ich            Stufe 1
 SPK-      Compose-     Add-on    nur die
 Paket?    Datei?                  Integration?
   │         │           │         │
Stufe 2   Stufe 1     Stufe 3    Stufe 4
```

> **Die Stufen schließen sich nicht aus.** Ein Nutzer kann das Gateway als SPK auf der
> DS923+ betreiben **und** in HA die HACS-Integration einrichten (Stufe 2 + 4).
> Stufe 3 setzt voraus, dass HA auf HA OS läuft.

## 3. Stufe 1 – Docker Compose

**Für:** alle mit Docker (NAS, Raspberry Pi, Linux-Server, Proxmox).

````bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/main/deploy/compose/compose.yaml -o compose.yaml
docker compose up -d
docker compose logs -f gateway
````

| Aspekt | Wert |
|---|---|
| Image | `ghcr.io/rf77/comfobox-gateway:<tag>` |
| Port | `8081:8080` |
| Serieller Port | fest in der YAML (`/dev/serial/by-id/…`) |
| Konfiguration | `config.json` im Volume + Env-Override |
| Einrichtung | von Hand: `config.json` schreiben, Token anlegen |

> **Details:** [containerisierung.md](containerisierung.md)

## 4. Stufe 2 – Synology-Paket (SPK)

**Für:** DS923+-Besitzer, die kein Compose-Management wollen.

```
Paketcenter → Installieren → Klick
  → Container Manager als Abhängigkeit
  → Container startet
  → Browser öffnet automatisch http://<NAS-IP>:8081/
  → Getting-Started-Assistent (6 Schritte)
```

| Aspekt | Wert |
|---|---|
| Image | `ghcr.io/rf77/comfobox-gateway:<tag>` |
| Port | `8081` (`adminport` in der `INFO`) |
| Serieller Port | **Assistent** → Supervisor rendert `compose.yaml` neu |
| Konfiguration | `config.json` im `var`-Verzeichnis |
| Einrichtung | **Assistent** (Port, BACnet, MQTT, Benutzer, Passwort) |

> **Details:** [synology-paketcenter.md](synology-paketcenter.md),
> [ADR-008](../05-entscheidungen/adr-008-synology-spk.md)

## 5. Stufe 3 – Home-Assistant-Add-on

**Für:** HA-OS-/Supervised-Nutzer (Green, Pi, VM).

```
HACS → Add-on Store → ComfoBox Gateway → Installieren
  → Optionen im Add-on-Formular:
      serieller Port (Dropdown), Baudrate, Broker, Base-Topic
  → Web-UI erscheint als Panel in Home Assistant
```

| Aspekt | Wert |
|---|---|
| Image | **dasselbe** wie Stufe 1/2, Multi-Arch (`amd64` + `aarch64`) |
| Port | `8099` (Host) → `8080` (Container) |
| Serieller Port | `device(subsystem=tty)` → **Dropdown** des Supervisors |
| Konfiguration | `/data/options.json` des Supervisors → wird vom Gateway gelesen |
| Web-UI | **Ingress** → Panel in HA, kein zusätzlicher Port |
| Broker | `mqtt:want` → optional der interne Mosquitto von HA |
| Überwachung | `watchdog` → der Supervisor startet bei Hänger neu |

> **Details:** [ADR-009 §E2](../05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md#e2--ha-add-on-f2),
> `deploy/ha-addon/`

### 5.1 Options → Gateway-Konfiguration

Der HA-Supervisor schreibt die validierten Optionen nach `/data/options.json`. Der Gateway
liest diese Datei **optional** und mappt sie:

| Option | Schema | Gateway-Ziel |
|---|---|---|
| `device` | `device(subsystem=tty)` | `Bacnet:Port` |
| `baudrate` | `int(9600,115200)` | `Bacnet:Baudrate` |
| `base_topic` | `str` | `Gateway:BaseTopic` |
| `broker_host` | `str` | `Mqtt:Brokers:0:Host` |
| `broker_port` | `port` | `Mqtt:Brokers:0:Port` |
| `broker_username` | `str?` | `Mqtt:Brokers:0:Username` |
| `broker_password` | `password?` | `Mqtt:Brokers:0:Password` |
| `use_tls` | `bool?` | `Mqtt:Brokers:0:UseTls` |
| `enable_discovery` | `bool?` | `Mqtt:EnableDiscovery` |
| `log_level` | `list(…)` | `Logging:Level` |

> **Präzedenz:** Die Kette ist durchgängig aufsteigend:
> `Umgebungsvariablen` → `config.json` → `options.json`.
> Die `options.json` hat damit **Vorrang vor beiden**.
>
> **Warum auch vor den Umgebungsvariablen?** Das Image setzt
> `COMFOBOX_Bacnet__Port=/dev/ttyUSB0` als Vorgabe. Wäre die Umgebungsvariable
> stärker, wäre die Portwahl des Add-ons – der Kern des Add-ons – wirkungslos.
> Der Supervisor schreibt die Optionen bei jedem Start neu; ein Env-Override
> wäre stillschweigend wirkungslos.
>
> **Implementierung:** `ComfoBox.Gateway/Configuration/AddOnOptionsProvider.cs` –
> ein `IConfigurationProvider`, der `/data/options.json` einliest, sofern die Datei
> existiert. Der Pfad ist über `Gateway:AddOnOptionsPath` konfigurierbar
> (Standard: `/data/options.json`).
>
> **Leere Werte werden ignoriert:** Der Supervisor schreibt **alle** Optionen –
> auch die, die der Nutzer nicht angefasst hat (mit dem Standardwert). Ein leerer
> Broker-Host würde einen Broker **ohne Host** erzeugen; `ValidateOnStart` würde
> fehlschlagen und der Container würde nicht starten. Deshalb werden die
> Broker-Optionen nur übernommen, wenn ein Broker benannt ist – aus der Datei
> oder aus der `config.json`.

### 5.2 Der Broker im Add-on

| Variante | Konfiguration |
|---|---|
| **HA-eigener Mosquitto** | `broker_host: core-mosquitto`, `broker_port: 1883` – der Add-on erhält den Broker über den Supervisor-Dienst `mqtt:want` |
| **Externer Broker** | `broker_host: 192.168.1.10`, Credentials als Option |

> Ohne Broker läuft der Gateway **ohne MQTT** – die Werte sind dann nur über die REST-API
> bzw. die HA-Integration erreichbar. Das ist ein legitimer Betriebszustand.

## 6. Stufe 4 – Home-Assistant-Integration

**Für:** alle HA-Nutzer, die Entitäten **ohne** MQTT-Broker wollen.

```
HACS → Integration → ComfoBox → Hinzufügen
  → URL des Gateways + API-Token
  → Gerät „ComfoBox ELESTA" mit 131 Entitäten erscheint
```

| Aspekt | Wert |
|---|---|
| Transport | REST (+ SSE in v2) |
| Auth | **API-Token** (Bearer) |
| Broker | **nicht** erforderlich |
| Entitäten | 127 Items + 4 Sonder-Entitäten |

> **Details:** [ha-integration.md](../02-modernisierung/ha-integration.md),
> [ADR-009](../05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md)

## 7. Was ist wo identisch?

| Baustein | Unterschiede zwischen den Stufen |
|---|---|
| `.NET-Gateway` | **identisch** – gleiches Image, gleicher Code |
| `Angular-UI` | **identisch** – gleiches `wwwroot/` |
| Item-Katalog | **identisch** |
| Schreibschutz | **identisch** |
| MQTT-Vertrag | **identisch** (Topic, Payload, QoS, Retain) |
| Konfiguration | **anders**: Stufe 1 = `config.json`/Env, Stufe 2 = `config.json` + Assistent, Stufe 3 = `options.json` |
| Serial-Port | **anders**: Stufe 1 = YAML, Stufe 2 = Supervisor rendert, Stufe 3 = Supervisor-Dropdown |
| Ports | **anders**: 8081 (1/2), 8099 (3), via URL (4) |

> **Regel:** Was der Nutzer in HA sieht, ist in **allen** Stufen identisch – bis auf die
> Portnummer. Es gibt keine „Add-on-Version“ des Gateways.

## 8. Multi-Arch

| Stufe | benötigte Architektur |
|---|---|
| Stufe 1 (Compose) | `linux/amd64` (DS923+), optional `linux/arm64` (Pi 5) |
| Stufe 2 (SPK) | `linux/amd64` (SPK selbst ist `noarch`) |
| Stufe 3 (Add-on) | `linux/amd64` **und** `linux/arm64` **verpflichtend** |
| Stufe 4 (Integration) | architekturunabhängig (Python) |

> **Konsequenz:** Für Stufe 3 muss das Image **multi-arch** publiziert werden
> (`linux/amd64,linux/arm64`). Ein reines `amd64`-Image liefert auf einem Raspberry Pi 5
> einen `exec format error`.
> → Anpassung in [docker.instructions.md](../../.github/instructions/docker.instructions.md)
> und in der CI-Matrix.

## 9. Versions-Pinning & Upgrade

| Stufe | Pin | Update-Mechanismus |
|---|---|---|
| Stufe 1 | Tag in `compose.yaml` (z. B. `1.0.0`) | `docker compose pull && up -d` |
| Stufe 2 | `version` in der `INFO` | DSM-Paketcenter → *Aktualisieren* |
| Stufe 3 | `version` in `config.yaml` = Image-Tag | HACS/Add-on-Store → *Aktualisieren* |
| Stufe 4 | Tag des HACS-Integrations-Releases | HACS → *Aktualisieren* |

> **Faustregel:** In Produktion **niemals** `latest` – ein Rückroll auf eine konkrete
> Version muss immer möglich sein.

## 10. Aufwand

| Stufe | PT | In Phase |
|---|---|---|
| Stufe 1 (Compose) | 2 | 6 |
| Stufe 2 (SPK) | 13 | 8 |
| Stufe 3 (Add-on) | 3 | 8b |
| Stufe 4 (Integration) | 8 + 1 | 8a, 8d |
| Multi-Arch-Publish | 2 | 8c |
| **Summe** | **29** | |

## 11. Verwandte Dokumente

- [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md) – ein Image
- [ADR-008](../05-entscheidungen/adr-008-synology-spk.md) – SPK
- [ADR-009](../05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md) – Add-on + Integration
- [containerisierung.md](containerisierung.md) · [synology-paketcenter.md](synology-paketcenter.md)
- [ha-integration.md](../02-modernisierung/ha-integration.md)
