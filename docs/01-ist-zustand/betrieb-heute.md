# Betrieb heute (Legacy)

> ⚠️ **Beschreibt den Betrieb des Legacy-Bestands unter [`src_old/`](../../src_old).**
> Für den Zielbetrieb siehe [Synology DS923+](../03-deployment/synology-ds923.md) und
> [Installationsstufen](../03-deployment/installationsstufen.md). (Ist-Zustand)

## 1. Betriebsformen

| Variante | Projekt | Plattform | Status |
|---|---|---|---|
| **Konsolen-App** | `ComfoBoxMqttConsole` | Linux/Mono (Raspberry Pi), macOS | **Haupt-Deployment** laut README |
| Windows-Service | `ComfoboxService` | Windows | optional |
| WPF-Demo | `DemoClient` | Windows | Direktzugriff auf BACnet ohne MQTT |
| MSI-Installer | `ServiceInstaller` (WiX) | Windows | Build **fehlerhaft** (fehlende DLL) |

## 2. Linux / Raspberry Pi (Mono) — der produktive Pfad

### Installation (README)

```bash
sudo apt-get install mono-complete
sudo apt-get install zip unzip
# ComfoBoxMqtt-Dateien aus dem Release-ZIP kopieren und entpacken
mono ComfoBoxMqttConsole.exe
```

### Konfiguration

Die Konfiguration liegt in `ComfoBoxMqttConsole.exe.config` (aus `App.config` + `XmlConfigMerge`).
Manuell anpassen:

```xml
<setting name="Port" serializeAs="String">
  <value>/dev/ttyUSB0</value>
</setting>
<setting name="Baudrate" serializeAs="String">
  <value>38400</value>
</setting>
```

> Die ComfoBox muss auf **38400** umkonfiguriert und **neu gestartet** werden, damit
> 38400 funktioniert.

### Ablauf der Konsolen-App

```csharp
// ComfoBoxMqttConsole/Program.cs
AsyncContext.Run(async () => {
    while (true) {
        try {
            var client = new ComfoBoxMqttClient(Settings.Default.MqttBrokerAddresses.OfType<string>().ToArray(),
                new ComfoBoxClient(LibSettings.Default.Port, LibSettings.Default.Baudrate,
                                   LibSettings.Default.BacnetClientId));
            await client.StartAsync();
            await client.StartPollingAsync();
        }
        catch (Exception ex) { Logger.Error(...); Thread.Sleep(5000); }
    }
});
Console.ReadLine();
```

| Beobachtung | Wirkung |
|---|---|
| Endlos-Neustartschleife | Nach jedem Fehler wird der **komplette** Client neu gebaut (Broker-Connection inkl.) |
| `Thread.Sleep(5000)` | 5 s Pause zwischen den Neustarts |
| `Console.ReadLine()` **nach** `AsyncContext.Run` | blockiert auf stdin → für den Dienstbetrieb ungeeignet |
| `AsyncContext.Run` (Nito.AsyncEx) | stellt einen `SynchronizationContext` bereit – unter .NET Core obsolet |

### Es gibt keinen Dienst auf dem Pi

Kein systemd-Unit, kein Autostart, kein Restart-on-failure. Der Prozess läuft manuell
(oder über `screen`/`tmux`).

## 3. Kopieren auf den Raspberry (Copy-Skripte)

| Datei | Quelle | Ziel |
|---|---|---|
| `CopyConsoleToRaspberry.cmd` | `src\ComfoBoxMqttConsole\bin\Debug\*.*` | `N:\mono\ComfoBox` |
| `CopyReleaseConsoleToRaspberry.cmd` | `src\ComfoBoxMqttConsole\bin\Release\*.*` | `N:\mono\ComfoBox` |
| `CopyServiceToRaspberry.cmd` | `src\ComfoboxService\bin\Debug\*.*` | (analog) |
| `CopyReleaseServiceToRaspberry.cmd` | `src\ComfoboxService\bin\Release\*.*` | (analog) |

```bat
del N:\mono\ComfoBox\*.* /S /Q
xcopy D:\_git\comfobox-mqtt\src\ComfoBoxMqttConsole\bin\Debug\*.* N:\mono\ComfoBox
```

| Problem | Auswirkung |
|---|---|
| Fest verdrahtete Pfade `D:\_git\comfobox-mqtt` | Repo liegt jetzt unter `C:\_git\rf77\comfobox-mqtt` → **Skripte laufen ins Leere** |
| `N:\mono\ComfoBox` | Netzwerk-Share; das Skript ist nicht portable |
| `del … /S /Q` | löscht rekursiv das **gesamte** Zielverzeichnis |
| Konfigurationsdateien werden **nicht** kopiert | `.exe.config` muss manuell nachgezogen werden |

