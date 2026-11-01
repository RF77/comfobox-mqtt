# Phase 8a–8d – Home-Assistant-Ökosystem (14 PT)

**Ziel:** *„Alles in HA installierbar: Add-on + HACS-Plugin, Zugriff ohne
MQTT-Konfiguration, Gerät mit allen Entities."*
**Braucht:** Phase 4, Phase 6, Phase 8
**Spec:** [ha-integration](../02-modernisierung/ha-integration.md) ·
[installationsstufen](../03-deployment/installationsstufen.md) ·
[ADR-009](../05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md)

> **Der Kern steht schon** (Python-Module, Manifest, Tests). Offen sind die
> **Verifikation**, das **Options-Mapping** und der **Multi-Arch-Release**.

---

## 8a.1 – Manifest & Config Flow · 1.5 PT ✅

| | |
|---|---|
| **Ziel** | `custom_components/comfobox/manifest.json` ✅, `hacs.json` ✅, `config_flow.py` ✅ |

**Manifest-Pflichtfelder:** `domain`, `name`, `codeowners`, `config_flow`, `documentation`, `integration_type`, `iot_class`, `issue_tracker`, `version`

**Akzeptanzkriterien:**
- [ ] `hacs.json` vorhanden, HACS findet die Integration
- [ ] Config Flow mit Adresse + Token
- [ ] Fehler `cannot_connect` / `invalid_auth` verständlich
- [ ] Reauth-Flow bei abgelaufenem Token

---

## 8a.2 – API-Client · 1.0 PT ✅

| | |
|---|---|
| **Ziel** | `custom_components/comfobox/api.py` |

| Regel | Grund |
|---|---|
| `Authorization: Bearer <token>` | maschinen-tauglich |
| ein `aiohttp.ClientSession` | Performance-Regel von HA |
| Timeout 10 s / 30 s für Schreibvorgänge | BACnet-Timeout |
| **Token nie im Log** | Sicherheit |

**Akzeptanzkriterien:**
- [ ] `get_status`, `get_items`, `write_item`, `restart`
- [ ] Fehlerklassifizierung: `AuthenticationError`, `ConnectionFailed`
- [ ] Der Token erscheint **nirgends** in Logs oder Exceptions

---

## 8a.3 – Coordinator · 1.0 PT ✅

| | |
|---|---|
| **Ziel** | `custom_components/comfobox/coordinator.py` |

| Aspekt | Wert |
|---|---|
| Intervall | 10 s (konfigurierbar, Minimum 5 s) |
| Bei Ausfall | `UpdateFailed` → Entitäten `unavailable` (**kein** Flackern) |
| Bei 401 | `ConfigEntryAuthFailed` → HA fordert zur Neuauthentifizierung auf |

**Akzeptanzkriterien:**
- [ ] Status und Items werden gemeinsam aktualisiert
- [ ] Der Status liegt unter dem reservierten Schlüssel `__status__`
- [ ] `local_polling` in `iot_class` (v1) → später `local_push` mit SSE

---

## 8a.4 – Entitäten-Plattformen · 2.5 PT ✅

| | |
|---|---|
| **Ziel** | `sensor.py`, `number.py`, `select.py`, `switch.py`, `binary_sensor.py` |

| Item-Merkmal | Plattform |
|---|---|
| `isReadOnly=true`, Zahl | `sensor` |
| `isReadOnly=false`, Zahl | `number` mit **exaktem** Min/Max |
| `isReadOnly=false`, Enum | `select` mit den **exakten** Enum-Namen |
| `isReadOnly=false`, 0/1 | `switch` |
| Status | `binary_sensor` (BACnet, Broker) |

**Akzeptanzkriterien:**
- [ ] **Ein** Gerät „ComfoBox ELESTA“ (Hersteller, Modell, SW-Version, Konfigurations-URL)
- [ ] Alle 127 Items + 4 Sonder-Entitäten angelegt
- [ ] `unique_id` stabil und eindeutig (`{deviceId}_{itemId}`)
- [ ] `isStale` → Entität `unavailable`
- [ ] Ein Gerätekatalog-Test prüft die Zuordnung

