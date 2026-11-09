# Security and API tokens

The gateway has two independent access controls:

1. **Sign-in for the web UI** — username and password, stored as a PBKDF2 hash.
2. **API tokens** — for programs (Home Assistant, scripts, Grafana, …), with a **scope** and an
   optional **expiry**.

Both are optional in the sense that the gateway starts in **LAN mode** (no sign-in). You can turn
the requirement on at any time.

> [!WARNING]
> Pre-release software — see [Status](README.md#status).

## Administrator account

### Created during setup

The setup wizard has a mandatory **Access** step: username (≥ 3 characters) and password
(≥ 12 characters, at least 3 of 4 character classes).

What is stored:

| Field | In `config.json` | Notes |
|---|---|---|
| `Api.AdminUsername` | plain text | the user name is not a secret |
| `Api.AdminPasswordHash` | `pbkdf2-sha256$600000$…` | PBKDF2-SHA256, 600,000 iterations, random salt |
| `Api.SetupCompleted` | `true` | onboarding is done |

The password is sent **once** in plain text and hashed immediately. It is never written to disk,
never logged, and never appears in the log ring buffer.

### Change it later

**Configuration → Security**:

* **Username** — prefilled, editable;
* **Current password** — required (a stolen session cookie is not enough);
* **New password** + repeat.

After a password change **all sessions are invalidated** — every signed-in browser has to sign in
again.

### Turn sign-in on or off

**Configuration → Security → "Sign-in required"**.

| State | Effect |
|---|---|
| **on** (recommended) | every `/api` call needs a session cookie, a Basic header or a Bearer token |
| **off** ("LAN mode") | anonymous read **and** write is possible — anyone on the network can control the heating |

Sign-in cannot be enabled without an administrator; the gateway rejects that with
`409 admin_missing` to prevent a lock-out.

The setting is persisted in `config.json` (`Api.RequireAuthentication`) and wins over the
environment variable, so the switch in the UI takes effect **immediately**, without a restart.

## API tokens

### Create one

**Configuration → API tokens**:

| Field | Meaning |
|---|---|
| **Name** | display name, e.g. `home-assistant` |
| **Scope** | `Read only`, `Read and control`, `Administrator (Advanced)` |
| **Valid for** | `Unlimited`, `30 days`, `90 days`, `365 days` |

Press **Create token**. The plaintext appears exactly **once**:

```
cbk_9f3a1c0d4e5b6a7f_Xq7tR2mN8vKpL0sYbWcZdEfGhIjKlMnOpQrStUvWxYz
```

Copy it now. Only the hash is stored, so it cannot be shown again.

### Scopes

| Scope | JSON value | Allowed |
|---|---|---|
| Read only | `read` | `GET /api/status`, `/api/items`, `/api/health`, `/api/diagnostics/*` |
| Read and control | `control` | additionally `PUT /api/items/{id}` — change set points and modes |
| Administrator | `admin` | additionally configuration, token management, restart ("advanced mode") |

Rules:

* the scope is the **outer** limit — it applies independently of the expert mode;
* a `read` token that tries to write gets **403** with `{"error": "read_only_token"}`, not 401;
  the client must not fall into a sign-in loop;
* the expert mode is an **additional** restriction for sensitive parameters, not a substitute.

### Expiry

`expiresInDays` is a number of days between `1` and `3650`. `0` or omitted means **unlimited**.

An expired token stops working **immediately** — the check happens on every request. The token is
**not** deleted automatically, so the UI can show it as *expired* (red badge) and you can revoke it
yourself.

### Revoke

Trash icon next to the token, or:

```bash
curl -X DELETE -H "Authorization: Bearer $TOKEN" \
  http://192.168.1.50:8099/api/security/tokens/<id>
```

Revocation is immediate: the verification cache is invalidated with every change to the token
store.

### Store tokens safely

* a token is a password — do not commit it;
* use **one token per consumer**, so a single revocation does not break everything;
* prefer `read` unless the consumer must write;
* give a token an expiry when it is only needed temporarily.

## Authentication methods on the wire

Three ways to authenticate a request:

```bash
# 1. API token (recommended for programs)
curl -H "Authorization: Bearer cbk_…" http://host:8099/api/items

# 2. Basic auth with the administrator (handy for quick tests)
curl -u admin:geheim123 http://host:8099/api/items

# 3. Session cookie (what the web UI uses)
curl -c cookies.txt -X POST -H "Content-Type: application/json" \
     -d '{"username":"admin","password":"geheim123"}' \
     http://host:8099/api/auth/login
curl -b cookies.txt http://host:8099/api/items
```

The session cookie is `HttpOnly` and `SameSite=Strict`, so JavaScript cannot read it and a
cross-site request does not carry it.

### Open endpoints

These are readable **without** credentials in LAN mode, and readable with **any** valid credential
when sign-in is on:

* `GET /api/status`, `GET /api/health`
* `GET /api/items`, `GET /api/items/{id}`
* `GET /api/diagnostics/errors`, `GET /api/diagnostics/writes`

`/api/setup/*` is open **only** while `SetupCompleted` is `false`. Afterwards it requires an
administrator, because it can overwrite the configuration — including the admin password.

## Why invalid credentials are rejected even on open endpoints

If you send an `Authorization` header and it is wrong, the gateway answers **401** — even for an
endpoint that would have been readable anonymously.

Reason: a Home Assistant integration with a wrong token would otherwise look healthy for months
and only fail on the first write. Rejecting early makes the mistake visible immediately.

No header → no complaint. That is the LAN mode.

## Audit and logging

* every failed authentication is logged with the source address, never with the secret;
* the in-memory log ring buffer (1000 entries) redacts secrets before storing them;
* tokens are never logged — not even at `Verbose`.

## Known gaps

* **Server messages are German.** The UI is translated to DE/EN/FR, but API error messages
  (`message` field) are German. The stable `error` code is language-neutral and meant for
  programmatic use.
* **No HTTPS.** The gateway serves plain HTTP. Put it behind a reverse proxy with TLS if it is
  reachable from outside your LAN — or do not expose it at all.
* **No rate limiting** on the login endpoint. Use a long password and keep the port in the LAN.
* **No per-IP lockout.** A wrong token simply answers 401.