## 4. Windows-Service

### Startlogik

```csharp
// ComfoboxService/ComfoboxService.cs:44
protected override void OnStart(string[] args)
{
    Task.Run(() => {
        AsyncContext.Run(async () => {
            XmlConfigurator.Configure();
            while (!_stop) {
                try {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    _client?.Stop();
                    _client = new ComfoBoxMqttClient(...);
                    await _client.StartAsync();
                    await _client.StartPollingAsync();
                }
                catch (OperationCanceledException) { return; }
                catch (Exception ex) { Logger.Error(...); await Task.Delay(1000); }
            }
        });
    }, _cancellationTokenSource.Token);
    base.OnStart(args);
}
```

| Beobachtung | Wirkung |
|---|---|
| `OnStart` kehrt **sofort** zurück | Der SCM meldet „gestartet", obwohl der Gateway noch nicht läuft |
| Kein `RequestAdditionalTime` | Der SCM bricht bei 30 s Start-Timeout ab |
| `Task.Run` + `AsyncContext.Run` | Verschachtelung zweier Async-Mechanismen |
| Fehler → 1 s Pause → kompletter Neustart | Endlosschleife bei dauerhaftem Fehler (z. B. falscher Port) |
| `OnStop` → `_client.Stop()` | **`NullReferenceException`**, wenn vor `OnStart` gestoppt wird |
| `Stop()` → `Disconnect()` → `ConnectionClosed`-Event | Der Service **reconnectet beim Stoppen** (kein Cancellation-Token im Reconnect-Pfad) |

### MSI-Installer (WiX)

`src_old/ServiceInstaller/Product.wxs`:

| Property | Wert |
|---|---|
| Version | `0.4.0.0` |
| InstallScope | `perMachine` |
| Ziel | `%ProgramFiles%\ComfoBox` |
| Service | `ownProcess`, `Start="auto"`, `Account="LocalSystem"`, `ErrorControl="ignore"` |
| RestartPolicy | 3× `FirstFailureActionType="restart"`, `RestartServiceDelayInSeconds=60` |

> **Build-Broken:** `Product.wxs:56` referenziert `HexMaster.ServiceHelper.dll` – **diese DLL
> existiert im Repo nicht**. Zusätzlich sind die Quellpfade fest auf
> `..\ComfoboxService\bin\x64\Release\…` verdrahtet.

## 5. Betrieb in Home Assistant (Ist-Zustand)

Es gibt **keine** MQTT-Discovery. Jedes der 127 Topics muss in HA **manuell** angelegt werden
(MQTT-Sensor bzw. MQTT-Switch mit `state_topic` = `{BaseTopic}/…` und `command_topic` =
`{BaseTopic}/…/Set`).

Das Topic `ComfoBox/Connected` ist der einzige Health-Indikator – er wird aber:

- beim ersten Connect als `1` publiziert,
- per **LWT** auf `0` gesetzt, wenn die Verbindung abbricht,
- **nach einem Reconnect nicht erneut** auf `1` gesetzt (Bug).

> **Konsequenz:** HA-Entitäten, deren `availability_topic` auf `ComfoBox/Connected` liegt,
> bleiben nach einem Broker- oder Netzwerkproblem dauerhaft `unavailable`, obwohl der
> Gateway läuft. Siehe [befunde.md](befunde.md#a10-nach-reconnect-kein-connected1-publiziert).

## 6. Betrieb auf der Synology DS923+ (Ziel)

Der heutige Stand kann auf der DS923+ **nicht** produktiv betrieben werden:

- .NET Framework 4.5.2 / Mono auf DSM ist nicht sinnvoll unterstützt.
- `BacnetPipeTransport` nutzt `kernel32.dll`-P/Invoke (Windows-only) – wird zwar nicht genutzt,
  verhindert aber einen unveränderten Build.
- Es gibt keine Container-Definition, kein Image, keine Automatisierung.

Das Zielbild steht in [../03-deployment/synology-ds923.md](../03-deployment/synology-ds923.md).

## 7. Betriebsaufwand heute (Zusammenfassung)

| Aufgabe | Aufwand | Automatisiert? |
|---|---|---|
| Deployment auf Pi | `Copy*.cmd` + manuelles `.exe.config` | nein |
| Konfiguration ändern | XML-Datei auf dem Pi editieren | nein |
| Autostart | manuell / kein systemd-Unit | nein |
| Neustart bei Fehler | interne Schleife (5 s), aber mit Broker-Nebeneffekt | teilweise |
| Health in HA | manuell konfiguriert | nein |
| Neustart aus HA | **nicht möglich** | nein |
| Statusübersicht | nur MQTT-Topics + Grafana/InfluxDB | teilweise |
| Update der Software | manuelles Kopieren | nein |
