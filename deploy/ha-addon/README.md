# ComfoBox Gateway (Home-Assistant-Add-on)

MQTT-Gateway für die **Zehnder ComfoBox Series 5 (ELESTA)** – BACnet/MS-TP über einen
USB-RS485-Adapter.

Dieses Verzeichnis enthält das **Add-on-Manifest** für den Home-Assistant-Supervisor.
Es nutzt **dasselbe Image** wie die Docker- und Synology-Installationen.

## Installation

```
HACS → Add-on Store → ComfoBox Gateway → Installieren → Starten
```

Repository-URL:
`https://github.com/RF77/comfobox-mqtt/tree/main/deploy/ha-addon`

## Inhalt

| Datei | Zweck |
|---|---|
| `repository.yaml` | Add-on-Repository-Metadaten (im Wurzelverzeichnis des Repos nötig) |
| `comfobox/config.yaml` | Add-on-Manifest: Image, Optionen, Ports, Ingress, Watchdog |
| `comfobox/DOCS.md` | Dokumentation, die HA im Add-on-Panel anzeigt |
| `comfobox/CHANGELOG.md` | Versionshistorie |
| `comfobox/translations/{de,en}.yaml` | Beschriftungen der Options-Formular |

## Wichtige Punkte

| Punkt | Wert |
|---|---|
| Serieller Port | `device(subsystem=tty)` → Dropdown mit stabilen `by-id`-Pfaden |
| Web-UI | `ingress: true` → Panel in Home Assistant, kein zusätzlicher Port |
| Integration | REST/SSE auf Host-Port **8099** (`http://<ha-host>:8099`) |
| Broker | `mqtt:want` → optional; ohne Broker läuft das Gateway ohne MQTT |
| Image | `ghcr.io/rf77/comfobox-gateway` (multi-arch `amd64` + `aarch64`) |

> **Der Ingress-Pfad ist nicht für die HA-Integration geeignet** – er enthält ein
> rotierendes Token. Die Integration nutzt deshalb den Host-Port 8099.

## Optionen → Gateway-Konfiguration

Der Supervisor schreibt die validierten Optionen nach `/data/options.json`. Der Gateway
liest diese Datei optional und mappt sie auf seine Options-Objekte.
Details: [Installationsstufen §5.1](../../docs/03-deployment/installationsstufen.md#51-options--gateway-konfiguration)

## Verwandte Dokumente

- [HA-Integration (Spec)](../../docs/02-modernisierung/ha-integration.md)
- [Installationsstufen](../../docs/03-deployment/installationsstufen.md)
- [ADR-009](../../docs/05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md)
