# Hardware & BACnet (Ist-Zustand)

## 1. Physische Anbindung

```
  ComfoBox ELESTA-Controller                USB2RS485-Adapter (CH340)
  ┌──────────────────────────┐              ┌────────────────────────┐
  │  R (+ rot)  ─────────────┼──────────────┤  A (Data+)             │
  │  B (blau)  ─────────────┼──────────────┤  B (Data-)             │
  └──────────────────────────┘              └────────────────────────┘
                                            │  /dev/ttyUSB0
                                            ▼
                                     ComfoBoxMqttConsole
```

Die Verdrahtung erfolgt **parallel** an den rot markierten Pins der ComfoBox-Steuerung
(siehe `docs/images/Elesta.png`). Ein USB-RS485-Adapter mit **CH340**-Chip ist der getestete
Aufbau.

> **Achtung:** Der Adapter muss **parallel** (nicht seriell in Reihe) geschaltet werden, da die
> ComfoBox den Bus selbst mit terminiert.

## 2. Serielle Parameter

Gesetzt in [`BACnetTransport.cs:400`](https://github.com/RF77/comfobox-mqtt):

```csharp
m_port = new Ports.SerialPort(m_port_name, m_baud_rate,
                               System.IO.Ports.Parity.None, 8, Ports.StopBits.One);
```

| Parameter | Wert | Konfigurierbar? |
|---|---|---|
| Baudrate | **38400** (Default) | ja, `Baudrate` |
| Parität | `None` | **nein – hart verdrahtet** |
| Datenbits | 8 | nein |
| Stopbits | 1 | nein |
| Handshake | `None` | nein |
| Read-Timeout | wird pro `Read()` gesetzt | nein |

### Baudrate — kritischer Punkt

Laut README funktionierte am Raspberry Pi **nur 38400**. Die ComfoBox muss dafür vom
Werks-Default **76800 auf 38400 umkonfiguriert und neu gestartet** werden.

| Ort | Wert |
|---|---|
| `ComfoBoxLib/Properties/Settings.settings` | `Baudrate` = 38400 |
| `ComfoBoxLib/app.config` | 38400 |
| DemoClient | hart kodiert auf **76800** (`MainViewModel.cs:35`) |

> Der DemoClient nutzt 76800, das Gateway 38400 – das ist kein Zufall, sondern der
> Faktor „Was war zuerst eingestellt".

## 3. BACnet MSTP

### Protokoll

BACnet MS/TP (Master-Slave/Token-Passing) über RS485. Das Gateway ist **Master**,
die ComfoBox ist **Slave** mit Device-ID **1** (`BacnetMasterId`).

| Parameter | Wert | Ort |
|---|---|---|
| This-Station (Source-Address) | 3 | `BacnetClientId` |
| Max. Master | 127 | Default im Transport |
| Max Info Frames | **1** | Default im Transport |
| Geräteerkennung | `WhoIs` → `IAm` | [ComfoBoxClient.cs:88](https://github.com/RF77/comfobox-mqtt) |

### MSTP-Timings (aus `BACnetTransport.cs`)

| Konstante | Wert | Bedeutung |
|---|---|---|
| `T_FRAME_ABORT` | 80 ms | Frame-Abbruch |
| `T_NO_TOKEN` | 500 ms | Token-Verlust |
| `T_REPLY_TIMEOUT` | 295 ms | Antwort-Wartezeit |
| `T_USAGE_TIMEOUT` | 95 ms | Token-Nutzung |
| `T_REPLY_DELAY` | 250 ms | Max. Verzögerung vor Antwort |
| `ETIMEDOUT` | 110 | interner Timeout-Code |
| `m_max_poll` | 50 | Tokens vor Poll-For-Master-Zyklus |
| `MaxBufferLength` | 502 | Puffergröße |

### Zustandsmaschine

```
Reset → Initialize → Idle ⇄ (PollForMaster | PassToken | UseToken | WaitForReply
                                                  | AnswerDataRequest) → DoneWithToken
```

Der MSTP-Thread läuft mit `ThreadPriority.Highest`.

### Client-Timeouts (nie überschrieben!)

```csharp
// BACnetClient.cs:118 — Konstruktor-Defaults
timeout  = 1000 ms
retries  = 3
// BACnetClient.cs:41
m_transmit_timeout = 30000   // "long transmit timeout due to MSTP"
```

`ComfoBoxClient.StartInternalAsync()` erzeugt den `BacnetClient` **ohne** diese Defaults zu
überschreiben → 3 Retries × 1 s = bis zu 3 s pro Objekt im Fehlerfall.

`ComfoBoxClient.StartAsync()` wartet `MqttNoResponseTimeout` (Default **30 s**) auf die
`IAm`-Antwort und wirft dann `TimeoutException`.

### Zustandsübergänge im Gateway

```
StartAsync()
  ├─ ItemFactory.CreateItems()   → 127 Items, Subscribe-Aufrufe (VOR dem Connect!)
  ├─ CreateSpecialItems()
  ├─ PollSpecialItems()          → eigener Task, 1-s-Intervall
  ├─ ComfoBoxClient.StartAsync() → WhoIs + warte auf IAm (max 30 s)
  ├─ Event-Hookups für M2Mqtt
  └─ ReconnectAsync()            → 5 s Delay, DANN ConnectMqtt()
```

> Die MQTT-Subscriptions werden **vor** dem Broker-Connect abgesetzt – und der erste Connect
> verzögert sich um 5 Sekunden, weil er über den Reconnect-Pfad läuft.

## 4. Geräte-Registry

```csharp
// ComfoBoxClient.cs:31
private readonly List<BacNode> _devicesList = new List<BacNode>();
```

`OnIam()` fügt jedes gefundene Gerät hinzu; der Start gilt als erfolgreich, sobald die
`IAm` der Geräte-ID `BacnetMasterId` (Default 1) eintrifft. Alle Lese-/Schreibzugriffe gehen
ausschließlich an `BacnetMasterId` – die Registry ist damit faktisch nur für die Erkennung da.

## 5. Bekannte Hardware-Realität

| Beobachtung | Auswirkung |
|---|---|
| ComfoBox muss auf 38400 umgestellt und neu gestartet werden | Betriebsaufwand bei Gerätetausch |
| USB-Adapter wird gezogen → `SerialPort` wirft | **Tight-Spin-Loop, 100 % CPU** (siehe [befunde.md](befunde.md#a8-tight-spin-loop-bei-serial-connectionerror)) |
| Kein serieller Reconnect | nur Neustart des Prozesses hilft |
| `BacnetPipeTransport` nutzt `kernel32.dll!PeekNamedPipe` | Windows-only, bricht unter Mono |

## 6. Nächster Schritt

Die Portierung des Protokollstacks auf .NET 10 ist in
[../02-modernisierung/bacnet-portierung.md](../02-modernisierung/bacnet-portierung.md)
beschrieben.
