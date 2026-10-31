# Befunde (Ist-Zustand)

Priorisierte Liste der Defekte, Schwachstellen und Risiken. Grundlage für die Spec in
[../02-modernisierung](../02-modernisierung/README.md).

**Prioritäten**

| Prio | Bedeutung |
|---|---|
| **P0** | Blockiert die Modernisierung oder ist ein Sicherheits-/Datenrisiko |
| **P1** | Führt im Feld zu Ausfällen oder Fehlverhalten |
| **P2** | Wartbarkeitsproblem, kein akuter Ausfall |

---

## A – Build- und Portierungs-Blocker (P0)

| ID | Befund | Fundstelle | Lösung |
|---|---|---|---|
| **B1** | `packages.config` statt `PackageReference` – kein Standard-Restore mehr | alle 5 `packages.config` | [dotnet-migration](../02-modernisierung/dotnet-migration.md) |
| **B2** | Altes non-SDK-csproj (`ToolsVersion 12/14`) – kein `dotnet build` | alle 6 `.csproj` | SDK-Projektformat |
| **B3** | `System.IO.Ports` nicht in der BCL ab .NET Core | `BACnetTransport.cs:400` | NuGet-Paket `System.IO.Ports` |
| **B4** | `ApplicationSettingsBase` existiert unter .NET Core nicht | `*/Properties/Settings.Designer.cs` | `Microsoft.Extensions.Configuration` |
| **B5** | log4net 2.0.5 unterstützt .NET 8/10 nicht offiziell | alle Projekte | `Microsoft.Extensions.Logging` |
| **B6** | `Nito.AsyncEx` / `AsyncContext.Run` obsolet | Console/Service `Program.cs` | `Microsoft.Extensions.Hosting` |
| **B7** | M2Mqtt 4.3.0.0 (2017), kein Async-Client, kein .NET-Standard | `ComfoBoxMqtt.csproj` | **MQTTnet 5** |
| **B8** | WPF/WinForms → Windows-only | `DemoClient.csproj` | DemoClient wird Web-UI (siehe [UI-Spec](../02-modernisierung/ui-angular-optimus.md)) |
| **B9** | `ServiceBase` → kein Linux | `ComfoboxService.cs` | `Microsoft.Extensions.Hosting` |
| **B10** | WiX referenziert fehlende `HexMaster.ServiceHelper.dll` | `Product.wxs:56` | MSI-Installer wird durch Container + `dotnet publish` ersetzt |
| **B11** | `XmlConfigMergeConsole.exe` als Binär-Artefakt ohne Quellcode im Build | Post-Build-Events | Tool entfällt komplett (Config wird beim Start gelesen) |
| **B12** | `UdpClient.BeginReceive` existiert in .NET Core/5+ nicht | `BACnetTransport.cs:168,236` | UDP-Transport wird **verworfen** (nur MSTP wird gebraucht) |
| **B13** | `IPAddress.IPv4Mask` obsolete | `BACnetTransport.cs:340` | entfällt mit UDP-Transport |

---

## B – Robustheit und Fehlerverhalten (P1/P0)

