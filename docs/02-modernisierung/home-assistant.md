# Home-Assistant-Integration

## 1. Ausgangslage

Im Ist-Zustand gibt es **keine** MQTT-Discovery. Alle 127 Topics mussten in Home Assistant
**manuell** angelegt werden. Es gibt:

- keinen `availability`-Mechanismus (nur `ComfoBox/Connected` mit dem Reconnect-Bug A10),
- **keinen** Restart-Mechanismus,
- **keine** Fehler-/Health-Entitäten.

Ziel: **Null-Setup** in HA – der Container konfiguriert sich selbst.

## 2. Discovery-Übersicht

| Discovery-Topic | Plattform | Inhalt |
|---|---|---|
| `homeassistant/device/comfobox/config` | `device` | Gerätebeschreibung (identifiers, model, sw_version) |
| `homeassistant/sensor/comfobox_<id>/config` | `sensor` | Analog-/Enum-Werte (read-only) |
| `homeassistant/number/comfobox_<id>/config` | `number` | schreibbare Analog-Werte |
| `homeassistant/select/comfobox_<id>/config` | `select` | schreibbare Enum-Werte |
| `homeassistant/switch/comfobox_<id>/config` | `switch` | Boolean-Zustände |
| `homeassistant/button/comfobox_restart/config` | `button` | **Neustart** |
| `homeassistant/sensor/comfobox_health/config` | `sensor` | Health-Status |
| `homeassistant/sensor/comfobox_version/config` | `sensor` | Version |
| `homeassistant/sensor/comfobox_writes24h/config` | `sensor` | Schreibzähler |
| `homeassistant/sensor/comfobox_last_error/config` | `sensor` | letzter Fehler |

Alle Discovery-Payloads werden **retained** publiziert.

## 3. Device-Discovery

Ein Gerät mit 127 Komponenten → **ein** Device-Payload mit `cmps` wäre ein riesiger JSON-Blob
(> 60 KB). Deshalb: **Device-Discovery für die Gerätebeschreibung** + **Single-Component-Discovery
pro Entität**, alle mit derselben `device.identifiers`-Referenz. So bekommt HA **eine** Gerätekarte
mit 130 Entitäten, ohne ein monolithisches Payload.

````csharp
// src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs
internal sealed class HomeAssistantDiscovery(IMqttPublisher publisher, IOptions<MqttOptions> options)
{
    private string Prefix => options.Value.DiscoveryPrefix;   // "homeassistant"

    public async Task PublishDeviceAsync(string baseTopic, string version, CancellationToken ct)
    {
        var payload = new
        {
            dev = new
            {
                ids = new[] { "comfobox" },
                name = "ComfoBox",
                mf = "Zehnder",
                mdl = "ComfoBox Series 5",
                sw = version,
                cu = "http://<host>:8080",              // configuration_url → UI
            },
            o = new
            {
                name = "comfobox-gateway",
                sw = version,
                url = "https://github.com/RF77/comfobox-mqtt",
            },
        };

        await publisher.PublishAsync($"{Prefix}/device/comfobox/config",
                                      JsonSerializer.Serialize(payload), retain: true, ct: ct);
    }
}
````

> `configuration_url` verlinkt die HA-Geräteseite direkt auf die Web-UI – der Nutzer landet
> dort, wo er die Heizung bedienen kann.

## 4. Entitäts-Discovery

### 4.1 Read-only Analogwert → `sensor`

````json
{
  "name": "Raumsollwert Normalbetrieb",
  "unique_id": "comfobox_zone_setpoint_normal",
  "state_topic": "ComfoBox/Zone/SetPointNormal",
  "json_attributes_topic": "ComfoBox/Zone/SetPointNormal",
  "device_class": "temperature",
  "unit_of_measurement": "°C",
  "state_class": "measurement",
  "availability_topic": "ComfoBox/Status/Health",
  "payload_available": "online",
  "payload_not_available": "offline",
  "device": { "identifiers": ["comfobox"] },
  "origin": { "name": "comfobox-gateway", "sw_version": "1.0.0" }
}
````

