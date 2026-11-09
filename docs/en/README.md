# ComfoBox Gateway — Documentation

> [!WARNING]
> **Pre-release.** The new gateway has never successfully communicated with a real ComfoBox.
> It is not released. See [Status](#status).

## Status

| | |
|---|---|
| Maturity | development preview, **never verified against a physical ComfoBox** |
| Stable release | none for the new version |
| Last production-grade code | [`src_old/`](../../src_old/) (.NET Framework, Mono, Windows service) |
| Supported platforms | Linux `x86_64` / `aarch64`, Docker; Synology DSM 7; Raspberry Pi (64-bit) |

Everything in this folder describes **intended** behaviour. If your unit stays silent, that is
an expected outcome at this stage — see [Troubleshooting](troubleshooting.md) and open an issue.

## Pick your path

| Situation | Guide |
|---|---|
| Raspberry Pi (3/4/5), one command, autostart | [Install on a Raspberry Pi](install-raspberry-pi.md) |
| Synology NAS with DSM 7 | [Install on Synology](install-synology.md) |
| Any Linux host with Docker | [Install with Docker Compose](install-docker.md) |
| Home Assistant entities | [Home Assistant](home-assistant.md) |
| Protect the UI, create a token | [Security and tokens](security.md) |
| Script against the gateway | [REST API](rest-api.md) |
| Nothing works | [Troubleshooting](troubleshooting.md) |
| How it is built | [Architecture](architecture.md) |

## The three different things called "Home Assistant integration"

1. **MQTT discovery** — the gateway publishes `homeassistant/…` discovery topics. Home Assistant
   creates the entities by itself. No custom integration needed. Requires a broker.
2. **HACS integration `comfobox`** — a REST client. You enter *host* and *API token*, and it polls
   the gateway directly. No broker needed.
3. **Home Assistant add-on** — runs the gateway container inside Home Assistant OS / Supervised.
   Only available on those installations, not on a plain Raspberry Pi OS.

You can use 1 and 2 at the same time, but you should not: two sources for the same entity cause
duplicates. Pick one.

## Ports and paths

| What | Value |
|---|---|
| Web UI and REST API | container `8080`, published as `8099` by the installers |
| Data directory in the container | `/var/lib/comfobox` (volume `comfobox-data`) |
| Configuration file | `/var/lib/comfobox/config.json` |
| Logs | `/var/lib/comfobox/logs` |
| MQTT base topic | `ComfoBox` |
| Serial baud rate | `38400` (the ComfoBox must be reconfigured from 76800) |

## Serial adapter

Any USB RS485 adapter with a **CH340** or **FTDI** chip works on Linux, because both drivers are
in the mainline kernel. What does *not* work:

* a NAS whose kernel exports **no** USB serial driver at all (some Synology models);
* an adapter behind **two** USB hubs (the device re-enumerates constantly);
* a 32-bit Raspberry Pi OS (`armv7l`).

Check the adapter with the bundled diagnostic script:

```bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/synology-spk/tools/usb-serial-check.sh | sudo sh
```

It prints whether a driver exists, which device node appeared, and how deep the adapter sits in
the USB tree.
