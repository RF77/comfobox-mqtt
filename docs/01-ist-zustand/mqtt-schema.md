# MQTT-Schema (Ist-Zustand)

> **Dieses Dokument ist der Kompatibilitätsvertrag.** Anforderung 4 der Modernisierung lautet:
> *„MQTT-Adressen sollen gleich bleiben."* Die hier beschriebenen Adressen und Payloads müssen
> nach der Migration **unverändert** gültig sein.
> Siehe [../04-migration/mqtt-kompatibilitaet.md](../04-migration/mqtt-kompatibilitaet.md).

## 1. Topic-Baum

```
{BaseTopic}                                       Default: "ComfoBox"
│
├── /Connected                                    "1" | "0"        (Publish, retained via LWT)
├── /Version                                      "Ver 0.5.0"     (Publish, nicht retained)
│
├── /Special/NumberOfWritesPer24h                 "42"             (Publish, retained)
│
├── /Virtual/IsHeatingMode                        ⚠ NICHT VORHANDEN (Code auskommentiert)
├── /Virtual/IsCoolingMode                        ⚠ NICHT VORHANDEN
├── /Virtual/IsHeatingWithHeatPump                ⚠ NICHT VORHANDEN
├── /Virtual/WarmWater                            ⚠ NICHT VORHANDEN
│
└── /{ItemKlasse}/{Unterklasse?}/{Property}
    ├── {Topic}                    (Publish, retained)   → Wert
    ├── {Topic}/Set                (Subscribe, QoS 2)     → Schreibwert
    ├── {Topic}/AsNumber           (Publish, retained)   → Enum als Zahl   [nur Enums]
    └── {Topic}/AsNumber/Set       (Subscribe, QoS 2)     → Enum als Zahl schreiben [nur Enums]
```

`BaseTopic` ist konfigurierbar (`BaseTopic`, Default `ComfoBox`). **Die Standard-Belegung
`ComfoBox` ist Teil des Vertrags.**

## 2. Item-Klassen im Topic

| Klasse | Beispiel-Topic | Wertetyp | Schreibbar |
|---|---|---|---|
| `Zone` | `ComfoBox/Zone/OperationMode` | Enum | ja |
| | `ComfoBox/Zone/HeatingCoolingMode` | Enum | ja |
| | `ComfoBox/Zone/SetPointNormal` | Analog (Min 5 / Max 30, Unit °C) | ja |
| `States` | `ComfoBox/States/HeatPump/HeatPumpStatus` | Enum | nein |
| `Tests` | `ComfoBox/Tests/…` | Enum | nein |
| `Warmwater` | `ComfoBox/Warmwater/…` | Enum / Analog | teilweise |
| `Time` | `ComfoBox/Time/…` | – | – (Klasse leer) |
| `Config` | `ComfoBox/Config/General/Sommerknick` | Enum | nur ExpertMode |

Vollständige Liste: [`docs/topics.md`](../topics.md) (127 Einträge),
maschinenlesbar: [`docs/topics.json`](../topics.json).

## 3. Payload-Formate

| Topic-Typ | Payload | Beispiel | Anmerkung |
|---|---|---|---|
| Analog | `float.ToString()` | `23.5` | **kulturabhängig!** auf de-DE → `23,5` |
| Analog, ganzzahlig | `float.ToString()` | `21` | |
| Enum (Haupt-Topic) | Enum-Name | `Normal` | |
| Enum (`/AsNumber`) | `Convert.ToInt32(...).ToString()` | `6` | |
| Date | – | – | `DateValue.ConvertValue` ist leer → **nie publiziert** |
| Special | `int?.ToString()` | `42` | |
| Connected | `"1"` / `"0"` | | |
| Version | `Settings.Version` | `Ver 0.5.0` | |

### Enum-Werte im Klartext

| Enum | Werte |
|---|---|
| `OperationModes` | 7 Werte |
| `HeatingCoolingModes` | 4 Werte |
| `HeatPumpStatusEnums` | ~60 Werte, mit `[Description]` |
| `KondensatFrostschutzEnum` | 6 Werte |

Die Namen sind englisch/gemischt (z. B. `HeatingWithHeatPump`, `KondensatFrostschutzEnum`).
Sie sind Teil des Vertrags und dürfen **nicht** umbenannt werden.

## 4. QoS, Retain, LWT