### 4.2 Schreibbarer Analogwert → `number`

````json
{
  "name": "Raumsollwert Normalbetrieb",
  "unique_id": "comfobox_zone_setpoint_normal",
  "state_topic": "ComfoBox/Zone/SetPointNormal",
  "command_topic": "ComfoBox/Zone/SetPointNormal/Set",
  "min": 5,
  "max": 30,
  "step": 0.5,
  "unit_of_measurement": "°C",
  "device_class": "temperature",
  "mode": "box",
  "availability_topic": "ComfoBox/Status/Health",
  "device": { "identifiers": ["comfobox"] },
  "origin": { "name": "comfobox-gateway", "sw_version": "1.0.0" }
}
````

> **Wichtig:** Der Zahlenwert kommt als **Payload** (`21.5`), nicht als JSON. HA parst das mit
> der Standard-`value_template` (`{{ value }}`) problemlos – und genau so, wie es der
> Ist-Zustand tat. **Keine JSON-Umbau-Aktion für bestehende Setups nötig.**

### 4.3 Schreibbarer Enumwert → `select`

````json
{
  "name": "Betriebsart",
  "unique_id": "comfobox_zone_operationmode",
  "state_topic": "ComfoBox/Zone/OperationMode",
  "command_topic": "ComfoBox/Zone/OperationMode/Set",
  "options": ["Normal", "Economy", "Comfort", "Eco", "Party", "Vacation", "Sleep"],
  "availability_topic": "ComfoBox/Status/Health",
  "device": { "identifiers": ["comfobox"] },
  "origin": { "name": "comfobox-gateway", "sw_version": "1.0.0" }
}
````

> `command_topic` ist `…/Set` (Name-Form) – **identisch** zum Ist-Zustand. Die bestehenden
> HA-Automationen, die `ComfoBox/Zone/OperationMode/Set` mit `Normal` publishen, funktionieren
> unverändert.

### 4.4 Enum read-only → `sensor` mit `value_template`

````json
{
  "name": "Wärmepumpenstatus",
  "unique_id": "comfobox_states_heatpump_heatpumpstatus",
  "state_topic": "ComfoBox/States/HeatPump/HeatPumpStatus",
  "availability_topic": "ComfoBox/Status/Health",
  "device": { "identifiers": ["comfobox"] },
  "origin": { "name": "comfobox-gateway", "sw_version": "1.0.0" }
}
````

Der Payload ist der Enum-**Name** (`HeatingWithHeatPump`) – identisch zum Ist-Zustand.

### 4.5 Optionale `AsNumber`-Variante

Für Enums publiziert das Gateway parallel `…/AsNumber`. Das kann als **zusätzliche** Entität
angeboten werden (z. B. als Template-Sensor) – aber **nicht** per Default-Discovery, um die
Entitätszahl nicht zu verdoppeln.

## 5. Availability (Anforderung 5)

### 5.1 Availability-Topic

| Topic | Payload | Retain | LWT |
|---|---|---|---|
| `ComfoBox/Status/Health` | `online` / `degraded` / `offline` | ja | **ja** (`offline`) |
| `ComfoBox/Connected` | `1` / `0` | ja | **ja** (`0`) – Kompatibilität |

Alle Entitäten verwenden:

````json
"availability_topic": "ComfoBox/Status/Health",
"payload_available": "online",
"payload_not_available": "offline"
````

### 5.2 Semantik

| Zustand | Bedeutung | HA-Darstellung |
|---|---|---|
| `online` | BACnet **und** MQTT verbunden, Polling läuft | verfügbar |
| `degraded` | MQTT verbunden, aber **BACnet nicht** (oder Bus-Timeout) | verfügbar, aber Werte sind `stale` |
| `offline` | MQTT-Verbindung weg (LWT) oder Prozess beendet | **nicht verfügbar** |

