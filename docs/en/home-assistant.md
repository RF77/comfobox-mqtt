# Home Assistant

Two independent ways to get entities. Pick **one**.

| | MQTT discovery | HACS integration `comfobox` |
|---|---|---|
| Broker needed | yes | no |
| Transport | MQTT | REST polling |
| Entities | created automatically | created by the integration |
| Setup | nothing to install | install via HACS, enter host + token |
| Works with the gateway behind NAT | no | yes (needs the port reachable) |

Running both at the same time creates two entity sets for the same data. Pick one.

If you pick **B**, also switch MQTT off — no broker needed, smaller attack surface:

```json
{ "Mqtt": { "Enabled": false } }
```

or **Configuration → MQTT → MQTT enabled** off (takes effect after a restart). With MQTT off the
gateway still serves REST/SSE and the HA integration as before; see
[AI assistants](ai-integration.md#choose-the-safest-configuration).

---

## A — MQTT discovery

### Requirements

* a broker (Mosquitto add-on, or any MQTT broker);
* the gateway configured with that broker;
* the MQTT integration in Home Assistant connected to the same broker.

### Gateway configuration

Set the broker in the setup wizard, or in `config.json`:

```json
{
  "Mqtt": {
    "EnableDiscovery": true,
    "DiscoveryPrefix": "homeassistant",
    "Brokers": [
      { "Host": "192.168.1.10", "Port": 1883, "UseTls": false }
    ]
  }
}
```

Restart the gateway afterwards (`docker restart comfobox-gateway`).

### What appears

* one **device** per ComfoBox;
* **sensors** for the readable items;
* **numbers** / **selects** for the writable items;
* an **availability** topic (`ComfoBox/Connected`), so entities become `unavailable` when the
  gateway is down;
* a **restart button** under `ComfoBox/Command/Restart`.

The retained flag matters: with `useMqttRetain = false`, Home Assistant loses the values after a
restart until the next poll.

### Topic contract

Unchanged from the legacy client — see [the README](../../README.md#mqtt-compatibility) and
[`docs/topics.md`](../topics.md).

---

## B — HACS integration `comfobox`

The integration is in [`custom_components/comfobox`](../../custom_components/comfobox).

### Install

1. HACS → **Integrations** → three-dot menu → **Custom repositories**.
2. Add `https://github.com/RF77/comfobox-mqtt` as **Integration**.
3. Search for **ComfoBox** and install it.
4. Restart Home Assistant.

### Configure

1. **Settings → Devices & Services → Add integration → ComfoBox**.
2. Enter:
   * **host** — e.g. `http://192.168.1.50:8099` (include the port!);
   * **API token** — create it in the gateway UI under **Configuration → API tokens**.
3. The integration verifies the token against `/api/status` and `/api/items`.

### Which token scope

| You want | Scope |
|---|---|
| only sensors and the overview | **Read only** (`read`) |
| also change set points and modes | **Read and control** (`control`) |
| manage tokens / restart the gateway from HA | **Administrator** (`admin`) |

A `read` token is rejected with **403 `read_only_token`** as soon as a number or select entity tries
to write. That is intentional — the scope is the outer limit.

### Token expiry

A token can expire. When it does, the integration shows a **reauth** prompt: enter a new token,
no need to delete and re-add the integration.

### Push, not polling

Values are **pushed** over Server-Sent Events (`GET /api/events`): the integration reads the item
catalogue once at setup and then receives every change. That is why the manifest declares
`iot_class: local_push`.

The gateway **batches** the values of one poll cycle into a single `values` event: a cycle reads
the items one after another from the bus, so 35 (secondary cycle: 160) changes arrive within a
second and belong together. Without batching the coordinator would notify every entity once per
value — up to 25 000 state writes per secondary cycle.

Only the gateway status (BACnet/MQTT connection, write counter) is still polled — every 60 seconds
by default, configurable between 15 and 3600 seconds in the integration options.

The gateway itself polls the BACnet bus with `Gateway:PollIntervalSeconds` (default 60 s), so
asking for values more often than that cannot produce new data. Writing a value updates the cache
immediately and therefore shows up in Home Assistant right away.

---

## Add-on (Home Assistant OS / Supervised only)

`deploy/ha-addon/comfobox` packages the same container as a Supervisor add-on.

| Option | Meaning |
|---|---|
| `uart: true` | passes the serial devices into the container — **required** |
| `devices` | explicit device list, alternative to `uart` |
| `usb` | not needed for a serial adapter |
| `ingress` | web UI inside HA — but the ingress URL contains a rotating token, so integrations must use port `8099` |

The `device(subsystem=tty)` filter in the schema only builds the dropdown; it does **not** grant
access. Only `uart: true` (or `devices`) does.

> A plain Raspberry Pi OS install does **not** use the add-on. There, use
> [install-raspberry-pi.md](install-raspberry-pi.md).

---

## Only one MS-TP master

The BACnet MS/TP bus tolerates exactly **one** master per station address. Do not run the gateway
**and** a direct BACnet integration in parallel — neither the gateway nor Home Assistant will get
a stable token ring. Let the gateway be the only master and let HA talk to the gateway.

## Troubleshooting

| Symptom | Cause |
|---|---|
| entities exist but are `unavailable` | availability topic not retained, or MQTT disconnected |
| `invalid_auth` in the config flow | token wrong, expired or revoked |
| `cannot_connect` | host/port wrong — the port is part of the URL |
| values never change | the ComfoBox is silent. Check `ComfoBox/Connected` and the dashboard |
| numbers cannot be set | token scope is `read` |
