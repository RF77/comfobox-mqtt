# Troubleshooting

> [!WARNING]
> Pre-release software — see [Status](README.md#status). "No values" is a *known* possible outcome
> at this stage, not necessarily a defect on your side.

## First: is the adapter even there?

Run the diagnostic script **on the host** (not in the container):

```bash
curl -fsSL https://raw.githubusercontent.com/RF77/comfobox-mqtt/master/deploy/synology-spk/tools/usb-serial-check.sh | sudo sh
```

It reports, in this order:

1. whether a **kernel driver** for USB serial exists at all (`ch341`, `ftdi_sio`, `usbserial`);
2. whether an adapter is **visible on the USB bus**;
3. how **deep** it sits in the USB hub tree;
4. whether the device node **exists** (`/dev/ttyUSB0`, `/dev/ttyACM0`, …);
5. whether the container may **open** it (cgroup rules);
6. the device node's owner, group and mode.

The three possible verdicts matter:

| Verdict | Meaning | Fix |
|---|---|---|
| no adapter | nothing on the bus | plug it in, try another cable/port |
| adapter, no driver | the kernel cannot drive it (**some NAS**) | use a Raspberry Pi; an FTDI adapter does not help |
| adapter + driver, open fails | permissions / cgroup | see [Permissions](#permissions) |

## The most common cause: 76800 baud

The ComfoBox ships with **76800 baud**. The gateway speaks **38400**.

At the wrong baud rate the unit is completely silent — no error frame, no NAK, nothing. The gateway
reports `no_iam` ("I am not there").

Fix:

1. reconfigure the ComfoBox to **38400 baud**;
2. **restart the ComfoBox** (the setting is only read at boot);
3. restart the gateway or press *Reconnect*.

## No values, but the bus is "connected"

| Check | Where |
|---|---|
| device ID of the controller | wizard step 2, default `1` |
| A/B wires | swapping A and B gives silence, not garbage |
| another MS/TP master on the bus | see [one master only](home-assistant.md#only-one-ms-tp-master) |
| the token ring | Diagnostics → **BACnet bus** → start an observation |

The **BACnet bus** tab shows whether an MS/TP token circulates. If nothing appears at all, the
gateway never receives the token — that is a wiring/baud/station problem, not an item problem.

## Permissions

`ls -l /dev/ttyUSB0` typically prints:

```
crw-rw---- 1 root dialout 188, 0 Nov  9 09:00 /dev/ttyUSB0
```

So the process needs to be in `dialout` (gid 20 on Debian and DSM).

* **Docker**: `group_add: ["20"]` **and** `device_cgroup_rules: ["c 188:* rwm"]`.
  `/dev:/dev` alone is not enough — it makes the node visible but does not allow `open()`.
* **Synology SPK**: DSM's worker strips the quotes from `group_add` and rejects the file.
  Use Container Manager instead of the SPK in that case.

## USB hubs

An adapter behind **two** cascaded hubs re-enumerates continuously. In `dmesg` you see:

```
usb 1-3.2.1: device descriptor read/64, error -71
usb 1-3.2.1: attempt power cycle
```

`-71` is `EPROTO`, `-32` is `EPIPE`. The device node flickers between existing and not existing.

**Always plug the adapter directly into the host.** A powered hub is better than a passive one,
but a direct port is best.

## Container

```bash
docker ps -a --filter name=comfobox-gateway
docker logs --tail 200 comfobox-gateway
curl -s http://localhost:8099/api/health | jq
```

| Symptom | Cause |
|---|---|
| `Restarting (1)` | configuration invalid — read the log |
| healthy, but the port list is empty | no serial device inside the container (`uart`/`devices`/`--device`) |
| `dubious ownership` / permission error on `config.json` | the volume belongs to another UID |
| web UI reachable, `/api/health` returns 503 | a component is degraded — check `checks` |

## HTTP status codes

| Code | Meaning | Fix |
|---|---|---|
| 401 `unauthorized` | no/invalid token, or expired | new token |
| 401 `invalid_credentials` | wrong user/password | — |
| 403 `forbidden` | not an administrator | token scope `admin` |
| 403 `read_only_token` | scope `read` cannot write | new token with `control` |
| 403 `expert_mode_required` | sensitive parameter | enable the expert mode |
| 409 `admin_missing` | sign-in without admin | create an admin first |
| 422 `value_out_of_range` | outside `min`/`max` | use a value in range |
| 429 `write_limit_reached` | 24 h counter exhausted | wait, or raise the limit deliberately |

## Home Assistant

| Symptom | Cause |
|---|---|
| `invalid_auth` | token wrong, expired or revoked |
| `cannot_connect` | host without port — use `http://host:8099` |
| entities `unavailable` | MQTT down or availability not retained |
| nothing discovered | `Mqtt:EnableDiscovery` false, or the discovery prefix differs |
| duplicated entities | MQTT discovery **and** the HACS integration active — pick one |

## Logs

The gateway logs to three places: console (container log), file (`/var/lib/comfobox/logs`) and an
in-memory ring buffer of 1000 entries shown in **Diagnostics → Log**.

Secrets are redacted (`••••`) before they reach the ring buffer.

## Nothing helps

Open an issue and include:

1. the output of `usb-serial-check.sh`;
2. `docker logs --tail 200 comfobox-gateway`;
3. Diagnostics → **BACnet bus** with a 15 s observation;
4. the exact ComfoBox model and controller firmware, if known.

Please remember: this version has never been verified against real hardware. A report with a
working setup is as valuable as a bug report.
