# Phase 3 – MQTT-Migration & HA-Discovery (6 PT)

**Ziel:** Der Gateway publiziert alle 127 Topics und meldet sich bei Home Assistant an.
**Braucht:** Phase 2
**Spec:** [mqtt-library-migration](../02-modernisierung/mqtt-library-migration.md) ·
[mqtt-kompatibilitaet](../04-migration/mqtt-kompatibilitaet.md) ·
[home-assistant](../02-modernisierung/home-assistant.md)

> ⚠️ **Der Topic-Vertrag ist unverhandelbar.** Der Contract-Test gegen
> `docs/topics.json` muss grün werden und **bleibt** grün. Erreicht er diese
> Phase nicht, wird sie nicht verlassen.

---

## 3.1 – MQTTnet-Client · 1.5 PT

| | |
|---|---|
| **Quelle** | `src_old/ComfoBoxMqtt/ComfoBoxMqttClient.cs` (M2Mqtt) |
| **Ziel** | `src/ComfoBox.Gateway/Mqtt/` |
| **Bibliothek** | **MQTTnet 5** – M2Mqtt wird **nicht** verwendet |

| Komponente | Aufgabe |
|---|---|
| `MqttConnectionFactory` | Verbindungsoptionen, TLS, Auth, Keepalive |
| `MqttConnectionManager` | Connect/Disconnect/Reconnect mit Backoff |
| `MqttPublisher` | Publizieren mit QoS/Retain |

**Pflichtwerte:**

| Aspekt | Wert | Grund |
|---|---|---|
| Keepalive | **30 s** | Befund A24: 2 s war der Broker-Killer |
| Protokoll | MQTT 5 | HA empfiehlt v5 |
| QoS Publish | 0 | hohe Frequenz |
| QoS Discovery | 1 | wichtig, einmalig |
| QoS Subscribe | 1 | QoS 2 bringt nichts |
| Retain Items | ja | HA behält den Zustand |
| Retain Discovery | **ja** | sonst fehlen Entitäten nach HA-Neustart |
| LWT | `Status/Health` + `Connected` | Availability |

**Akzeptanzkriterien:**
- [ ] MQTTnet 5 im Einsatz, M2Mqtt **vollständig** entfernt
- [ ] TLS, Username/Passwort und Port **pro Broker** konfigurierbar
- [ ] Reconnect mit Backoff **und** `CancellationToken`
- [ ] **Nach Reconnect wird `Connected=1` publiziert** (Regressionstest A10)
- [ ] LWT auf `Status/Health` und `Connected` registriert
- [ ] Sauberer Shutdown: erst `offline` publishen, **dann** trennen (sonst feuert der LWT)

---

## 3.2 – Subscriber für `/Set` · 0.5 PT

| | |
|---|---|
| **Topics** | `{BaseTopic}/{Group}/{Name}/Set` und `…/AsNumber/Set` |

| Regel | Grund |
|---|---|
| Nur `/Set`-Topics abonnieren | Schreibzugriffe von außen |
| `Connected=1` bei **jedem** Connect | Befund A10 |

**Akzeptanzkriterien:**
- [ ] Schreiben per `/Set` und `/AsNumber/Set` funktioniert
- [ ] Ein Schreibvorgang erhöht den Schreibzähler (der Schreibschutz darf **nicht** umgangen werden)

---

## 3.3 – Payload-Format & Enum-Parsing · 0.5 PT

| | |
|---|---|
| **Befunde** | **A5** (kulturabhängig), **A6** (0-Fallback) |

````csharp
// ✅ richtig
public static string Format(float value) => value.ToString("0.###", CultureInfo.InvariantCulture);
public static bool TryParse(string payload, out float value)
    => float.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

// ✅ Enum: ablehnen statt 0 schreiben
if (!Enum.TryParse(enumType, text, ignoreCase: true, out var parsed) || parsed is null)
{
    // ablehnen + Fehler melden — NIEMALS 0 schreiben
}

