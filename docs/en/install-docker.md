# Install with Docker Compose

For any Linux host with Docker — x86_64 or aarch64.

> [!WARNING]
> Pre-release software — see [Status](README.md#status).

## Image

```
ghcr.io/rf77/comfobox-gateway:<version>
```

Tags are the release versions (`1.0.4`, `1.0.5`, …). The image contains the API **and** the
built web UI — there is no separate nginx container.

Platforms: `linux/amd64`, `linux/arm64`. **No** `armhf` (32-bit ARM).

## compose.yaml

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
      - comfobox-data:/var/lib/comfobox
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

volumes:
  comfobox-data:
```

```bash
docker compose up -d
docker compose logs -f gateway
```

Then open `http://<host>:8099` — the setup wizard starts.

## The three lines people forget

```yaml
device_cgroup_rules:
  - "c 188:* rwm"
```

Without it the device node exists (thanks to `/dev:/dev`) but `open()` fails with `EPERM`.
Major `188` = `ttyUSB*`, major `166` = `ttyACM*`.

```yaml
group_add:
  - "20"
```

Needed when the node is `root:dialout` with mode `0660`. `20` is `dialout` on Debian and on
Synology DSM. Check the group ID on your host with `getent group dialout`.

```yaml
DOTNET_gcServer: "0"
```

Server GC reserves far more memory than a small gateway needs. On a NAS this is the difference
between 120 MB and 600 MB.

## Configuration reference

All settings use the `COMFOBOX_` prefix and `__` as the section separator
(`COMFOBOX_Gateway__DataDirectory` → `Gateway:DataDirectory`).

| Setting | Default | Meaning |
|---|---|---|
| `Gateway:BaseTopic` | `ComfoBox` | MQTT base topic — do not change for existing setups |
| `Gateway:DeviceId` | `comfobox1` | identifier for Home Assistant |
| `Gateway:PollIntervalSeconds` | `3600` | how often all items are polled |
| `Gateway:MaxWritesPer24h` | `200` | **EEPROM protection** — persistent counter |
| `Gateway:ExpertMode` | `false` | allows writing sensitive parameters |
| `Bacnet:Port` | `/dev/ttyUSB0` | serial device |
| `Bacnet:Baudrate` | `38400` | must match the ComfoBox |
| `Bacnet:DeviceId` | `1` | device ID of the controller |
| `Bacnet:ThisStation` | — | MS/TP station of the gateway |
| `Mqtt:Brokers:0:Host` | — | broker host; leave empty to disable MQTT |
| `Mqtt:Brokers:0:Port` | `1883` | broker port |
| `Mqtt:Brokers:0:UseTls` | `false` | TLS |
| `Mqtt:EnableDiscovery` | `true` | publish `homeassistant/…` discovery |
| `Api:RequireAuthentication` | `false` | force sign-in for UI and API |

The values changed in the web UI are written to `config.json` in the data directory and take
precedence over the environment.

## Without MQTT

Leave `Mqtt:Brokers:0:Host` empty. The gateway then only serves the web UI and the REST API.
No broker is required for the HACS integration.

## Next

* [Home Assistant](home-assistant.md)
* [Security and tokens](security.md)
* [REST API](rest-api.md)