| Aspekt | Wert | Ort |
|---|---|---|
| Publish-QoS | **0** (fest kodiert) | [ComfoBoxMqttClient.cs:296](https://github.com/RF77/comfobox-mqtt) |
| Subscribe-QoS | **2** (`QOS_LEVEL_EXACTLY_ONCE`) | [ComfoBoxMqttClient.cs:271](https://github.com/RF77/comfobox-mqtt) |
| QoS-Mismatch | Subscribe 2 / Publish 0 → effektiv 0 | |
| Retain (Items) | **ja**, via `UseMqttRetain` (Default `true`) | [MqttItem.cs:139](https://github.com/RF77/comfobox-mqtt) |
| Retain (`Connected`, `Version`) | nein | |
| Retain (LWT) | ja | |
| LWT-Topic | `{BaseTopic}/Connected` | |
| LWT-Payload | `"0"` | |
| LWT-Delay | 60 s (`willDelayInterval`) | |
| keepAlivePeriod | **2 s** | [ComfoBoxMqttClient.cs:89](https://github.com/RF77/comfobox-mqtt) |

> Der 2-Sekunden-Keepalive ist extrem aggressiv: der Broker trennt bei Inaktivität nach ~4 s.

## 5. Verbindungsparameter

```csharp
// ComfoBoxMqttClient.cs:49  — Konstruktor
_mqttClients = brokerHostNames.Select(i => new MqttClient(i)).ToArray();

// ComfoBoxMqttClient.cs:89  — Connect
client.Connect($"ComfoBox{DateTime.Now.Ticks}", null, null, true, 2, true,
               $"{Settings.Default.BaseTopic}/Connected", "0", true, 60);
```

| Parameter | Wert | Bewertung |
|---|---|---|
| Client-ID | `ComfoBox{DateTime.Ticks}` | unüblich, besser `Guid` |
| Username/Password | `null, null` | **keine Authentifizierung** |
| Port | 1883 (Default) | nicht konfigurierbar im genutzten Pfad |
| TLS | **nein** | `MqttSslProtocols.None` nur im toten `InitMqttClient` |
| cleanSession | `true` | |
| Broker-Liste | `MqttBrokerAddresses` (StringCollection) | Multi-Broker strukturell vorhanden |

## 6. Multi-Broker-Verhalten

`_mqttClients` ist ein Array – jeder Broker bekommt denselben Client.
`ConnectMqtt()` verbindet **alle** nicht-verbundenen Clients.
`Publish()` publiziert an alle verbundenen Clients.

> **Nicht implementiert:** Broker-spezifische Einstellungen (Port, User, TLS) pro Broker.
> Die Konfiguration ist ein flaches Adress-Array.

## 7. Schreibpfade und ihre Konventionen

| Aktion | Topic | erwartete Payload |
|---|---|---|
| Analogwert schreiben | `{Topic}/Set` | Zahl, z. B. `21.5` |
| Enum als Name schreiben | `{Topic}/Set` | Name, z. B. `Normal` |
| Enum als Zahl schreiben | `{Topic}/AsNumber/Set` | Zahl, z. B. `6` |

### Bekannte Schreibfehler (dokumentiert, im Ist-Zustand aktiv)

```csharp
// EnumMqttItem.cs:91-98
float newValue;
if (float.TryParse(message, out newValue))
{
    newValue = Convert.ToInt32(Enum.Parse(enumValue.GetEnumType(), message));
}
```

Bei einer ungültigen Payload (z. B. `Normal` auf `/AsNumber/Set`) ist `float.TryParse` → `false`,
`newValue` bleibt `0` → es wird der **Wert 0 geschrieben**.

```csharp
// MqttItem.cs:110
float.Parse(message)   // nutzt CurrentCulture
```

Auf einem System mit `de-DE`-Kultur ist `23.5` nicht parsebar → der Schreibvorgang wird
verworfen; `23,5` wird zu `235` interpretiert.

> Beide Fehler werden im Rahmen der Migration behoben – **ohne** das Topic-Schema zu ändern.
> Die Vertrags-relevanten Payloads (Name oder Zahl, Punkt als Dezimaltrenner) bleiben identisch,
> aber die **Interpretation** wird kultur-invariant. Details in
> [../04-migration/mqtt-kompatibilitaet.md](../04-migration/mqtt-kompatibilitaet.md#payload-interpretation).

## 8. Retention-Semantik

Alle Item-Werte werden **retained** publiziert. Nach einem Broker- oder Gateway-Neustart stehen
die letzten Werte sofort zur Verfügung.

> **Caveat (im Ist-Zustand):** Nach einem MQTT-Reconnect wird `Connected=1` **nicht** erneut
> publiziert (nur beim allerersten Connect). Home Assistant sieht den Gateway daher dauerhaft
> als offline. Siehe [befunde.md](befunde.md#a10-nach-reconnect-kein-connected1-publiziert).

## 9. Discovery / Topic-Liste

Es gibt **keine Laufzeit-Discovery**. Die Topic-Liste wird ausschließlich per Reflection aus dem
Objektmodell erzeugt und im **DEBUG-Build** als Datei geschrieben:

| Datei | Inhalt |
|---|---|
| [`docs/topics.md`](../topics.md) | 127 Items, Markdown, mit Min/Max/Unit/Beschreibung |
| [`docs/topics.json`](../topics.json) | dieselben Items als JSON-Metadaten |

```csharp
// ComfoBoxMqttClient.cs:104
#if DEBUG
if (Settings.Default.WriteTopicsToFile) { … }
#endif
```

Im Release-Build (Service/Installer) werden die Dateien **nicht** erzeugt.
