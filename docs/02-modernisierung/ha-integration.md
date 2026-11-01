# HA-Integration (ohne MQTT)

> Anforderung 7: *„ohne MQTT-Konfiguration direkt auf die ComfoBox Zugriff, als Gerät mit
> seinen Entities."*
> Entscheidung: [ADR-009](../05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md)

## 1. Zweck & Geltungsbereich

Dieses Dokument spezifiziert eine **Home-Assistant-Integration** (`comfobox`), die

- über HACS installiert wird,
- **ohne** MQTT-Broker auskommt,
- ein **Gerät** „ComfoBox ELESTA" mit allen **127 Entitäten** anlegt,
- Werte **lesen** und **schreiben** kann,
- mit dem Gateway über **REST/SSE** spricht.

> **Nicht** Gegenstand dieses Dokuments: der BACnet-Protokollstack (der bleibt im .NET-Gateway)
> und der MQTT-Pfad (der parallel bestehen bleibt).

## 2. Architektur

```
┌──────────────────────── Home Assistant ─────────────────────────┐
│                                                                 │
│  custom_components/comfobox                                    │
│    ├── config_flow.py   (URL + Token)                           │
│    ├── api.py           (aiohttp-Client, Bearer-Token)          │
│    ├── coordinator.py   (Polling, ValueCache)                   │
│    └── sensor / number / select / binary_sensor                │
│         │                                                       │
│         │  REST + SSE  (http://<ha-host>:8099)                 │
└─────────┼───────────────────────────────────────────────────────┘
          │
┌─────────▼────────────────────────────┐
│  ComfoBox.Gateway  (.NET 10)         │
│    ├── /api/items        (GET, PUT)   │
│    ├── /api/status       (GET)       │
│    ├── /api/events       (SSE)       │
│    ├── /api/health       (GET)       │
│    └── BACnet-MS/TP  ──► ComfoBox    │   ← einziger Master am Bus
└──────────────────────────────────────┘
```

| Punkt | Entscheidung |
|---|---|
| Transport | **REST** für Anlage/Abfrage/Schreiben, **SSE** später für Live-Werte |
| Master am Bus | **ausschließlich** das Gateway |
| Metadaten-Quelle | der Item-Katalog im Gateway (`/api/items`) – die Integration erfindet **keine** Min/Max/Enum-Werte |
| Broker | **nicht** erforderlich |
| Version | v1: `local_polling`; v2: `local_push` (SSE) |

## 3. Gerät und Entitäten

### 3.1 Ein Gerät, nicht 127 lose Entitäten

Die Integration legt **ein** Gerät im Device-Registry an:

| Feld | Wert | Quelle |
|---|---|---|
| `identifiers` | `{(DOMAIN, "comfobox-<deviceId>")}` | aus `/api/status` |
| `manufacturer` | `Zehnder` | statisch |
| `model` | `ComfoBox Series 5 (ELESTA)` | statisch |
| `sw_version` | `1.0.0` | `GET /api/status` → `version` |
| `configuration_url` | `http://<host>:8099` | aus der Config-Entry |
| `connections` | `{(CONNECTION_NETWORK_MAC, mac)}` | falls der Adapter sie meldet |

> **Warum ein Gerät:** Der Nutzer sieht „ComfoBox ELESTA" als **ein** Gerät in der
> Geräteliste – nicht 127 einzelne Sensoren ohne Gerätezuordnung. Genau das war der Wunsch.

### 3.2 Entitäten-Mapping

Die Zuordnung Item → HA-Plattform ergibt sich **aus den Metadaten** des Gateways:

| Item-Merkmal | HA-Plattform | Beispiel |
|---|---|---|
| `isReadOnly = true`, Typ `float` | `sensor` | `sensor.comfobox_heat_pump_operating_hours` |
| `isReadOnly = false`, Typ `float`, Min/Max vorhanden | `number` | `number.comfobox_zone_setpoint_normal` |
| `isReadOnly = false`, Typ `float`, Min/Max **fehlen** | `number` ohne Bereich (nur wenn `writable`) | `number.comfobox_zone_party_time` |
| `isReadOnly = false`, Typ Enum | `select` | `select.comfobox_zone_operation_mode` |
| `isReadOnly = true`, Typ Enum | `sensor` mit Options-Liste | `sensor.comfobox_zone_operation_mode_state` |
| `isReadOnly = false`, `0/1`-Analog | `switch` bzw. `number` | `switch.comfobox_warmwater_do_heat_water_now` |