// ❌ verboten (Befund A6)
float.TryParse(message, out newValue);
newValue = Convert.ToInt32(Enum.Parse(enumType, message));
````

**Akzeptanzkriterien:**
- [ ] Alle Zahlen-Payloads **invariant** (Regressionstest vorhanden)
- [ ] Eine ungültige Enum-Payload schreibt **keinen** 0-Wert (Regressionstest A6)
- [ ] Ein Test läuft mit `de-DE`-Kultur und besteht

---

## 3.4 – LWT, `Connected`, `Version` · 0.5 PT

| | |
|---|---|
| **Kompatibilität** | Diese Topics sind **Altbestand** – sie bleiben unverändert |

| Topic | Payload | Anmerkung |
|---|---|---|
| `ComfoBox/Connected` | `1` / `0` | **Kompatibilität!** |
| `ComfoBox/Version` | `Ver 1.0.0` | **Kompatibilität!** |
| `ComfoBox/Special/NumberOfWritesPer24h` | `42` | **Kompatibilität!** |

**Akzeptanzkriterien:**
- [ ] Alle drei Topics unverändert vorhanden
- [ ] `Connected=1` bei jedem Connect, `0` beim Shutdown
- [ ] Der Contract-Test prüft die **exakten** Payloads

---

## 3.5 – Home-Assistant-Discovery · 1.5 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Gateway/Mqtt/HomeAssistantDiscovery.cs` |

**Discovery-Payloads** (MQTT-Standard, unverändert):

````json
{
  "name": "Raumsollwert Normal",
  "unique_id": "comfobox_zone_setpointnormal",
  "device": {
    "identifiers": ["comfobox1"],
    "manufacturer": "Zehnder",
    "model": "ComfoBox Series 5 (ELESTA)",
    "sw_version": "1.0.0"
  },
  "state_topic": "ComfoBox/Zone/SetPointNormal",
  "command_topic": "ComfoBox/Zone/SetPointNormal/Set",
  "unit_of_measurement": "°C",
  "min": 5,
  "max": 35,
  "device_class": "temperature"
}
````

| Item-Typ | HA-Komponente |
|---|---|
| Zahl, nicht schreibbar | `sensor` |
| Zahl, schreibbar | `number` |
| Auswahl, schreibbar | `select` |
| Auswahl, nicht schreibbar | `sensor` mit `value_template` |

**Akzeptanzkriterien:**
- [ ] Discovery für **alle** 127 Items
- [ ] Payloads sind valides JSON und erfüllen das HA-Schema
- [ ] **Kein** `Virtual`-Topic im Katalog
- [ ] Enum-Namen **unverändert** (nicht übersetzt)

---

## 3.6 – Health-/Status-Publisher · 1.0 PT

| | |
|---|---|
| **Ziel** | `src/ComfoBox.Gateway/Health/` |

| Topic | Inhalt |
|---|---|
| `ComfoBox/Status/Health` | `online` / `degraded` / `offline` |
| `ComfoBox/Status/Version` | `1.0.0` |
| `ComfoBox/Status/Uptime` | Sekunden |
| `ComfoBox/Status/Errors` | letzter Fehler (JSON) |

> **Fehler dürfen nicht verschluckt werden** – jeder Fehler landet in **drei**
> Kanälen: Log, `Status/Errors` und UI.

**Akzeptanzkriterien:**
- [ ] `Health` wechselt korrekt zwischen den drei Zuständen
- [ ] Jeder Fehler erscheint in Log **und** `Status/Errors`
- [ ] Beim Shutdown wird `offline` publiziert

---

## Abnahme der Phase 3

- [ ] **Contract-Test grün**: alle Topics aus `docs/topics.json` werden publiziert bzw. abonniert
- [ ] Discovery-Payloads erscheinen in Home Assistant
- [ ] `ComfoBox/Connected` und `ComfoBox/Version` unverändert
- [ ] Meilenstein **M2** erreicht: „Gateway publiziert alle 127 Topics + Discovery“

→ Weiter mit [Phase 4](phase-4.md)
