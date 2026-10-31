# Architektur (Ist-Zustand)

## 1. Schichtenmodell

```
┌──────────────────────────────────────────────────────────────────────┐
│  ComfoBoxMqttConsole.exe   /   ComfoboxService.exe                    │
│  (Host: AsyncContext.Run + Endlos-Neustartschleife)                  │
└──────────────────────────────┬───────────────────────────────────────┘
                               │
┌──────────────────────────────▼───────────────────────────────────────┐
│  ComfoBoxMqtt (Library)                                              │
│  ComfoBoxMqttClient ─ MQTT-Fassade (M2Mqtt)                          │
│  ItemFactory       ─ Reflection → MqttItem-Liste                    │
│  Models/Items      ─ MqttItem, EnumMqttItem, SpecialMqttItem, ...    │
└───────────────┬──────────────────────────────┬───────────────────────┘
                │                              │
┌───────────────▼──────────────────┐  ┌────────▼───────────────────────┐
│  ComfoBoxLib (Library)           │  │  log4net                       │
│  ComfoBoxClient ─ Wrapper        │  │  Fasterflect (Reflection)      │
│  BacNode       ─ Geräte-Registry │  │  Newtonsoft.Json              │
│  Items/**      ─ 127 Item-Defs   │  └────────────────────────────────┘
│  Values/**     ─ Analog/Enum/... │
└───────────────┬──────────────────┘
                │
┌───────────────▼───────────────────────────────────────────────────────┐
│  BacnetLib (Library, Fremdcode)                                      │
│  BACnetClient.cs (2.370 Z.)  ─ BACnet-Protokoll                       │
│  BACnetTransport.cs (2.260 Z.) ─ MSTP-State-Machine + SerialPort     │
│  BACnetBase.cs (8.200 Z.)      ─ Encoder/Decoder                      │
└──────────────────────────────────────────────────────────────────────┘
```

## 2. Datenfluss eines Werts

### 2.1 Lesen (Poll-Zyklus)

```
ComfoBoxMqttClient.StartPollingAsync()          [ComfoBoxMqttClient.cs:242]
  └─ foreach (var mqttItem in _items)
       └─ await mqttItem.ReadAsync(_comfoBoxClient)
            └─ ItemValue.ReadValueAsync()         [ItemValue.cs:66]
                 └─ await Task.Delay(1)            ← Pseudo-Async
                 └─ client.ReadValue(this)
                      └─ ComfoBoxClient.ReadValue()          [ComfoBoxClient.cs:220]
                           └─ ReadScalarValue(BacnetMasterId, item.BacnetObjectId,
                                            PROP_PRESENT_VALUE)
                                └─ lock(this) → _bacnetClient.ReadPropertyRequest(...)
                                     └─ BACnetClient → BacnetMstpProtocolTransport
                                          └─ MSTP-Thread → System.IO.Ports.SerialPort
            └─ ItemValue.Value setzen
       └─ mqttItem.Publish() → M2Mqtt Publish(topic, payload, qos:0, retain:true)
```