> **Nicht 1:1 mit dem MQTT-Schema:** Die MQTT-Discovery kennt nur `sensor`/`number`/`select`.
> Die Integration darf **mehr** anbieten (z. B. `switch` für boolesche Items), weil sie das
> Item-Modell direkt sieht. Das ist einer der Mehrwerte gegenüber der reinen MQTT-Variante.

### 3.3 Namensschema

| Teil | Regel | Beispiel |
|---|---|---|
| `entity_id` | `comfobox_` + `group` + `name` in snake_case | `comfobox_zone_setpoint_normal` |
| Anzeigename | `Description` aus dem Katalog, Fallback `Name` | „Raumsollwert Normal" |
| `unique_id` | `{deviceId}_{itemId}` | `comfobox1_zone_setpoint_normal` |
| `translation_key` | `{group}_{name}` (für spätere Core-Übernahme) | `zone_setpoint_normal` |
| Geräteklasse | aus der Einheit abgeleitet | `°C` → `temperature`, `h` → `duration`, `%` → `humidity` |

> **Wichtig:** Die `entity_id`s werden **nicht** umbenannt, wenn sich der MQTT-Topic ändert –
> sie leiten sich aus dem **Item-Katalog** ab, nicht aus dem Topic. Das entkoppelt die
> Integration von der MQTT-Kompatibilitätszusage.

### 3.4 Sonder-Entitäten

Zusätzlich zu den 127 Items:

| Entität | Plattform | Quelle |
|---|---|---|
| `ComfoBox Verbindungsstatus` | `binary_sensor` | `/api/status` → `bacnet.connected` |
| `ComfoBox Broker verbunden` | `binary_sensor` | `/api/status` → `mqtt.connected` |
| `ComfoBox Schreibvorgänge (24 h)` | `sensor` | `/api/status` → `writes.count` / `writes.limit` |
| `ComfoBox Version` | `sensor` | `/api/status` → `version` |

## 4. Config Flow

### 4.1 Schritt 1 – Benutzer

| Feld | Typ | Pflicht | Beschreibung |
|---|---|---|---|
| `host` | `str` | ja | `http://192.168.1.20:8099` (ohne Pfad, ohne Slash am Ende) |
| `token` | `password` | ja | API-Token aus der Gateway-UI |
| `verify_ssl` | `bool` | nein | Default `true` |

### 4.2 Validierung

Beim Absenden:

````python
try:
    status = await client.get_status()          # GET /api/status
except AuthenticationError:
    errors["base"] = "invalid_auth"             # → "Ungültiges Token"
except ConnectionError:
    errors["base"] = "cannot_connect"           # → "Gateway nicht erreichbar"
else:
    await async_config_entry_first_refresh()
````

> **Warum funktioniert die Token-Prüfung über `/api/status`?**
> Der Endpunkt ist zwar offen, aber: wer einen **ungültigen** Token
> vorlegt, bekommt **immer** `401` (→ [ADR-012](../05-entscheidungen/adr-012-api-zugriffskontrolle.md)).
> Die Integration erkennt einen falschen Token damit **sofort** – beim
> Einrichten, nicht erst Wochen später.

| Fehler | Meldung (de) |
|---|---|
| `cannot_connect` | „Das Gateway ist unter dieser Adresse nicht erreichbar." |
| `invalid_auth` | „Der API-Token ist ungültig." |
| `unsupported_version` | „Diese Gateway-Version wird nicht unterstützt (erforderlich: ≥ 1.0.0)." |

### 4.3 Nach der Einrichtung

| Aktion | Ergebnis |
|---|---|
| Gerät anlegen | „ComfoBox ELESTA" |
| Entitäten anlegen | 127 + 4 Sonder-Entitäten |
| `unique_id` | `{deviceId}_{itemId}` → Duplikate werden erkannt |

> **Re-Konfiguration:** Der Nutzer kann die Adresse später über *Integration → Konfigurieren*
> ändern (Reauth-Flow). Ohne Token geht es nicht – der Token ist Pflicht.

## 5. API-Client

### 5.1 Endpunkte