> **Warum `degraded` nicht `offline`:** Wenn der USB-Adapter gezogen wird, ist der MQTT-Broker
> noch erreichbar. HA soll dann „unbekannt" bzw. „veraltet" anzeigen, nicht „gerät weg".
> Genau dafür gibt es zusätzlich `Status/Bacnet` (JSON) mit Details.

### 5.3 Geburt & Testament

HA publiziert auf `homeassistant/status` → `online` beim Start der MQTT-Integration.
Das Gateway abonniert dieses Topic und sendet die Discovery-Payloads erneut:

````csharp
client.ApplicationMessageReceivedAsync += async e =>
{
    if (e.ApplicationMessage.Topic == $"{Prefix}/status" &&
        Encoding.UTF8.GetString(e.ApplicationMessage.Payload) == "online")
    {
        await PublishAllDiscoveryAsync(ct);
        await PublishAllStateAsync(ct);        // Retained-Werte erneut senden
    }
};
````

> Das ist der empfohlene Weg: **nicht** nur retained Discovery zu senden, sondern auf die
> Geburt zu reagieren. Damit sind die Entitäten nach einem HA-Neustart sofort da.

## 6. Neustart aus Home Assistant (Anforderung 6)

### 6.1 Topic

```
ComfoBox/Command/Restart      (Subscribe, QoS 1)
```

Erlaubte Payloads: `restart`, `RESTART`, `1`.

### 6.2 Discovery als `button`

````json
{
  "name": "Gateway neu starten",
  "unique_id": "comfobox_restart",
  "command_topic": "ComfoBox/Command/Restart",
  "payload_press": "RESTART",
  "device": { "identifiers": ["comfobox"] },
  "origin": { "name": "comfobox-gateway", "sw_version": "1.0.0" }
}
````

In HA erscheint der Button in der **Gerätekarte „ComfoBox"** → Nutzer tippt → Neustart.

### 6.3 Device-Trigger (optional, für Automationen)

