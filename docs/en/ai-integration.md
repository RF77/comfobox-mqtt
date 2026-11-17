# AI assistants (Home Assistant MCP and REST)

This page is written for **AI assistants and tool authors**. It describes the stable surface an
assistant can use to read and write ComfoBox values: entity IDs in Home Assistant, the REST
contract, and the safety limits it must respect.

> [!WARNING]
> Pre-release software — see [Status](README.md#status).

## Two channels, one gateway

| Channel | What the assistant sees | Requires |
|---|---|---|
| **Home Assistant MCP** | the entities and services of the HACS integration | HA MCP server |
| **REST API** | the full item catalogue with limits and enum names | API token |

Both channels talk to the **same** gateway. Use MCP for natural-language read/dashboard tasks;
use REST when the assistant needs the exact `min`/`max`/`enumValues` or the stable item id.

## Choose the safest configuration

| Situation | Recommendation |
|---|---|
| Assistant should only read | Create a token with scope `read` |
| Assistant may write setpoints | Create a separate token with scope `control` |
| No MQTT broker | Set `Mqtt:Enabled=false` (UI, `config.json`, `COMFOBOX_Mqtt__Enabled`, or an empty `broker_host` in the add-on) |

With MQTT disabled, the gateway opens **no** broker connection, publishes no topics, sends no
discovery and accepts **no** MQTT write commands. REST, SSE, the web UI and the HA integration
are unaffected. `GET /api/status` reports `mqtt.enabled = false`.

## REST contract

Base URL: `http://<gateway>:<port>/api` (containers publish `8099`; Docker/Synology often `8081`).

```http
GET /api/items
Authorization: Bearer cbk_…
```

Every item has this shape (field names are stable):

```json
{
  "id": "zone.set_point_normal",
  "group": "Zone",
  "name": "SetPointNormal",
  "displayName": "Raumsollwert normal",
  "description": "Raumsollwert Normalbetrieb der Zone",
  "kind": "analog",
  "value": 21.5,
  "valueText": "21,5 °C",
  "unit": "°C",
  "min": 5,
  "max": 30,
  "writable": true,
  "requiresExpertMode": false,
  "stateTopic": "ComfoBox/Zone/SetPointNormal",
  "setTopic": "ComfoBox/Zone/SetPointNormal/Set",
  "lastUpdated": "2026-11-15T10:22:41+00:00",
  "stale": false,
  "enumValues": {}
}
```

Rules for assistants:

| Rule | Why |
|---|---|
| Use `value` (number) for logic, never `valueText` | `valueText` is display text only |
| Use `id` for writes, never `displayName` | ids are stable; display names are localized |
| Use `min`/`max` from the item | the gateway rejects values outside them |
| Use the **exact** string from `enumValues` for enums | enum names are part of the contract |
| Treat `stale: true` as “do not control on this value” | the last read failed |
| Do not write `requiresExpertMode` items without explicit consent | sensitive parameters |
| Never guess limits or enum values | the gateway will reject the write |

Write one value:

```http
PUT /api/items/zone.set_point_normal
Content-Type: application/json
Authorization: Bearer cbk_…

{ "value": 21.5 }
```

Enum write — use the **name**, exactly as listed in `enumValues`:

```json
{ "value": "Automatic" }
```

Status codes: `204` written · `400` malformed · `401` bad token · `403` wrong scope or expert
mode required · `404` unknown item · `409` write limit reached · `422` out of range · `502`
device did not answer.

The **24 h write counter** is persistent and cannot be bypassed. When it is exhausted the API
answers `429 write_limit_reached`; nothing is written.

Error bodies always carry a stable `error` code:

```json
{
  "error": "value_out_of_range",
  "message": "Der Wert liegt außerhalb des zulässigen Bereichs.",
  "itemId": "zone.set_point_normal",
  "min": 5,
  "max": 30
}
```

Use `error`, not `message` (German, may change).

## Home Assistant entities

The HACS integration creates one device, **ComfoBox ELESTA**, and one entity per item:

| Item | Entity platform |
|---|---|
| read-only number or enum | `sensor` |
| writable number | `number` |
| writable enum | `select` |
| writable 0/1 | `switch` |
| gateway status | `binary_sensor` (`bacnet_connected`, `mqtt_connected`) |

Entity IDs are derived from the device name and the item display name, for example:

```text
number.comfobox_elesta_raumsollwert_normal
sensor.comfobox_elesta_aussentemperatur
select.comfobox_elesta_betriebsart
```

> **Do not derive entity IDs from names.** Read the actual registry in Home Assistant
> (**Settings → Devices & services → ComfoBox ELESTA**) or use the MCP server's entity list.
> Display names are German and may change; the entity registry is authoritative.

Services:

| Service | Fields | Purpose |
|---|---|---|
| `comfobox.set_value` | `entity_id`, `value` | write one value |
| `comfobox.read_all` | — | poll all items immediately |
| `comfobox.restart` | — | restart the gateway |

`comfobox.set_value` accepts a number or the exact enum name. Writes still pass through the
gateway's write protection.

## What an assistant should be told

1. Read first, then write — use the current value and the limits from the gateway.
2. Never write outside `min`/`max`.
3. Never invent enum names.
4. Do not write in tight loops; every write counts against the 24 h limit (default 200).
5. Do not write expert-mode items without explicit approval.
6. Do not act on `stale` values.

## Limits of this documentation

* The item catalogue is **data**, not a static list: `GET /api/items` is the source of truth.
  `docs/topics.json` is a test fixture, not an API reference.
* The OpenAPI/Swagger UI (`/swagger`) is available in the **Development** environment only.
  In production, use this page and `GET /api/items` as the contract.
* The REST API speaks JSON with invariant numbers (`.` decimal separator). The UI and
  `valueText` are localized and must not be parsed.
