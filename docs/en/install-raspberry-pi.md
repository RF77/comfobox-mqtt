# Install on a Raspberry Pi

One command, no configuration files to edit, autostart included.

> [!WARNING]
> Pre-release software — see [Status](README.md#status).

## Requirements

| | |
|---|---|
| Hardware | Raspberry Pi 3, 4, 5 or Zero 2 W — **or** any other 64-bit Linux host |
| Operating system | Raspberry Pi OS **64-bit** (`aarch64`), Debian or Ubuntu |
| Architecture check | `uname -m` must print `aarch64` or `arm64` |
| Power supply | the official PSU; undervoltage causes USB dropouts |
| USB RS485 adapter | CH340 or FTDI, plugged **directly** into the Pi (no hub) |
| Cable | A/B to the ComfoBox controller |
| Network | the Pi needs internet for the first install |

### 32-bit is not supported

If `uname -m` prints `armv7l` or `armv6l`, you are running a 32-bit OS. There is no `armhf`
image, because .NET 10 dropped 32-bit ARM. Re-flash Raspberry Pi OS 64-bit.

## One-line install

```bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/pi/install.sh | sudo sh
```

### What the script does

1. Refuses to run on 32-bit and warns about low disk space.
2. Resolves the newest release tag (falls back to a built-in default).
3. Installs Docker via `get.docker.com` if it is missing and enables the service.
4. Finds `/dev/ttyUSB*` / `/dev/ttyACM*`. A missing adapter is **not** fatal.
5. Writes `/opt/comfobox/compose.yaml`.
6. Pulls the image (or loads a local `.tar.gz` with `--offline`).
7. Starts the container and waits up to three minutes for the health check.

### What it deliberately does not do

* it does **not** ask questions — the wizard in the web UI does that;
* it does **not** edit `config.json`;
* it does **not** pin a device path, so a replugged adapter keeps working.

## Options

```bash
# a specific version
curl -fsSL …/install.sh | sudo sh -s -- --version 1.0.4

# another web port
curl -fsSL …/install.sh | sudo sh -s -- --port 8100

# offline: image as .tar.gz on the Pi
curl -fsSL …/install.sh | sudo sh -s -- --offline /home/pi/comfobox-gateway-arm64.tar.gz

# remove container and volume (keeps nothing)
curl -fsSL …/install.sh | sudo sh -s -- --uninstall

# help
curl -fsSL …/install.sh | sudo sh -s -- --help
```

## First steps after the install

1. Open `http://<pi-address>:8099`. The **setup wizard** starts automatically.
2. **Serial port** — pick the port of the adapter. Leave "pass through all serial ports" enabled.
3. **BACnet** — baud rate **38400**, device ID usually `1`. Run the connection test.
4. **MQTT** — optional. Leave the host empty to skip.
5. **Access** — create the administrator. The password needs at least 12 characters and 3 of 4
   character classes.
6. **Done** — apply. The gateway restarts and publishes the values.

> The ComfoBox must be switched to 38400 baud and restarted. At the factory default of 76800
> it will not answer — this is the single most common cause of "no values".

Then create an API token for Home Assistant: see [Security](security.md).

## Operation

| Task | Command |
|---|---|
| Logs | `docker logs -f comfobox-gateway` |
| Restart | `docker restart comfobox-gateway` |
| Update | re-run the installer, or `docker compose -f /opt/comfobox/compose.yaml pull && docker compose -f /opt/comfobox/compose.yaml up -d` |
| Health | `curl -s http://localhost:8099/api/health` |
| Uninstall | `curl -fsSL …/install.sh \| sudo sh -s -- --uninstall` |

## Autostart

The generated compose file uses `restart: unless-stopped`. Docker starts the container after a
reboot as long as it was running before. Additionally, `install.sh` enables the `docker` systemd
unit, so Docker itself starts with the system.

## Why Docker on a Pi

* the same image runs on Synology, x86 servers and any ARM64 board;
* no Mono, no .NET runtime to install by hand;
* updates are a `pull` + `up -d`;
* the image contains the API **and** the web UI.

## Troubleshooting

| Symptom | Cause | Fix |
|---|---|---|
| `FEHLER: Dieses System ist 32-Bit …` | 32-bit OS | re-flash 64-bit |
| `permission denied` on `/dev/ttyUSB0` | user not in `dialout` | the container uses `group_add: ["20"]`; check with `ls -l /dev/ttyUSB0` |
| Port list is empty | no driver / no adapter | run the diagnostic script (see [docs index](README.md#serial-adapter)) |
| Container restarts in a loop | bad configuration | `docker logs comfobox-gateway` |
| Web UI unreachable | port already in use | reinstall with `--port` |

More: [Troubleshooting](troubleshooting.md).