| Methode | Endpunkt | Zweck |
|---|---|---|
| `GET` | `/api/status` | Geräteinfo, Verbindungen, Schreibzähler, Version |
| `GET` | `/api/items` | vollständiger Item-Katalog mit aktuellen Werten |
| `GET` | `/api/items/{id}` | einzelnes Item (für Details) |
| `PUT` | `/api/items/{id}` | Wert schreiben |
| `GET` | `/api/health` | Health-Check |
| `GET` | `/api/events` | SSE (v2) |

### 5.2 DTO

````python
@dataclass(slots=True)
class ComfoBoxItem:
    id: str
    group: str
    name: str
    description: str | None
    value_type: str            # "float" | "enum" | "bool"
    unit: str | None
    is_read_only: bool
    min_value: float | None
    max_value: float | None
    enum_values: list[str]
    value: float | str | bool | None
    last_updated: str | None
    is_stale: bool
````

> **Vertragsregel:** Die Integration liest **nur** Felder, die das Gateway garantiert
> liefert. Neue Felder sind additiv erlaubt, **fehlende** Pflichtfelder sind ein breaking
> Change → das Gateway muss dann die `version` in `/api/status` erhöhen, und die Integration
> prüft beim Aufbau `version >= 1.0.0`.

### 5.3 Client

````python
class ComfoBoxApiClient:
    def __init__(self, session: aiohttp.ClientSession, host: str, token: str) -> None:
        self._session = session
        self._host = host.rstrip("/")
        self._token = token

    def _headers(self) -> dict[str, str]:
        return {
            "Authorization": f"Bearer {self._token}",
            "Accept": "application/json",
        }

    async def get_items(self) -> list[ComfoBoxItem]:
        async with self._session.get(f"{self._host}/api/items", headers=self._headers()) as resp:
            resp.raise_for_status()
            return [ComfoBoxItem.from_dict(d) for d in await resp.json()]

    async def write_item(self, item_id: str, value: float | str | bool) -> None:
        async with self._session.put(
            f"{self._host}/api/items/{item_id}",
            headers=self._headers(),
            json={"value": value},
        ) as resp:
            resp.raise_for_status()
````

> **Regeln:**
> - `Authorization: Bearer <token>` – **nie** im Log, **nie** in der Exception-Meldung.
> - Ein **einziger** `aiohttp.ClientSession` für die gesamte Integration (Performance-Regel von HA).
> - Timeout: 10 s für normale Aufrufe, 30 s für Schreibvorgänge (BACnet-Timeout).

## 6. Coordinator

````python
class ComfoBoxCoordinator(DataUpdateCoordinator[dict[str, ComfoBoxItem]]):
    def __init__(self, hass: HomeAssistant, client: ComfoBoxApiClient, interval: int) -> None:
        super().__init__(
            hass,
            _LOGGER,
            name=DOMAIN,
            update_interval=timedelta(seconds=interval),
        )
        self._client = client

    async def _async_update_data(self) -> dict[str, ComfoBoxItem]:
        try:
            items = await self._client.get_items()
        except AuthenticationError as err:
            raise ConfigEntryAuthFailed from err
        except (aiohttp.ClientError, asyncio.TimeoutError) as err:
            raise UpdateFailed(f"Gateway nicht erreichbar: {err}") from err
        return {item.id: item for item in items}
````

| Aspekt | Entscheidung |
|---|---|
| Intervall | **10 s** (konfigurierbar, Minimum 5 s) |
| „Letzter bekannter Wert" | Der Coordinator behält die letzten Werte → Entitäten zeigen nicht sofort `unknown` |
| `stale` | Items mit `isStale = true` werden als `unavailable` markiert (Wert älter als 3 Poll-Zyklen) |
| Schreibfehler | `UpdateFailed` → alle Entitäten `unavailable`, aber der **letzte Wert** bleibt sichtbar (`available`-Status bleibt, `state` wird `unknown`) |

> **Wichtig:** Bei einem Gateway-Ausfall sollen die Entitäten **nicht** flackern.
> HA-Standard: `UpdateFailed` → Entitäten werden `unavailable`. Für eine Heizungssteuerung
> ist das der richtige Weg: ein `unavailable` ist eindeutig, ein „letzter Wert" wäre
> gefährlich (der Nutzer glaubt, der Sollwert wäre aktiv).

## 7. Alternative Direktzugriff (BACnet in Python) — verworfen