Der Zyklus läuft **fest alle 60 Sekunden** (`Task.Delay(60000)`, [ComfoBoxMqttClient.cs:257]).
Alle 127 Items werden in jedem Durchlauf gelesen – es gibt **keine Priorisierung**
(siehe [befunde.md](befunde.md#a15-priorisierungsattribute-wirkungslos)).

### 2.2 Schreiben (MQTT → BACnet)

```
Broker → M2Mqtt MqttMsgPublishReceived
  └─ ComfoBoxMqttClient.MqttClientOnMqttMsgPublishReceived  [:160]
       └─ _subscriptions[topic](message)
            └─ MqttItem.WriteValueIfChanged(message)        [MqttItem.cs:98]
                 └─ float.Parse(message)   ← kulturabhängig!
                 └─ ItemValue.CheckSetValueConditions(value)  ← Min/Max/Null-Prüfung
                 └─ ComfoBoxClient.WriteValue(item, value)
                      └─ WriteScalarValue(...) → _bacnetClient.WritePropertyRequest(...)
                      └─ CurrentNumberOfWrites++              ← EEPROM-Schutz
```

### 2.3 Item-Erzeugung (Reflection)

```
ItemFactory.CreateItems()                        [ItemFactory.cs:29]
  └─ für Zone, Tests, Warmwater, States, Time, Config:
       └─ CreateItemsFromClass(list, classType, topicName, ...)
            ├─ verschachtelte Klassen rekursiv
            ├─ classType.CreateInstance()               (Fasterflect)
            ├─ classType.Properties(Flags.Default)      (Fasterflect)
            └─ je Property:
                 ├─ propertyValue = instance.GetPropertyValue(name) as IItemValue
                 ├─ Typ-Dispatch:
                 │    ICommandValue → CommandMqttItem
                 │    IEnumValue    → EnumMqttItem
                 │    sonst         → MqttItem
                 ├─ [Description] → item.Description
                 └─ [ExpertMode]  → item.ExpertMode = true
```

Jede Item-Property ist expression-bodied (`=> new AnalogValue(830)`) – **jeder Property-Zugriff
erzeugt ein neues Objekt**. `ItemFactory` greift genau einmal pro Property zu und hält das
Ergebnis in `MqttItem.ItemValue` fest.

### 2.4 BACnet-Objekt-Modell

Alle 127 Items sind BACnet-Objekte vom Typ **`OBJECT_ANALOG_VALUE`**. Es gibt keine
Unterscheidung nach Objekttyp, nur nach Instanznummer (z. B. 830, 3078, 62).

| Klasse | Topic-Pfad | Typ | Schreibbar |
|---|---|---|---|
| `Zone` | `ComfoBox/Zone/…` | Enum / Analog | teilweise |
| `States` | `ComfoBox/States/…` | Enum / Analog | nein |
| `Tests` | `ComfoBox/Tests/…` | Enum | nein |
| `Warmwater` | `ComfoBox/Warmwater/…` | Enum / Analog | teilweise |
| `Time` | `ComfoBox/Time/…` | – | – (leer) |
| `Config` | `ComfoBox/Config/…` | Enum / Analog | nur mit ExpertMode |

## 3. Threading-Modell

| Thread | Ort | Beschreibung |
|---|---|---|
| **MSTP-Thread** | [BACnetTransport.cs:1495](https://github.com/RF77/comfobox-mqtt) `Start()` | `System.Threading.Thread`, `IsBackground=true`, `Name="MSTP Thread"`, `Priority=ThreadPriority.Highest` |
| **Poll-Loop** | `ComfoBoxMqttClient.StartPollingAsync()` | async Task auf Thread-Pool |
| **MQTT-Event-Thread** | M2Mqtt | `MqttMsgPublishReceived` |
| **Write-Counter-Timer** | `ComfoBoxClient.StartTimer()` | `async void`, 24-h-Intervall |
| **Host-Schleife** | Console/Service | `AsyncContext.Run` + `while(true)` |

### Synchronisation

| Stelle | Mechanismus | Bemerkung |
|---|---|---|
| `ComfoBoxClient.ReadScalarValue` / `WriteScalarValue` | `lock (this)` | **global** serialisiert – kein Parallel-Read |
| `DeviceAddr()` | `lock (_devicesList)` | |
| `OnIam()` (MSTP-Thread) | `lock (_devicesList)` | |
| `BacnetClient.LastSegmentACK` | `Monitor.Enter/Exit` + `ManualResetEvent` | [BACnetClient.cs:77](https://github.com/RF77/comfobox-mqtt) |
| `m_invoke_id++` | **keine** | funktioniert nur, weil `lock(this)` alles serialisiert |

### Statische, prozessweite Zustände

```csharp
// ComfoBoxClient.cs:27-28
public static int  CurrentNumberOfWrites;
public static bool TimerIsRunning;
```

Der 24-h-Schreibzähler ist **nicht persistent** – ein Service-Neustart setzt die Grenze zurück
(siehe [befunde.md](befunde.md#a3-schreibzähler-nicht-persistent)).

## 4. Schichten und ihre Kopplung

Die Schichten sind über **Callbacks** gekoppelt, nicht über Interfaces:

```
ItemFactory ──> MqttItem ──> ComfoBoxMqttClient (Subscribe + Publish)
                                   ▲
                                   │ Func<ComfoBoxClient> comfoBoxClientFunc
ComfoBoxClient ────────────────────┘
```

`ItemFactory.CreateItems(this, () => _comfoBoxClient)` übergibt einen `Func<>`, obwohl ein
einfacher Methoden- oder Property-Zugriff ausreichen würde. Die Items kennen den
`ComfoBoxMqttClient` (für `Publish`/`On`) **und** den `ComfoBoxClient` – dadurch entsteht ein
Zykklus zwischen den beiden Bibliotheken, der nur durch die Reihenfolge in `StartAsync` aufgelöst wird.

## 5. Fehlerverhalten

```
Exception in ReadScalarValue
  └─ propagiert durch ItemValue.ReadValueAsync
       └─ StartPollingAsync: catch { _comfoBoxClient.Dispose(); throw; }
            └─ Host-Schleife: catch { log; sleep; } → kompletter Neustart
```

Ein einziger BACnet-Fehler (z. B. USB-Adapter gezogen) zerstört damit den **gesamten** Gateway inkl.
MQTT-Verbindung und Startet alles neu. Details in [befunde.md](befunde.md#a7-ein-bacnet-fehler-killt-den-gateway).

## 6. Was der Protokollstack mitbringt (und nicht braucht)

`src/Bacnet` enthält **vier** Transport-Implementierungen:

| Transport | Klasse | Wird genutzt? | Portierungsrisiko |
|---|---|---|---|
| Serial (MSTP) | `BacnetSerialPortTransport` | **ja** | mittel (`System.IO.Ports` → NuGet) |
| Named Pipe | `BacnetPipeTransport` | nein | hoch (P/Invoke `kernel32.dll`, Windows-only) |
| BACnet/IP (UDP) | `BacnetIpUdpProtocolTransport` | nein | hoch (`UdpClient.BeginReceive` existiert in .NET Core nicht mehr) |
| PTP | `BacnetPtpProtocolTransport` | nein | mittel |

Siehe [bacnet-portierung.md](../02-modernisierung/bacnet-portierung.md) – die Empfehlung ist,
nur MSTP/Serial zu portieren und den Rest zu verwerfen.

## 7. Unbenutzter Code (Dead Code)

| Element | Ort | Anmerkung |
|---|---|---|
| `InitMqttClient()` | [ComfoBoxMqttClient.cs:57](https://github.com/RF77/comfobox-mqtt) | einzige Stelle mit TLS/User/Pass – **nie aufgerufen** |
| `CreateVirtualItems()` | [ComfoBoxMqttClient.cs:178](https://github.com/RF77/comfobox-mqtt) | auskommentiert in `StartAsync` → `ComfoBox/Virtual/*` existieren nicht |
| `CommandMqttItem` / `ICommandValue` | `Models/Items/` | `CommandValue` wird nirgends instanziiert |
| `ReadMultipleValues()` | [ComfoBoxClient.cs:190](https://github.com/RF77/comfobox-mqtt) | wirft `NotImplementedException` |
| `_specialItems` | [ComfoBoxMqttClient.cs:44](https://github.com/RF77/comfobox-mqtt) | Feld wird nie gelesen |
| `MqttQoS` | Settings | definiert, nie verwendet |
| `Primary*/Secondary*RefreshRate` | Settings | definiert, nie verwendet |
| `SpecialSubTopic` / `VirtualTopic` | Settings | `SpecialSubTopic` wird nicht gelesen, `VirtualTopic` vom toten Code |