---

## 8a.5 – Services & Übersetzungen · 0.5 PT ✅

| | |
|---|---|
| **Ziel** | `services.py`, `strings.json`, `translations/en.json` |

| Service | Zweck |
|---|---|
| `comfobox.set_value` | Wert schreiben |
| `comfobox.restart` | Gateway neu starten |
| `comfobox.read_all` | alle Werte sofort neu lesen |

**Akzeptanzkriterien:**
- [ ] Alle drei Services registriert
- [ ] `strings.json` + `translations/en.json` vollständig
- [ ] Deutsche Fehlertexte vorhanden

---

## 8a.6 – Tests · 1.5 PT ✅

| | |
|---|---|
| **Ziel** | `custom_components/comfobox/tests/` |
| **Stand** | **35 Tests grün** – Config Flow (3), API-Client (10), Coordinator (8), Entity-Mapping (12), Schreibpfade (2) |

**Ablauf:**

````bash
# Python 3.12 (Windows, ohne Admin):
winget install --id Python.Python.3.12 --scope user

pip install -r custom_components/comfobox/requirements_test.txt
pytest custom_components/comfobox/tests -v
ruff check custom_components/comfobox
````

> **Konfiguration:** `pytest.ini` im Repo-Root setzt `asyncio_mode = auto` und den
> `testpaths` auf `custom_components/comfobox/tests`.

| Test | Werkzeug | Status |
|---|---|---|
| Config Flow (Erfolg, falscher Token, nicht erreichbar) | `pytest-homeassistant-custom-component` | ✅ 3 Tests |
| API-Client (Mock) | `aioresponses` | ✅ 10 Tests |
| Coordinator (Timeout, 401, Normalfall) | `pytest` | ✅ 8 Tests |
| Entity-Mapping | `pytest` | ✅ 12 Tests |
| Schreibpfade (number/select/switch, Service) | `pytest` | ✅ 2 Tests |

**Akzeptanzkriterien:**
- [x] `pytest` grün (35 Tests)
- [x] `ruff check` ohne Befund
- [x] Mehr als **5** Testfälle
- [x] API-Client, Coordinator und Entity-Mapping sind getestet

> **Windows-Fallstricke** (in `tests/conftest.py` gelöst):
> `pytest-socket` blockiert jede Socket-Erstellung – unter Windows braucht asyncio für
> den Self-Pipe ein TCP-Socketpaar → `pytest_socket.disable_socket` im conftest ersetzen.
> `aiodns` (HAs fester DNS-Resolver) verlangt unter Windows einen `SelectorEventLoop` →
> im Test `ThreadedResolver` statt `AsyncResolver`. Im **Produktivbetrieb** (Linux)
> bleibt aiodns unverändert.
>
> **Zusätzlich (für eine eigene `ClientSession`):** `aiohttp.ClientSession()` benutzt
> unter Windows ebenfalls aiodns → im Test einen eigenen `TCPConnector` mit
> `aiohttp.ThreadedResolver` setzen.

> **Vorher manuell geprüft:** Der Code wurde von Hand reviewiert; dabei wurden drei
> echte Fehler gefunden und behoben (unausgewerteter `await` beim Service-Setup,
> fehlerhafte Content-Type-Prüfung, `config_entry`-Zugriff im Unique-ID).
>
> **Beim Schreiben der Tests gefunden (und behoben):** Die Plattformen
> (`sensor`/`number`/`select`/`switch`) stürzten mit `AttributeError` ab – sie
> iterierten auch über den reservierten Statuseintrag `__status__` – und die
> Entitäten waren **keine** `SensorEntity`/`NumberEntity`/… (der Service
> `comfobox.set_value` schrieb außerdem auf die falsche Item-Id). Details im
> [Protokoll](protokoll.md).

