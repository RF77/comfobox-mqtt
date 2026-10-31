---
description: "Use when changing MQTT publishing, subscribing, topic mapping, payloads, Home Assistant discovery, availability, health/status topics, or the restart command in this repository."
applyTo: ["src/ComfoBox.Gateway/Mqtt/**/*.cs", "src/ComfoBoxMqtt/**/*.cs", "src/ComfoBox.Domain/**/*.cs"]
---

# MQTT & Home Assistant – Regeln

## 1. Library

**MQTTnet 5** (`MQTTnet` 5.2.0.1603). M2Mqtt ist tot und wird **nicht** verwendet.

| Ist (M2Mqtt – verboten) | Soll (MQTTnet 5) |
|---|---|
| `new MqttClient(host)` | `new MqttClientFactory().CreateMqttClient()` |
| `client.Connect(id, user, pass, clean, keepAlive, willFlag, willTopic, willQos, willRetain, willDelay)` | `client.ConnectAsync(MqttClientOptions, ct)` |
| `client.Disconnect()` | `await client.DisconnectAsync(new MqttClientDisconnectOptions(), ct)` |
| `client.MqttMsgPublishReceived += h` | `client.ApplicationMessageReceivedAsync += h` |
| `client.ConnectionClosed += h` | `client.DisconnectedAsync += h` |
| `client.Subscribe(topics, qos)` | `await client.SubscribeAsync(opts, ct)` |
| `client.Publish(topic, bytes, qos, retain)` | `await client.PublishAsync(MqttApplicationMessage, ct)` |

## 2. Topic-Vertrag (unverhandelbar)

```
{BaseTopic}                                        Default: "ComfoBox"
├── /Connected                                     "1" | "0"        (Kompatibilität!)
├── /Version                                       "Ver 1.0.0"     (Kompatibilität!)
├── /Special/NumberOfWritesPer24h                  "42"
├── /Status/…                                      NEU (Health, Version, Uptime, …)
├── /Command/Restart                                NEU (Subscribe)
└── /{Group}/{Name}
    ├── {Topic}                     Publish (retained)  → Wert
    ├── {Topic}/Set                 Subscribe           → Schreibwert
    ├── {Topic}/AsNumber            Publish (retained)  → Enum als Zahl
    └── {Topic}/AsNumber/Set        Subscribe           → Enum als Zahl schreiben
```

**Regeln:**

1. `BaseTopic` bleibt `ComfoBox`.
2. `Group`/`Name` sind **exakt** die bisherigen Topic-Segmente (Großbuchstaben!).
3. Neue Topics **nur** unter `ComfoBox/Status/` und `ComfoBox/Command/`.
4. Kein `Virtual/`-Topic – es gab nie einen.
5. Enum-**Namen** nicht umbenennen.
6. Der Contract-Test gegen `docs/topics.json` muss grün bleiben.

## 3. Payload-Format