> Ausführliche Begründung in
> [ADR-009](../05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md#e4--kein-direkter-bacnet-zugriff-aus-ha-f4).

### 7.1 Warum nicht

| Grund | Detail |
|---|---|
| **Ein Master pro Bus** | MS/TP ist ein Token-Bus. Gateway **oder** Direktintegration – nie beides. Ein Umschaltpunkt ist im Heizungsbetrieb fehleranfällig. |
| **Doppelte Metadaten** | Min/Max, Enum-Namen, Schreibbarkeit, Prioritäten liegen im C#-Katalog. Eine Python-Kopie divergiert garantiert → Schreibvorgänge außerhalb des gültigen Bereichs. |
| **Schreibschutz** | Das 24-h-Limit und die `InvariantCulture`-Regeln liegen im Gateway. Direktzugriff umginge beides → EEPROM-Schutz verletzt. |
| **Fehlerkanal** | Anforderung 5 verlangt drei Kanäle (Log, `Status/Errors`, UI). Eine Direktintegration hat keinen davon. |
| **Aufwand** | MS/TP-Serial-Stack in Python (Framing, Token, Timer, DLL-Kodierung, Reconnect): 600–900 Zeilen, ≈15–20 PT. |

### 7.2 Was der Nutzer dadurch verliert

| Verlust | Bewertung |
|---|---|
| Gateway als Abhängigkeit | Der Gateway muss laufen (als Add-on, SPK oder Container). |
| Zusätzliche Round-Trips | HTTP statt direktem Bus – im LAN irrelevant (ein Round-Trip ≈ 2 ms). |
| Python im Projekt | Zweiter Toolchain-Pfad (HA-Tests mit `pytest`). |

> **Ergebnis:** Der Nutzer gewinnt **keinen** funktionalen Vorteil, verliert aber den
> Schreibschutz. → **verworfen.**

## 8. Services

| Service | Zweck | Nutzt |
|---|---|---|
| `comfobox.set_value` | Wert schreiben | `PUT /api/items/{id}` |
| `comfobox.restart` | Gateway neu starten | `POST /api/service/restart` |
| `comfobox.read_all` | alle Items sofort neu lesen | Coordinator-Update auslösen |

````yaml
# Beispiel: Sollwert setzen
service: comfobox.set_value
target:
  entity_id: number.comfobox_zone_setpoint_normal
data:
  value: 21.5
````

## 9. Fehlerbehandlung

| Situation | Verhalten |
|---|---|
| Gateway nicht erreichbar | `UpdateFailed` → Entitäten `unavailable`, Log-Warnung **ohne** Token |
| Token ungültig | `ConfigEntryAuthFailed` → HA zeigt „Neu authentifizieren" |
| Schreibvorgang abgelehnt (409/422) | HA-`HomeAssistantError` mit der Meldung des Gateways |
| Item unbekannt (404) | `HomeAssistantError` „Unbekanntes Item" |
| Gateway-Version zu alt | Integration lädt gar nicht erst (Versionsprüfung im Manifest) |

> **Regel:** Der **Token** darf in **keinem** Log, **keiner** Exception und **keinem**
> HA-Diagnostic-Eintrag stehen. Beim Einsammeln der Diagnose wird er maskiert.

## 10. Verpackung (HACS)

### 10.1 Repository-Struktur

````
/
├── hacs.json                  # HACS-Metadaten
├── custom_components/
│   └── comfobox/
│       ├── manifest.json
│       ├── __init__.py
│       ├── const.py
│       ├── api.py
│       ├── coordinator.py
│       ├── config_flow.py
│       ├── entity.py
│       ├── sensor.py
│       ├── number.py
│       ├── select.py
│       ├── binary_sensor.py
│       ├── switch.py
│       ├── strings.json
│       └── translations/en.json
├── tests/                     # pytest + HA-Harness
└── README.md
````

### 10.2 `hacs.json`

````json
{
  "name": "ComfoBox Gateway",
  "render_readme": true,
  "homeassistant": "2024.11.0"
}
````

| Feld | Wert | Begründung |
|---|---|---|
| `name` | „ComfoBox Gateway" | Anzeigename in HACS |
| `render_readme` | `true` | README wird im HACS-Dialog angezeigt |
| `homeassistant` | `2024.11.0` | Mindestversion für `entry.runtime_data` und `_get_reauth_entry()` |

> **Kein** `content_in_root`: HACS installiert den Ordner `custom_components/comfobox`
> unverändert nach `config/custom_components/comfobox/`.

### 10.3 `manifest.json`

````json
{
  "domain": "comfobox",
  "name": "ComfoBox",
  "codeowners": ["@RF77"],
  "config_flow": true,
  "documentation": "https://github.com/RF77/comfobox-mqtt",
  "integration_type": "device",
  "iot_class": "local_polling",
  "issue_tracker": "https://github.com/RF77/comfobox-mqtt/issues",
  "requirements": [],
  "version": "1.0.0"
}
````

| Feld | Wert | Begründung |
|---|---|---|
| `integration_type` | `device` | ein Gerät pro Config-Entry |
| `iot_class` | `local_polling` | v1 pollt; SSE (v2) → `local_push` |
| `requirements` | `[]` | reiner Standardbibliothek-Einsatz (`aiohttp` ist in HA vorhanden) |
| `version` | `1.0.0` | **Pflicht** für Custom-Integrations |

## 11. Tests

| Test | Werkzeug |
|---|---|
| Config-Flow (Erfolg, falscher Token, nicht erreichbar) | `pytest-homeassistant-custom-component` |
| API-Client gegen einen Mock (`aioresponses`) | `pytest` |
| Coordinator (Timeout, 401, Normalfall) | `pytest` |
| Entity-Mapping (Item → Plattform, `unique_id`) | `pytest` |
| Snapshot der Entitäten-Liste | `syrupy` |

> **Ohne Tests keine HA-Integration.** HA-Core-Integrationen haben eine Testabdeckung von
> >95 % (Silber) – für eine Custom-Integration ist das kein Muss, aber **ein Test-Skelett
> mit fünf Tests** ist Pflicht, sonst wird die Integration zur Wartungsschleife.

## 12. Abhängigkeiten & Reihenfolge

```
Phase 4 (REST/SSE-API)  ──▶  Phase 8a (HACS-Integration)
                                    │
Phase 6 (Container)       ──▶  Phase 8b (HA-Add-on)
                                    │
                          Phase 8c (Multi-Arch-Image + Release)
```

| Phase | Inhalt | PT |
|---|---|---|
| **8a** | HACS-Integration (Domain, API-Client, Coordinator, 5 Plattformen, Tests) | 8 |
| **8b** | HA-Add-on (Manifest, Options, `device`-Dropdown, `ingress`, Übersetzungen) | 3 |
| **8c** | Multi-Arch-Publish (`amd64` + `arm64`), HACS-Release, Add-on-Repo | 2 |
| **8d** | Token-API im Gateway (`/api/security/tokens`) | 1 |

## 13. Definition of Done

- [ ] `hacs.json` vorhanden, HACS findet die Integration
- [ ] Config Flow mit URL + Token, Fehlerfälle verständlich
- [ ] Ein **Gerät** „ComfoBox ELESTA" mit Hersteller, Modell und SW-Version
- [ ] Alle 127 Items als Entitäten (Sensor/Number/Select/Switch) angelegt
- [ ] Enum-Items als `select` mit **exakten** Enum-Namen (nicht übersetzt)
- [ ] Schreibbare Analog-Items als `number` mit **exaktem** Min/Max aus dem Katalog
- [ ] `unique_id` stabil (`{deviceId}_{itemId}`)
- [ ] Gateway-Ausfall → `unavailable`, kein Flackern
- [ ] Token erscheint **nirgends** in Logs oder Exceptions
- [ ] Tests vorhanden und grün (`pytest`)
- [ ] `strings.json` + `translations/en.json` (Englisch als HA-Standard, **deutsch** ergänzt)
- [ ] HA-Add-on installierbar, serieller Port per Dropdown wählbar
- [ ] Web-UI erscheint als Panel in HA (`ingress`)
- [ ] Image für `linux/amd64` **und** `linux/arm64` publiziert
- [ ] README erklärt die Installation in 5 Sätzen

## 14. Verwandte Dokumente

- [ADR-009](../05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md)
- [Installationsstufen](../03-deployment/installationsstufen.md)
- [home-assistant.md](home-assistant.md) – MQTT-Discovery (bleibt bestehen)
- [datenmodell-und-api.md](datenmodell-und-api.md) – REST-Vertrag
- [mqtt-kompatibilitaet.md](../04-migration/mqtt-kompatibilitaet.md) – MQTT-Vertrag (unberührt)