| ID | Prio | Befund | Fundstelle | Wirkung |
|---|---|---|---|---|
| **A7** | P0 | Ein BACnet-Fehler → Gateway wird komplett zerstört (`Dispose()` + `throw`) | `ComfoBoxMqttClient.cs:262-265` | Ein einzelner Lesefehler reißt Broker-Verbindung und Polling mit |
| **A8** | P0 | **Tight-Spin-Loop bei Serial-Fehler** – `m_port == null` wird nie gesetzt, `while (m_port != null)` läuft weiter | `BACnetTransport.cs:1900` | **100 % CPU** auf dem Pi, wenn der USB-Adapter gezogen wird |
| **A9** | P0 | Reconnect-Rekursion ohne Backoff und ohne `CancellationToken` | `ComfoBoxMqttClient.cs:70-81` | Unbegrenzte Kette, Thread-Pool wächst, Stop funktioniert nicht |
| **A10** | P1 | Nach Reconnect wird `Connected=1` **nicht** erneut publiziert | `ComfoBoxMqttClient.cs:84-94` | **HA sieht den Gateway dauerhaft als offline** |
| **A11** | P1 | Service reconnectet beim Stoppen (`ConnectionClosed` feuert nach `Disconnect`) | `ComfoBoxMqttClient.cs:276-294` | Beenden des Dienstes unzuverlässig |
| **A12** | P1 | `OnStop` → `NullReferenceException` bei `_client == null` | `ComfoboxService.cs:80-86` | Stop vor Start crasht den Dienst |
| **A14** | P1 | Ein Fehler im Publish-Pfad beendet den Poll-Loop | `StartPollingAsync` | |
| **A23** | P2 | MSTP-Thread mit `ThreadPriority.Highest` | `BACnetTransport.cs:1500` | Kann OS-Tasks starve'n |
| **A18** | P2 | `throw ex` statt `throw` (3×) – Stack-Trace-Verlust | `BACnetClient.cs:932` u. a. | Diagnose erschwert |
| **A19** | P2 | `m_invoke_id++` nicht threadsicher (20+ Stellen) | `BACnetClient.cs:949` u. a. | Nur durch `lock(this)` kaschiert |
| **A16** | P1 | BACnet-Stack nutzt `System.Diagnostics.Trace` statt Logging | 26 Fundstellen in `src/Bacnet` | **Protokolldiagnose unsichtbar**; im Release-Build gehen alle Meldungen ins Leere |
| **A26** | P1 | `BacnetPipeTransport` mit `kernel32.dll`-P/Invoke | `BACnetTransport.cs:466` | Windows-only – bricht unter Mono |

---

## C – Datenkorrektheit und Schreibschutz (P0/P1)

| ID | Prio | Befund | Fundstelle | Wirkung |
|---|---|---|---|---|
| **A5** | **P0** | **Kulturabhängige Zahlen** beim Parsen *und* Formatieren | `MqttItem.cs:110,139`, `EnumMqttItem.cs:96` | Auf de-DE: `23.5` nicht parsebar, `23,5` → `235`. **Schreibvorgänge sind im Feld faktisch kaputt**; publizierte Payloads sind für HA unbrauchbar |
| **A6** | **P0** | Ungültige Enum-Payload schreibt den **Wert 0** | `EnumMqttItem.cs:91-98` | `float.TryParse` prüft nur „ist es eine Zahl", geparst wird aber der String → `newValue` bleibt 0 |
| **A3** | P0 | 24-h-Schreibzähler ist **nicht persistent** und relativ zum Prozessstart | `ComfoBoxClient.cs:27,58-69` | Service-Neustart setzt den EEPROM-Schutz zurück → Schreibzyklen werden überschritten |
| **A22** | P1 | `CurrentNumberOfWrites++` auch bei **fehlgeschlagenem** Write | `ComfoBoxClient.cs:145-152` | Zähler zu pessimistisch |
| **A21** | P2 | `WriteValue` ignoriert den `propertyType`-Parameter | `ComfoBoxClient.cs:231-239` | Schreibt immer `PROP_PRESENT_VALUE` |
| **A20** | P2 | `DateValue.ConvertValue` ist leer, `ConvertValueBack` wirft `NotImplementedException` | `DateValue.cs:35-38` | Date-Items werden nie befüllt und nie publiziert |
| **A2** | P2 | Statische prozessweite Zustände (`CurrentNumberOfWrites`, `TimerIsRunning`) | `ComfoBoxClient.cs:27-28` | Nicht testbar, nicht parallelisierbar |
| **A17** | P2 | `lock (this)` auf `ComfoBoxClient` | `ComfoBoxClient.cs:110,132` | Publicly lockbar |

---

## D – Architektur und Wartbarkeit (P2)