| Typ | Format | Beispiel |
|---|---|---|
| Analog | Zahl mit **Punkt** | `21.5` |
| Enum (State) | Enum-**Name** | `Normal` |
| Enum (AsNumber) | Ganzzahl | `6` |
| Connected | `1` / `0` | |
| Version | `Ver <x.y.z>` | |
| Status/Health | `online` / `degraded` / `offline` | |
| Status/* | JSON | |

### InvariantCulture (Pflicht)

````csharp
// ✅
public static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
public static bool TryParse(string payload, out float value)
    => float.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

// ❌ kulturabhängig → auf de-DE kaputt (Befund A5)
float.Parse(message);
value.ToString();
````

### Enum-Parsing (kein 0-Fallback!)

````csharp
// ✅
if (!Enum.TryParse(enumType, text, ignoreCase: true, out var parsed) || parsed is null)
{
    // ablehnen + Fehler melden — NIEMALS 0 schreiben
}

// ❌ schreibt bei ungültiger Eingabe den Wert 0 (Befund A6)
float.TryParse(message, out newValue);
newValue = Convert.ToInt32(Enum.Parse(enumType, message));
````

## 4. QoS, Retain, LWT

| Aspekt | Wert | Begründung |
|---|---|---|
| Publish-QoS (Items) | 0 | hohe Frequenz, QoS 0 reicht |
| Publish-QoS (Discovery, Status) | 1 | wichtiger, einmalig |
| Subscribe-QoS (`/Set`) | 1 | QoS 2 = 4-Part-Handshake ohne Nutzen |
| Retain (Items) | ja (`UseMqttRetain`) | HA-Entities behalten ihren State |
| Retain (Discovery) | **ja** | sonst fehlen Entitäten nach HA-Neustart |
| Keepalive | **30 s** | nicht 2 s (Befund A24) |
| LWT-Topic | `ComfoBox/Status/Health` + `ComfoBox/Connected` | Availability |
| LWT-Payload | `offline` bzw. `0` | |
| Protokollversion | MQTT 5 | HA empfiehlt v5 |

> **QoS-Kompatibilität:** QoS wird vom **Empfänger** bestimmt. Ein Subscriber mit QoS 1
> bekommt QoS-0-Publishes als QoS 0. Der Wechsel von „Subscribe 2 / Publish 0" zu
> „Subscribe 1 / Publish 0|1" ist kompatibel.

## 5. Verbindungs-Management

````csharp
// ✅ Reconnect mit Backoff + Cancellation
var delay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Min(attempt, 6))));
await Task.Delay(delay, ct);

// ❌ Rekursion ohne Backoff/Cancellation (Befund A9)
catch (Exception ex) { await ReconnectAsync(); }
````

| Regel | Grund |
|---|---|
| `Connected=1` bei **jedem** Connect publizieren | Befund A10 – HA sah dauerhaft „offline" |
| Beim orderly Shutdown erst `offline` publishen, dann trennen | LWT wird sonst **nicht** ausgelöst |
| `CancellationToken` im gesamten Reconnect-Pfad | Befund A9/A11 – der Service reconnectete beim Stoppen |
| Broker-Liste: jeder Broker separat konfigurierbar (Host, Port, User, Pass, TLS) | Befund A25 |
| Fehler eines Brokers darf andere Broker nicht blockieren | |
| Publish ohne Verbindung → **verwerfen** + loggen, nicht werfen | |

## 6. Home-Assistant-Discovery

| Discovery-Topic | Plattform |
|---|---|
| `homeassistant/device/comfobox/config` | Gerät |
| `homeassistant/sensor/comfobox_<id>/config` | Read-only-Werte |
| `homeassistant/number/comfobox_<id>/config` | schreibbare Analogwerte |
| `homeassistant/select/comfobox_<id>/config` | schreibbare Enumwerte |
| `homeassistant/button/comfobox_restart/config` | Neustart |
| `homeassistant/sensor/comfobox_health/config` | Health |

**Regeln:**

1. Jede Discovery-Payload enthält `device.identifiers = ["comfobox"]` → **eine** Gerätekarte.
2. Jede Discovery-Payload enthält `origin` (`name`, `sw_version`) → Pflicht für Device-Discovery.
3. `availability_topic` = `ComfoBox/Status/Health`, `payload_available` = `online`,
   `payload_not_available` = `offline`.
4. Discovery-Payloads werden **retained** publiziert.
5. Discovery wird gesendet: beim Connect, bei `homeassistant/status` = `online`
   (Geburt der HA-MQTT-Integration) und bei Konfigurationsänderungen.
   **Nicht** im Poll-Takt.
6. `unique_id` = `comfobox_<pfad>` – stabil, damit HA die Entität nicht dupliziert.
7. Zum Entfernen: leere Payload auf das Discovery-Topic.

## 7. Neustart

| Element | Wert |
|---|---|
| Topic | `ComfoBox/Command/Restart` (Subscribe, QoS 1) |
| Payloads | `restart`, `RESTART`, `1` |
| REST | `POST /api/service/restart` → 202 |
| Signal-Topic | `ComfoBox/Status/Restart` → `restarted` |

**Ablauf:** `offline` publishen → Host kontrolliert stoppen (`StopApplication`) → Supervisor
startet neu → Discovery + `online` publishen → `restarted` publishen.

> **Kein** `docker restart` aus dem Container (kein Docker-Socket) und **kein** `systemctl`.

## 8. Health & Status

| Topic | Payload | Retain |
|---|---|---|
| `ComfoBox/Status/Health` | `online` / `degraded` / `offline` | ja |
| `ComfoBox/Status/Version` | `1.0.0` | ja |
| `ComfoBox/Status/Uptime` | Sekunden | ja |
| `ComfoBox/Status/Writes` | Anzahl im 24-h-Fenster | ja |
| `ComfoBox/Status/Bacnet` | JSON | ja |
| `ComfoBox/Status/Mqtt` | JSON | ja |
| `ComfoBox/Status/LastError` | Text | ja |
| `ComfoBox/Status/Errors` | JSON-Array (letzte 50) | nein |

**Semantik:**

| Zustand | Bedeutung |
|---|---|
| `online` | BACnet **und** MQTT verbunden, Polling läuft |
| `degraded` | mindestens eine Komponente defekt (Werte sind `stale`) |
| `offline` | MQTT getrennt (LWT) oder Prozess beendet |

> **Benutzerfehler** (`value_out_of_range`, `expert_mode_required`) verschlechtern den
> Health-Status **nicht** – der Gateway läuft ja.

## 9. Fehlerbehandlung

| Fehler | Aktion |
|---|---|
| Ungültige Payload auf `/Set` | ablehnen, `Status/Errors`, **nicht** schreiben |
| Wert außerhalb Min/Max | ablehnen + Fehler |
| Item read-only | ablehnen + Fehler |
| Expert-Modus fehlt | ablehnen + Fehler |
| BACnet-Busfehler | Health `degraded`, Fehler |
| Broker nicht erreichbar | Health `degraded`, Backoff-Reconnect |

> **Jeder** abgelehnte Schreibvorgang erzeugt einen Eintrag in `Status/Errors` → die UI und HA
> sehen denselben Fehlerkanal.

## 10. Tests

| Test | Prüft |
|---|---|
| `EveryLegacyItem_IsPresentInTheCatalog` | Topic-Existenz |
| `EveryLegacyItem_HasTheSameSetTopic` | Schreib-Topic-Existenz |
| `Payload_Parses_RegardlessOfCurrentCulture` | InvariantCulture |
| `InvalidEnumPayload_DoesNotWriteZero` | kein 0-Fallback |
| `Connected_IsPublishedOnEveryConnect` | Regression A10 |
| `Lwt_IsConfiguredForHealthTopic` | Availability |
| `DiscoveryPayload_MatchesSchema` | HA-Schema |

> Die Fixture `docs/topics.json` ist **unveränderlich** zu halten.