---

## 8b.1 – Add-on-Manifest · 1.0 PT ✅

| | |
|---|---|
| **Ziel** | `deploy/ha-addon/comfobox/config.yaml` |

| Schlüssel | Wert |
|---|---|
| `image` | `ghcr.io/rf77/comfobox-gateway` |
| `init` | `false` (das Image bringt tini mit) |
| `arch` | `aarch64`, `amd64` |
| Option `device` | `device(subsystem=tty)` → **Dropdown** |
| `ports` | `8080/tcp: 8099` |
| `ingress` | `true` → Panel in HA |
| `watchdog` | `http://[HOST]:[PORT:8080]/api/health` |
| `services` | `mqtt:want` |
| `map` | `app_config`, `data` |

**Akzeptanzkriterien:**
- [ ] `repository.yaml` im Wurzelverzeichnis
- [ ] Der Supervisor zeigt die seriellen Ports im Dropdown
- [ ] Die Web-UI erscheint als Panel in HA
- [ ] Port 8099 ist für die Integration erreichbar

---

## 8b.2 – Options-Mapping · 1.0 PT ✅

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Gateway/Configuration/AddOnOptionsProvider.cs` ✅ |

Der Supervisor schreibt die validierten Optionen nach `/data/options.json`. Der
Gateway liest diese Datei **optional** und mappt sie.

| Option | Gateway-Ziel |
|---|---|
| `device` | `Bacnet:Port` |
| `baudrate` | `Bacnet:Baudrate` |
| `base_topic` | `Gateway:BaseTopic` |
| `broker_host` / `broker_port` | `Mqtt:Brokers:0:Host` / `:Port` |
| `broker_username` / `broker_password` | `Mqtt:Brokers:0:Username` / `:Password` |
| `use_tls` | `Mqtt:Brokers:0:UseTls` |
| `enable_discovery` | `Mqtt:EnableDiscovery` |
| `log_level` | `Logging:Level` |

**Präzedenz:** `options.json` überschreibt `config.json` **und** die
Umgebungsvariablen. Grund: das Image setzt `COMFOBOX_Bacnet__Port` als Vorgabe –
wäre die Umgebungsvariable stärker, wäre die Portwahl des Add-ons wirkungslos.
Die Kette ist durchgängig aufsteigend: `Umgebung → config.json → options.json`.

> **Leere Werte werden ignoriert:** Der Supervisor schreibt **alle** Optionen –
> auch die nicht benutzten. Ein leerer Broker-Host würde einen Broker **ohne
> Host** erzeugen → `ValidateOnStart` schlägt fehl → der Container startet nicht.

**Akzeptanzkriterien:**
- [x] Die Datei wird gelesen, wenn sie existiert
- [x] Fehlende Datei → kein Fehler (manueller Betrieb)
- [x] Ein Test prüft die Zuordnung jedes Feldes
- [x] Verifiziert am laufenden Gateway (`GET /api/config` liefert exakt die
      Werte der `options.json`)

---

## 8b.3 – Add-on-Doku & Übersetzungen · 1.0 PT ✅

| | |
|---|---|
| **Ziel** | `deploy/ha-addon/comfobox/DOCS.md` ✅, `translations/{de,en}.yaml` ✅ |

**Akzeptanzkriterien:**
- [ ] `DOCS.md` wird im Add-on-Panel angezeigt
- [ ] Alle Options-Felder haben eine Beschreibung
- [ ] Deutsch **und** Englisch

---

## 8c.1 – Multi-Arch-Publish & HACS-Release · 2.0 PT ✅

| | | |
|---|---|---|
| **Datei** | `.github/workflows/ha-release.yml` ✅ · `.github/workflows/docker-publish.yml` (Multi-Arch) ✅ |

| Schritt | Wert |
|---|---|
| Image | `linux/amd64` **und** `linux/arm64` (Multi-Arch-Manifest) |
| HACS-Release | Tag = Version aus `manifest.json` (ohne „v") |
| Add-on-Release | Tag = Version aus `config.yaml` (ohne „v") |
| Validierung | `hacs/action` und `hassfest` |

> **HACS findet eine Custom-Integration über die **Releases**:** der Tag muss
> exakt der Version in der `manifest.json` entsprechen. Der Auslöser ist der Tag
> `v<version>` – die Releases entstehen **ohne** „v".
>
> **Der HA-Supervisor zieht das Image mit dem Versions-Tag** – ohne ihn findet
> er das Image nicht. Das Add-on-Manifest braucht also
> `image: ghcr.io/rf77/comfobox-gateway:<version>`.

**Akzeptanzkriterien:**
- [x] Das Image ist multi-arch (`linux/amd64` **und** `linux/arm64`)
- [x] Die Basis-Images unterstützen arm64 (`dotnet/sdk`, `dotnet/aspnet`,
      `node:22-alpine` – per `docker buildx imagetools inspect` geprüft)
- [x] Der Release-Workflow prüft **alle** Versionen (Tag ↔ `manifest.json` ↔
      `config.yaml` ↔ Image-Tag) und bricht bei Abweichung ab
- [x] HACS und hassfest validieren den Stand
- [ ] Das Image läuft auf einem Pi 5 (`arm64`) – **Hardwaretest steht aus**
- [ ] HACS findet die Integration in der Release-Version – **Release steht aus**
      (kein Tag gepusht)
- [ ] Der Add-on-Store zeigt das Update an – **Release steht aus**

---

## 8d.1 – Token-API · 1.0 PT ✅

| | | |
|---|---|---|
| **Bezug** | [Aufgabe 4.8](phase-4.md#48--token-api--10-pt) |
| **UI** | [Aufgabe 5.5](phase-5.md#55--konfigurationsseite--10-pt) → Karte *API-Token* |

**Akzeptanzkriterien:**
- [x] `POST /api/security/tokens` erzeugt einen Token (nur Admin)
- [x] Der Token wird **einmal** im Klartext angezeigt, danach nur noch der Hash
- [x] PBKDF2-HMAC-SHA256 mit 600.000 Iterationen
- [x] `DELETE /api/security/tokens/{id}` widerruft einen Token
- [x] `Authorization: Bearer <token>` wird akzeptiert, ungültig → `401`
- [x] In der UI: Token erzeugen, Name vergeben, widerrufen
- [x] Verifiziert: am laufenden Gateway (201 / 200 / 401 / 204) **und** im
      Browser (erzeugen → Klartext sichtbar → widerrufen mit Bestätigung)

> **Neue Regel (Phase 8d):** Ein **vorgelegtes**, aber **ungültiges**
> `Authorization` wird **immer** mit `401` beantwortet – auch auf den offenen
> Endpunkten. Ohne Header bleibt der LAN-Modus unverändert. Grund: die
> HA-Integration prüft ihren Token über `GET /api/status`; ein falscher Token
> darf dort nicht als gültig durchgehen.
> → [ADR-012](../05-entscheidungen/adr-012-api-zugriffskontrolle.md)

---

## Abnahme der Phase 8a–8d

- [x] `hacs.json` vorhanden, HACS findet die Integration
- [x] **Kein** MQTT-Broker für die Entitäten erforderlich
- [x] Schreibvorgänge laufen über den Gateway-Schreibschutz
- [x] Add-on installierbar, serieller Port per Dropdown wählbar
- [ ] Image für `linux/amd64` **und** `linux/arm64` **publiziert** – der
      Workflow ist fertig, aber **noch nicht gelaufen** (kein Tag gepusht)
- [x] `pytest` grün (35 Tests)

→ Weiter mit [Phase 7](phase-7.md) (Parallelbetrieb & Umschaltung)