Damit Automationen auf einen Neustart reagieren können (z. B. „Benachrichtige mich, wenn die
Heizung neu startet"):

````json
{
  "automation_type": "trigger",
  "type": "device_automation",
  "subtype": "restart",
  "topic": "ComfoBox/Status/Restart",
  "payload": "restarted",
  "device": { "identifiers": ["comfobox"] },
  "origin": { "name": "comfobox-gateway", "sw_version": "1.0.0" }
}
````

Discovery-Topic: `homeassistant/device_automation/comfobox_restart/config`.

Das Gateway publiziert nach einem Neustart `ComfoBox/Status/Restart` → `restarted`.

### 6.4 Ablauf

```
HA-Button gedrückt
  → publish "RESTART" auf ComfoBox/Command/Restart
  → Gateway-Subscriber empfängt
  → Validierung: RestartEnabled? ServiceControl.RestartAsync()
  → publish ComfoBox/Status/Health = "offline"   (LWT wird nicht ausgelöst)
  → Host stoppt kontrolliert (StopAsync)
  → Prozess beendet (exit 0)
  → Docker/systemd startet den Container neu
  → neue Verbindung, LWT neu registriert
  → Discovery + Status werden publiziert
  → publish ComfoBox/Status/Restart = "restarted"
```

> **Sicherheits-Hinweis:** Der Restart-Befehl ist standardmäßig **aktiv**, aber über
> `Gateway:RestartEnabled` abschaltbar. Ein MQTT-Broker ohne Auth (öffentliches Netz) wäre
> sonst ein DoS-Vektor → siehe [MQTT-Sicherheit](#9-sicherheit).

## 7. Status-Entitäten in HA

| Entität | Topic | Typ |
|---|---|---|
| Health | `ComfoBox/Status/Health` | `sensor` (Text) |
| Version | `ComfoBox/Status/Version` | `sensor` |
| Uptime | `ComfoBox/Status/Uptime` | `sensor` (Sekunden) |
| Schreibvorgänge 24 h | `ComfoBox/Status/Writes` | `sensor` |
| BACnet-Status | `ComfoBox/Status/Bacnet` | `sensor` (JSON-Attribute) |
| MQTT-Status | `ComfoBox/Status/Mqtt` | `sensor` (JSON-Attribute) |
| Letzter Fehler | `ComfoBox/Status/LastError` | `sensor` (Text) |

Discovery-Beispiel für einen JSON-Attribut-Sensor:

````json
{
  "name": "BACnet-Verbindung",
  "unique_id": "comfobox_status_bacnet",
  "state_topic": "ComfoBox/Status/Bacnet",
  "value_template": "{{ value_json.state }}",
  "json_attributes_topic": "ComfoBox/Status/Bacnet",
  "entity_category": "diagnostic",
  "device": { "identifiers": ["comfobox"] }
}
````

## 8. Migrationshilfen für bestehende HA-Setups

### 8.1 Bestehende manuelle Entitäten behalten

Wenn der Nutzer die 127 Topics bereits manuell angelegt hat, bleibt alles funktionsfähig:
Die alten Topics existieren unverändert. Die Discovery **ergänzt** sie nur.

> ⚠️ **Achtung – `unique_id`-Konflikte:** Wenn HA eine Entität per Discovery anlegt und eine
> manuell angelegte Entität denselben `entity_id` hat, entstehen Dubletten. Der `unique_id` der
> Discovery ist `comfobox_<pfad>`, die manuell angelegten haben meist keinen.
> → **Empfehlung:** Vor der Umstellung die manuell angelegten MQTT-Entitäten in HA entfernen
> (Settings → Devices & Services → MQTT → Entität → Löschen) und die Discovery übernehmen lassen.

### 8.2 Umschaltung ohne Ausfall

Schrittweise:

| Schritt | Aktion |
|---|---|
| 1 | Neues Gateway starten, `BaseTopic = ComfoBox2` (Testbetrieb parallel) |
| 2 | In HA die Discovery von `ComfoBox2` nutzen (eigene Gerätekarte „ComfoBox (neu)") |
| 3 | Funktionen vergleichen |
| 4 | `BaseTopic` auf `ComfoBox` umstellen → Discovery-Entitäten aktualisieren sich automatisch (gleiche `unique_id`) |
| 5 | Alte manuelle Entitäten löschen |

### 8.3 Bestehende Automationen

Automationen, die auf `ComfoBox/…` publishen, funktionieren **unverändert**, weil:

- Topic-Adressen identisch bleiben,
- Payload-Formate identisch bleiben (Name bzw. Zahl),
- QoS 0 → 1 eine **Aufwärts**-Änderung ist (Bester Empfänger bekommt mindestens QoS 0).

## 9. Sicherheit

| Thema | Maßnahme |
|---|---|
| **Broker ohne Auth** | Der Ist-Zustand unterstützt gar keine Auth → das ist mit MQTTnet gelöst. `Username`/`Password` pro Broker konfigurierbar. |
| **TLS** | `UseTls` pro Broker; optional `AllowUntrustedCertificates` für selbstsignierte Certs im LAN. |
| **Restart als DoS** | `Gateway:RestartEnabled` (Default `true`, abschaltbar). Optional `RestartToken` required. |
| **Expert-Modus** | Abschaltbar per `Gateway:ExpertMode`; Aktivierung erfordert Bestätigung und wird geloggt. |
| **Discovery-Spam** | Discovery wird bei Connect, bei Geburt (`homeassistant/status`) und bei Konfigurationsänderung gesendet – **nicht** im Polling-Takt. |
| **Payload-Größe** | 127 Discovery-Payloads ≈ 60 KB → einmalig beim Start, unkritisch. |

## 10. Validierung

````bash
# Gerätekarte in HA: alle Entitäten vorhanden?
mosquitto_sub -h <broker> -t 'homeassistant/#' -v | grep 'comfobox'

# Health-Topic prüfen
mosquitto_sub -h <broker> -t 'ComfoBox/Status/Health' -v

# Restart-Befehl
mosquitto_pub -h <broker> -t 'ComfoBox/Command/Restart' -m 'RESTART'
````
