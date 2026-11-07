# Changelog – ComfoBox Gateway Add-on

## 1.0.1

- Image `ghcr.io/rf77/comfobox-gateway:1.0.1` (multi-arch: `amd64`, `aarch64`)
- Das Synology-Paket dieser Version enthält die Rechte-Datei `conf/privilege`
  und lässt sich damit auf DSM 7 installieren

## 1.0.0

- Erstveröffentlichung
- Image `ghcr.io/rf77/comfobox-gateway:1.0.0` (multi-arch: `amd64`, `aarch64`)
- Serieller Port per Supervisor-Dropdown (`device(subsystem=tty)`)
- Web-UI über Ingress (Panel in Home Assistant)
- REST/SSE auf Host-Port 8099
- Optionaler MQTT-Broker (`mqtt:want`)
