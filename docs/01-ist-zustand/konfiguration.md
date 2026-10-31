# Konfiguration (Ist-Zustand)

## 1. Zwei parallele Konfigurationswege

| Quelle | Zweck | Erzeugt durch |
|---|---|---|
| `Properties/Settings.settings` | **Default-Werte** (Compile-Zeit) | `Settings.Designer.cs` (`ApplicationSettingsBase`) |
| `app.config` / `App.config` | **Laufzeit-Overrides** in der `.exe.config` | manuell gepflegt |

Zur Laufzeit gewinnt die `.exe.config` (via `LocalFileSettingsProvider`). Beide Dateien enthalten
**dieselben Werte** → klassische Drift-Quelle: eine Änderung an einem Ort wirkt nicht am anderen.

> `System.Configuration.ApplicationSettingsBase` existiert unter .NET Core/.NET 5+ **nicht**.
> Der gesamte Mechanismus muss bei der Migration ersetzt werden
> (siehe [../02-modernisierung/dotnet-migration.md](../02-modernisierung/dotnet-migration.md#settings--und-konfiguration)).

## 2. `ComfoBoxLib` – Hardware- und Geräteeinstellungen

Quelle: `src/ComfoBoxLib/Properties/Settings.settings` und `src/ComfoBoxLib/app.config`

| Setting | Typ | Default | Verwendung |
|---|---|---|---|
| `Port` | String | `/dev/ttyUSB0` | Serieller Port → `ComfoBoxClient` |
| `Baudrate` | Int32 | `38400` | Serielle Baudrate |
| `BacnetClientId` | Int16 | `3` | MSTP This-Station (Source-Address) |
| `BacnetMasterId` | Int16 | `1` | Geräte-ID des ELESTA-Controllers (Ziel aller Reads/Writes) |
| `ExpertMode` | Boolean | `False` | Schreibsperre auf Expert-Items |
| `MaxNumberOfWritesPer24h` | Int32 | `200` | EEPROM-Schreibschutz |
| `MqttNoResponseTimeout` | TimeSpan | `00:00:30` | Timeout beim Warten auf `IAm` |

### Effekt von `ExpertMode`

Der eigentliche Schreibschutz sitzt **nicht** am `[ExpertMode]`-Attribut, sondern am Item-Modell:

```csharp
// ComfoBoxLib/Items/Config.cs:38 (Beispiel)
new AnalogValue(830, …) { IsReadOnly = !Settings.Default.ExpertMode }
```

`IsReadOnly` → **kein** `/Set`-Topic und **kein** Subscribe ([MqttItem.cs:48](https://github.com/RF77/comfobox-mqtt)).
Das Attribut selbst wirkt nur auf die Doku-Ausgabe (`Needs Expert Mode: True`).

> **Konsequenz für die Migration:** Das Verhalten „ExpertMode=false ⇒ Item ist read-only ⇒
> kein `/Set`-Topic" muss identisch bleiben, sonst brechen bestehende HA-Automationen, die auf
> `/Set` schreiben.

## 3. `ComfoBoxMqtt` – MQTT- und Polling-Einstellungen

Quelle: `src/ComfoBoxMqtt/Properties/Settings.settings` und `src/ComfoBoxMqtt/App.config`

| Setting | Typ | Default | Verwendung |
|---|---|---|---|
| `BaseTopic` | String | `ComfoBox` | **Topic-Wurzel – Teil des Vertrags** |
| `MqttBrokerAddresses` | StringCollection | `10.10.1.20` | Broker-Liste |
| `UseMqttRetain` | Boolean | `True` | Retain-Flag für alle Item-Publishes |
| `MqttQoS` | Byte | `0` | **unbenutzt** |
| `PrimaryStatesRefreshRate` | TimeSpan | `00:01:00` | **unbenutzt** |
| `SecondaryStatesRefreshRate` | TimeSpan | `00:05:00` | **unbenutzt** |
| `PrimaryConfigStatesRefreshRate` | TimeSpan | `00:01:00` | **unbenutzt** |
| `SecondaryConfigStatesRefresh` | TimeSpan | `01:00:00` | **unbenutzt** (Namens-Inkonsistenz) |
| `SpecialSubTopic` | String | `Special` | **nicht gelesen** |
| `VirtualTopic` | String | `Virtual` | nur vom toten `CreateVirtualItems()` |
| `PollSpecialValuesIntervalInMs` | Int32 | `1000` | Intervall für `NumberOfWritesPer24h` |
| `WriteTopicsToFile` | Boolean | `True` | Doku-Generierung (**nur `#if DEBUG`**) |
| `WriteTopicsFilePath` | String | `..\..\..\..\docs\topics.md` | Ausgabepfad |
| `WriteTopicsJsonFilePath` | String | `..\..\..\..\docs\topics.json` | Ausgabepfad |
| `Version` | String | `Ver 0.5.0` | Wert für `{BaseTopic}/Version` |

> **Bemerkung:** `Primary*`/`Secondary*` sind Relikte eines geplanten, aber nie
> implementierten Priorisierungsmodells. Der Ist-Zustand pollt **alle** Items alle 60 s.

## 4. Log4net

| Datei | Appender | Ziel |
|---|---|---|
| `src/ComfoBoxMqttConsole/App.config` | `RollingFileAppender` + `ConsoleAppender` | `ComfoboxConsole_Log.txt` (CWD), Root-Level `DEBUG` |
| `src/ComfoboxService/App.config` | `RollingFileAppender` | `./logs/logs.txt` (**relativ zum Dienst-CWD**) |
| `src/DemoClient/App.config` | `RollingFileAppender` | `${USERPROFILE}\ComfoBox\Logs\Comfobox_Log.txt` |
| `src/ComfoBoxLib/app.config` | – | enthält **keine** log4net-Section |
| `src/ComfoBoxMqtt/App.config` | – | enthält **keine** log4net-Section |

Rolling-Strategie: `Size`, 10 MB × 5 Backups. **Keine Zeitrotation.**

### Konfigurationsfehler

| Beobachtung | Wirkung |
|---|---|
| Service loggt nach `./logs/logs.txt` | Unter `LocalSystem` landet das unter `C:\Windows\System32\logs\` |
| `XmlConfigurator.Configure()` ohne Argument | sucht `log4net.config` neben der Exe, sonst `App.config` → unter Mono abweichendes Verhalten |
| **BACnet-Stack nutzt `System.Diagnostics.Trace`** | 26 Fundstellen in `src/Bacnet` → `Trace` ist **nie** an log4net angebunden. Im Release-Build (`DefineConstants=TRACE`) gehen alle Protokollmeldungen nach `OutputDebugString` und sind **nicht** sichtbar. |

## 5. Build-Kopplung über `XmlConfigMerge`

Post-Build-Event in Console, Service und DemoClient:

```xml
@"$(SolutionDir)XmlConfigMerge\XmlConfigMergeConsole.exe" "$(TargetPath).config"
    -m "$(SolutionDir)\ComfoBoxLib\App.Config"
```

> `XmlConfigMergeConsole.exe` liegt **nur als opake Binärdatei** im Repo
> (`src/XmlConfigMerge/XmlConfigMergeConsole.exe`) – **kein Quellcode**.
> Ein frischer Clone baut nicht reproduzierbar; die Bitness des Tools muss zur Zielplattform passen.

Zusätzlich: `DemoClient/App.config` enthält **keine** `ComfoBoxLib.Properties.Settings`-Section,
obwohl der Post-Build-Event sie mergen soll → die Demo nutzt effektiv den Default
`/dev/ttyUSB0` statt des in der GUI gewählten Ports.

## 6. Konfigurationsprobleme für die Migration

| # | Problem | Lösung in der Spec |
|---|---|---|
| K1 | `ApplicationSettingsBase` existiert in .NET 10 nicht | `Microsoft.Extensions.Configuration` (JSON + Env + CLI) |
| K2 | Doppelte Quelle Settings/app.config | eine Quelle: `appsettings.json` + Env-Overrides |
| K3 | Keine Validierung | Start schlägt bei fehlendem Port/Broker **fehl** statt zur Laufzeit |
| K4 | Passwörter im Klartext in `app.config` | Env-Variablen / Docker-Secrets |
| K5 | Keine Broker-Authentifizierung | MQTTnet mit User/Pass/TLS (siehe [MQTT-Migration](../02-modernisierung/mqtt-library-migration.md)) |
| K6 | Kein Konfigurations-Endpoint | REST-API `PUT /api/config` (siehe [Datenmodell & API](../02-modernisierung/datenmodell-und-api.md)) |
| K7 | Schreibzähler nicht persistent | SQLite/JSON-Datei mit Zeitstempel (siehe [Health & Status](../02-modernisierung/health-und-status.md)) |

## 7. Aktuelle Konfigurationsbeispiele

### KomfoBoxLib (`app.config`)

```xml
<setting name="Port" serializeAs="String">
  <value>/dev/ttyUSB0</value>
</setting>
<setting name="Baudrate" serializeAs="String">
  <value>38400</value>
</setting>
<setting name="BacnetMasterId" serializeAs="String">
  <value>1</value>
</setting>
<setting name="ExpertMode" serializeAs="String">
  <value>False</value>
</setting>
<setting name="MaxNumberOfWritesPer24h" serializeAs="String">
  <value>200</value>
</setting>
```

### ComfoBoxMqtt (`App.config`)

```xml
<setting name="BaseTopic" serializeAs="String">
  <value>ComfoBox</value>
</setting>
<setting name="MqttBrokerAddresses" serializeAs="String">
  <value>
    <string>10.10.1.20</string>
  </value>
</setting>
<setting name="UseMqttRetain" serializeAs="String">
  <value>True</value>
</setting>
```

> `MqttBrokerAddresses` ist eine `StringCollection` – im XML eine Liste von `<string>`-Elementen.
> Bei der Migration wird sie zu einer Liste von Objekten mit Host/Port/User/Pass/TLS.