| ID | Befund | Fundstelle |
|---|---|---|
| **A15** | Prioritäts-/Refresh-Attribute (`[Primary]`, `[Secondary]`, `[Once]`) werden gelesen und **verworfen**; alle Items werden mit `RefreshPriority.None` erzeugt. Die 4 `*RefreshRate`-Settings sind tote Konfiguration | `ItemFactory.cs:66-82` |
| **A4** | Pseudo-Async: `await Task.Delay(1)` + blockierender BACnet-Call | `ItemValue.cs:66-76` |
| **A1** | `async void` an 6 Stellen → unbehandelbare Exceptions, Crash-Risiko | `ComfoBoxClient.cs:58`, `ComfoBoxMqttClient.cs:64,227`, 3× DemoClient |
| **A24** | `keepAlivePeriod = 2` s → unnötig häufige Disconnects | `ComfoBoxMqttClient.cs:89` |
| **A25** | **Keine TLS-Unterstützung, kein User/Pass, kein Port pro Broker** | `ComfoBoxMqttClient.cs:49` |
| **A13** | Subscriptions werden **vor** dem Connect abgesetzt; der erste Connect verzögert sich um 5 s | `ComfoBoxMqttClient.cs:104` vs. `:123` |
| **A14b** | Dead Code: `InitMqttClient`, `CommandMqttItem`, `CreateVirtualItems`, `ReadMultipleValues`, `_specialItems` | siehe [architektur.md](architektur.md#7-unbenutzter-code-dead-code) |
| **F5** | **Keine Tests** (kein Testprojekt) | – |
| **F6** | **Keine CI** | – |
| **F8** | Kein DI, kein Logging-Framework-Abstraktion (log4net direkt in allen Klassen) | – |
| **F9** | Kein `Directory.Packages.props` / Central Package Management | – |
| **F10** | Kein `global.json`, keine SDK-Pinning | – |
| **F11** | Kein `Nullable`-Support, keine Analyzers, kein `TreatWarningsAsErrors` | – |
| **F12** | Keine Konfigurationsvalidierung | – |
| **F14** | Kein Geräte-Discovery – der BACnet-Katalog ist hartkodiert | `ComfoBoxLib/Items/**` |

---

## E – Feature-Lücken gegenüber den Anforderungen (P1)

| ID | Anforderung | Ist-Zustand | Lücke |
|---|---|---|---|
| **F1** | HA-Integration | keine MQTT-Discovery | 127 Topics manuell in HA anlegen |
| **F2** | Health | nur `Connected` (1/0), LWT mit 60 s Delay, Bug A10 | kein zuverlässiges Availability-Signal |
| **F3** | **Neustart aus HA** | **nicht vorhanden** | kein Restart-Topic, kein Button |
| **F4** | Diagnose | nur `Special/NumberOfWritesPer24h` | keine Health-/Status-/Fehler-Topics |
| **F7** | Container | kein Docker, kein systemd | kein Deployment auf DS923+ |
| **F11** | UI | nur der WPF-`DemoClient` (Direktzugriff, read-mostly) | keine Konfigurations-/Status-Oberfläche |
| **F14** | Erweiterbarkeit | Katalog hartkodiert | neue ComfoBox-Generation → Handarbeit |

---

## F–Gesamtbewertung

**Reife:** Funktional abgeschlossen und im Feld erprobt (127 Items, openHAB-/Grafana-Anbindung
dokumentiert), aber technisch ein .NET-4.5.2-Fossil mit produktionsrelevanten Fehlern
(A5, A6, A7, A8, A10).

**Die drei kritischsten Punkte für die Modernisierung:**

1. **Fehlende Tests.** Weder für die 127 Item-Definitionen noch für den BACnet-Encoder/-Decoder.
   Ein BACnet-Regressionstest gegen einen Simulator oder die gesicherte
   [MoMA-Scan-Datei](../Mono/MoMA%20Scan%20Results.html) ist die Voraussetzung für jede Umstellung.
2. **Der Protokoll-Stack ist Fremdcode ohne Tests** (~12.800 Zeilen). Er muss 1:1 portiert werden –
   inklusive `System.IO.Ports` als NuGet-Paket und dem Wegfall von `UdpClient.BeginReceive`.
3. **Der Datenverlust-Kern ist die Item-Definition selbst.** Die 127
   `new AnalogValue(id, …)`-Zuordnungen sind die einzige Wahrheit über die ComfoBox. Sie sind über
   [`topics.md`](../topics.md) / [`topics.json`](../topics.json) dokumentiert – diese beiden Dateien
   sind das einzige Asset, das bei der Modernisierung **nicht** verloren gehen darf und als
   Test-Fixture dienen muss.
