# Install on a Synology NAS (DSM 7)

Two ways: the **SPK package** from the package center, or **Container Manager** with a compose file.

> [!WARNING]
> Pre-release software — see [Status](README.md#status).
>
> Also read [Known limitation: serial port on a NAS](#known-limitation-serial-port-on-a-nas).

## Which DSM versions

* DSM **7.x** (tested on a DS923+, DSM 7.4.1, Container Manager 24.0.2, Compose v2.20.1).
* DSM 6 is not supported — the SPK targets the DSM 7 package format.

## Option A — SPK package

### Install

1. Download `ComfoBoxGateway_noarch-dsm7_<version>.spk` from the
   [releases page](https://github.com/RF77/comfobox-mqtt/releases).
2. Package Center → **Manual Install** → choose the file.
3. Accept the disclaimer. The wizard asks for the HTTP port (default **8099**).
4. After the install: `http://<nas-address>:8099`.

### What the package does

| Step | Effect |
|---|---|
| `preinst` | creates the shared folder for the container data |
| `postinst` | starts a **host-side supervisor** that runs `docker compose up -d` and keeps it alive |
| `start` / `stop` | start and stop the container |
| `postuninst` | removes the container, **keeps** the data volume (the write counter lives there) |

The supervisor runs on the host because DSM does not give a container access to the Docker socket.
That is intentional — see [ADR-007](../05-entscheidungen/README.md).

### Common installer problem

If the package center reports `2201 fail compose up`, the compose file is invalid **after DSM
rewrote it**. DSM's `docker-project` worker (running as the package user) rewrites the file and
*strips quotes*. A quoted number such as `group_add: ["20"]` becomes the integer `20`, and the
parser rejects it. Background: `docs/03-deployment/synology-paketcenter.md` (German).

The shipped compose file avoids quoted numbers for this reason.

## Option B — Container Manager / compose

```yaml
services:
  gateway:
    image: ghcr.io/rf77/comfobox-gateway:1.0.4
    container_name: comfobox-gateway
    restart: unless-stopped
    ports:
      - "8099:8080/tcp"
    environment:
      COMFOBOX_Gateway__DataDirectory: /var/lib/comfobox
      COMFOBOX_Logging__Directory: /var/lib/comfobox/logs
      COMFOBOX_BACNET__Baudrate: "38400"
      ASPNETCORE_URLS: http://+:8080
      DOTNET_gcServer: "0"
    volumes:
      - /volume1/docker/comfobox:/var/lib/comfobox
      - /dev:/dev
    device_cgroup_rules:
      - "c 188:* rwm"
      - "c 166:* rwm"
    group_add:
      - "20"
    healthcheck:
      test: ["CMD", "curl", "-fsS", "http://127.0.0.1:8080/api/health"]
      interval: 30s
      timeout: 5s
      retries: 3
      start_period: 60s
    security_opt:
      - no-new-privileges:true
```

Notes:

* `device_cgroup_rules` is **required**. `/dev:/dev` only makes the nodes *visible*; opening them
  needs the cgroup rule. `188` is `ttyUSB*`, `166` is `ttyACM*`.
* `group_add: ["20"]` (`dialout`) is required when the device node is `root:dialout 0660`.
* `DOTNET_gcServer: "0"` keeps the memory footprint small on a NAS.

## Known limitation: serial port on a NAS

Some Synology kernels export **no USB serial driver at all** — neither `ch341` nor `ftdi_sio`,
sometimes not even `usbserial`. In that case no adapter can ever work, and no amount of
`modprobe` helps.

Then there is a second obstacle: the DSM package runs the container as the **package user**, which
is not a member of `dialout`, while `/dev/ttyUSB0` is `root:dialout` with mode `0660`. DSM's
worker strips the quotes from `group_add`, so the group cannot be passed reliably.

Practical consequence:

* if the adapter works but access is denied → use **Container Manager** (option B), where the
  compose file is not rewritten;
* if the kernel has no driver → use a **Raspberry Pi** instead. An FTDI adapter does not help.

Diagnose first:

```bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/synology-spk/tools/usb-serial-check.sh | sudo sh
```

The script distinguishes "no adapter", "no kernel driver" and "driver present but blocked".

## Data and backup

| What | Where |
|---|---|
| Configuration (`config.json`, hashed passwords, tokens) | `/volume1/docker/comfobox/config.json` |
| Write counter | in the same volume — losing it resets the EEPROM protection |
| Logs | `/volume1/docker/comfobox/logs` |

Back up that folder. Never delete it when reinstalling.

## Next steps

* First start: the setup wizard runs at `http://<nas>:8099`.
* Home Assistant: [Home Assistant](home-assistant.md).
* Token for scripts: [Security](security.md).
