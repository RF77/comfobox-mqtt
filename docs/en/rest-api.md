# REST API

Base URL: `http://<host>:8099/api`. All bodies are UTF-8 JSON. Numbers are always formatted
culture-invariant (`.` as decimal separator).

> [!WARNING]
> Pre-release software — see [Status](README.md#status).

## Authentication

See [Security](security.md). Short version:

```bash
curl -H "Authorization: Bearer cbk_…" http://host:8099/api/status
```

## Error model

Every error has the same shape:

```json
{
  "error": "value_out_of_range",
  "message": "Der Wert liegt außerhalb des zulässigen Bereichs.",
  "itemId": "zone.set_point_normal",
  "min": 5,
  "max": 30
}
```

| Field | Meaning |
|---|---|
| `error` | **stable** machine-readable code — use this in programs |
| `message` | German text for humans; may change |
| `itemId` | affected item, when relevant |
| `min` / `max` | allowed range, when relevant |
| `writesLast24h` / `maxWritesPer24h` | write counter state, when relevant |
| `hint` | UI hint, e.g. `no_iam` |

Common codes:

| Code | HTTP | Meaning |
|---|---|---|
| `unauthorized` | 401 | no or invalid credential |
| `forbidden` | 403 | authenticated but not administrator |
| `read_only_token` | 403 | the token has scope `read` |
| `invalid_credentials` | 401 | wrong user or password |
| `weak_password` | 422 | password policy not met |
| `admin_missing` | 409 | sign-in cannot be enabled without an admin |
| `value_out_of_range` | 422 | value outside `min`/`max` |
| `expert_mode_required` | 403 | item needs the expert mode |
| `write_limit_reached` | 429 | 24 h write counter exhausted |
| `item_not_found` | 404 | unknown item id |
| `token_not_found` | 404 | unknown token id |

## Endpoints

### Status and health

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/status` | open | BACnet/MQTT state, version, write counter, uptime |
| `GET` | `/health` | open | health checks per component, `startedAt` |

```bash
curl -s http://host:8099/api/status | jq
```

### Items

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/items` | open | full catalogue with values |
| `GET` | `/items/{id}` | open | one item |
| `GET` | `/items/group/{group}` | open | all items of a group |
| `PUT` | `/items/{id}` | `control` | write a value |

```bash
# read
curl -s http://host:8099/api/items/zone.set_point_normal | jq

# write an analog value
curl -X PUT -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     -d '{"value": 21.5}' http://host:8099/api/items/zone.set_point_normal

# write an enum — by name, exactly as listed in enumValues
curl -X PUT -H "Authorization: Bearer $TOKEN" -H "Content-Type: application/json" \
     -d '{"value": "Automatic"}' http://host:8099/api/items/zone.operation_mode
```

Writing is protected by the **24 h write counter**. When the limit is reached the API answers
`429 write_limit_reached`; nothing is written. The counter is persistent and cannot be bypassed —
not by the UI, not by MQTT, not by the REST API.

### Configuration

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/config` | `admin` | current configuration (secrets masked) |
| `PUT` | `/config` | `admin` | change runtime values |

Runtime-changeable: `expertMode`, `pollIntervalSeconds`, `maxWritesPer24h`, `useMqttRetain`.
Everything else needs a restart.

### Tokens

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/security/tokens` | `admin` | list tokens (no hashes, no plaintext) |
| `POST` | `/security/tokens` | `admin` | create a token |
| `DELETE` | `/security/tokens/{id}` | `admin` | revoke a token |

```bash
# create a read-only token valid for 90 days
curl -X POST -H "Authorization: Bearer $ADMIN_TOKEN" -H "Content-Type: application/json" \
     -d '{"name":"grafana","scope":"read","expiresInDays":90}' \
     http://host:8099/api/security/tokens
```

```json
{
  "id": "9f3a1c0d4e5b6a7f",
  "name": "grafana",
  "token": "cbk_9f3a1c0d4e5b6a7f_Xq7tR2mN8vKpL0sYbWcZdEfGhIjKlMnOpQrStUvWxYz",
  "createdAt": "2026-11-09T09:00:00+00:00",
  "scope": "read",
  "expiresAt": "2027-02-07T09:00:00+00:00",
  "isAdmin": false
}
```

`token` is returned exactly once.

### Security settings

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/security/settings` | `admin` | `hasAdmin`, `adminUsername`, `requireAuthentication` |
| `PUT` | `/security/settings` | `admin` | enable/disable the sign-in requirement |
| `PUT` | `/security/admin` | `admin` | change username and/or password |

`PUT /security/admin` requires `currentPassword` as soon as an administrator exists.

### Session

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/auth/login` | sign in, sets the `cbk_session` cookie |
| `POST` | `/auth/logout` | end the session |
| `GET` | `/auth/session` | `authenticated`, `userName`, `requireAuthentication`, `setupCompleted` |

### Diagnostics

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/diagnostics/errors` | open | error history |
| `GET` | `/diagnostics/writes` | open | write counter |
| `GET` | `/diagnostics/bacnet` | open | MS/TP token ring analysis |
| `POST` | `/diagnostics/bacnet/observe` | `admin` | start an observation |
| `GET` | `/logs` | `admin` | log ring buffer |
| `GET` | `/logs/stream` | `admin` | live log (SSE) |

### Service

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `POST` | `/service/restart` | `admin` | restart the gateway |
| `POST` | `/service/bacnet/reconnect` | `admin` | rebuild the BACnet connection |
| `POST` | `/service/mqtt/reconnect` | `admin` | rebuild the MQTT connection |

### Serial port

| Method | Path | Auth | Purpose |
|---|---|---|---|
| `GET` | `/serial/ports` | `admin` | list serial devices |
| `POST` | `/serial/test` | `admin` | test a port (IAM round trip) |
| `PUT` | `/serial/port` | `admin` | switch the port — takes effect immediately |

## Example: a daily read with a shell script

```bash
#!/bin/sh
TOKEN="cbk_…"
HOST="http://192.168.1.50:8099"

curl -fsS -H "Authorization: Bearer $TOKEN" "$HOST/api/items" |
  jq -r '.[] | select(.writable == false) | "\(.id)=\(.valueText)"'
```

A read-only token is enough — and it cannot damage the EEPROM if the script has a bug.
