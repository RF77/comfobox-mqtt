# `src/` – Neuzuimplementierung

> Dieser Ordner ist **die** Implementierung des ComfoBox-Gateways ab .NET 10.
> Der Legacy-Bestand liegt in [`src_old/`](../src_old) und dient **nur** als Referenz
> (Protokollverhalten, Item-Metadaten, Fehlerbilder). Er wird **nicht** weiterentwickelt.

## Warum ein leerer Ordner?

Der alte Code ist .NET Framework 4.5.2 mit `packages.config`, `System.IO.Ports`,
Fasterflect und handgeschriebenem Logging. Eine Portierung „am Stück“ hätte das
gesamte Alt-Gefüge in die neue Architektur getragen.

Stattdessen:

| | Altbestand | Neuzuimplementierung |
|---|---|---|
| Ort | `src_old/` | `src/` |
| Framework | .NET Framework 4.5.2 | .NET 10 (`net10.0`) |
| Projektformat | `packages.config`, Visual-Studio-CSproj | SDK-Projekte, `PackageReference` |
| Logging | log4net 2.0.5 | `Microsoft.Extensions.Logging` + Serilog |
| Konfiguration | `ApplicationSettingsBase` + `app.config` | `Microsoft.Extensions.Configuration` |
| BACnet | Fremd-Code ohne Tests | portiert, mit Golden-Tests |
| MQTT | M2Mqtt 4.3.0.0 (tot) | MQTTnet 5 |
| Tests | keine | xUnit + Golden-Files + Contract-Tests |

> **Der BACnet-Stack wird portiert, nicht neu erfunden.** Die Codec-Logik wird 1:1
> übernommen – die Golden-Tests sichern das ab.
> → [ADR-001](../docs/05-entscheidungen/adr-001-dotnet-version.md),
> [bacnet-portierung](../docs/02-modernisierung/bacnet-portierung.md)

## Zielstruktur

````
src/
├── ComfoBox.Bacnet/          # BACnet-MS/TP-Stack (portiert)
│   ├── Bacnet/
│   │   ├── BacnetCodec.cs        # Encoder/Decoder (aus BACnetBase.cs)
│   │   ├── BacnetClient.cs        # Read/Write Property, WhoIs/IAm
│   │   └── BacnetMstpTransport.cs # MSTP-Zustandsmaschine + SerialPort
│   └── Serial/SerialPortFactory.cs
│
├── ComfoBox.Domain/          # Item-Katalog + Werttypen (framework-frei!)
│   ├── Items/                    # 127 Item-Definitionen
│   ├── Values/                   # AnalogValue, EnumValue
│   └── ItemMetadata.cs
│
├── ComfoBox.Gateway/         # Host: BACnet + MQTT + API + Health (+ SPA aus wwwroot)
│   ├── Bacnet/                   # BacnetDevice (I/O, Retries, Reconnect)
│   ├── Mqtt/                     # MqttPublisher, MqttSubscriber, Discovery
│   ├── Health/                   # HealthReporter, StatusPublisher
│   ├── Api/                      # REST-Endpunkte, SSE, Setup-API
│   ├── Configuration/            # Options + Validierung + AddOnOptionsProvider
│   ├── Protection/               # persistenter Schreibzähler
│   └── Program.cs
│
├── ComfoBox.Web/             # Angular 22 + Optimus UI
│   ├── src/app/core/             # ApiClient, Signal-Stores, Auth, SetupStore
│   ├── src/app/features/         # setup, dashboard, items, config, diagnostics, help
│   └── public/help/              # generiert aus docs/benutzer/ (build:docs)
│
├── ComfoBox.Bacnet.Tests/     # Golden-Tests gegen die BACnet-Fixture
├── ComfoBox.Domain.Tests/     # Item-Katalog, Werttypen, Topic-Ableitung
├── ComfoBox.Gateway.Tests/    # API, Health, Schreibschutz
└── ComfoBox.Contract.Tests/   # MQTT-Kompatibilität gegen docs/topics.json
````

### Schichtregeln

````
ComfoBox.Domain   ← keine Abhängigkeiten (auch kein ILogger)
ComfoBox.Bacnet   ← nur System.IO.Ports + Logging-Abstraktion
ComfoBox.Gateway  ← MQTTnet, ASP.NET Core, Domain, Bacnet
ComfoBox.Web      ← spricht nur mit der REST/SSE-API des Gateways
````

| Regel | Begründung |
|---|---|
| `ComfoBox.Domain` darf **kein** `ILogger` führen | Framework-Freiheit → testbar ohne Container |
| Keine Zyklen zwischen Projekten | |
| Der BACnet-Stack kennt MQTT nicht, der MQTT-Stack kennt BACnet nicht | beide über `ItemCatalog` + `IComfoBoxDevice` |

## Was aus `src_old/` übernommen wird

| Altbestand | Wird | Bemerkung |
|---|---|---|
| `src_old/Bacnet/` | `ComfoBox.Bacnet/` | 1:1 portieren, Golden-Tests **vorher** |
| `src_old/ComfoBoxLib/Items/**` | `ComfoBox.Domain/Items/**` | 127 Items, BACnet-Objekt-IDs, Min/Max, Einheiten |
| `src_old/ComfoBoxLib/Values/**` | `ComfoBox.Domain/Values/**` | nur `AnalogValue`, `EnumValue` – `DateValue`/`CommandValue` sind tot |
| `src_old/ComfoBoxMqtt/Groups/ItemFactory.cs` | `ComfoBox.Domain/Items/Catalog.cs` | Prioritäts-Attribute **wirklich** auswerten |
| `src_old/ComfoBoxMqtt/` | `ComfoBox.Gateway/Mqtt/` | Rewrite auf MQTTnet, ohne Kulturabhängigkeit |
| `src_old/ComfoBoxMqttConsole/` | `ComfoBox.Gateway/` | ASP.NET-Core-Host |
| `src_old/ComfoboxService/` | – | entfällt (Container bzw. `WindowsService`-Host) |
| `src_old/DemoClient/` | – | entfällt → wird durch die Web-UI ersetzt |
| `src_old/ServiceInstaller/` | – | entfällt → `dotnet publish` + Container |
| `src_old/XmlConfigMerge/` | – | entfällt (Binär-Artefakt ohne Quellcode) |

> **Wichtig:** `src_old/` wird **nicht** mehr gebaut und **nicht** mehr gepflegt.
> Änderungen dort sind zu vermeiden. Es dient ausschließlich als Referenz für
> Verhalten, Metadaten und Fehlerbilder.

## Aufbau in Schritten

Die Reihenfolge ist nicht verhandelbar – siehe
[Roadmap](../docs/04-migration/roadmap.md).

| Schritt | Inhalt | PT |
|---|---|---|
| 0 | Solution, `global.json`, `Directory.Packages.props`, `Directory.Build.props` | 2 |
| 1 | BACnet portieren + Golden-Tests | 8 |
| 2 | Domain + Gateway-Kern | 6 |
| 3 | MQTT + HA-Discovery | 6 |
| 4 | REST/SSE-API | 4 |
| 5 | Angular-UI | 8 |
| 6 | Container + NAS | 4 |
| 8 | SPK + Getting Started | 13 |
| 8a–d | HA-Add-on + HACS-Integration | 14 |

> **Golden-Tests vor der Portierung.** Ohne Test-Fixture kein Meilenstein M1.
> → [teststrategie](../docs/04-migration/teststrategie.md)

## Konventionen

| Thema | Regel |
|---|---|
| Sprache | C# 14, `Nullable` aktiviert, `TreatWarningsAsErrors` |
| Async | `async`/`await`, **kein** `.Result`/`.Wait()`, **kein** `async void` |
| Logging | `ILogger`, **kein** `System.Diagnostics.Trace` |
| Zahlen | **immer** `InvariantCulture` für Payloads |
| Datenklassen | `record` |
| Benennung | `PascalCase` (Typen), `camelCase` (Felder), `_camelCase` (private Felder) |
| Kommentare | **deutsch** |
| Commits | Conventional Commits (`feat(scope): …`, `fix(bacnet): …`) |

Die vollständigen Regeln stehen in
[`.github/instructions/csharp.instructions.md`](../.github/instructions/csharp.instructions.md).

## Bauen

````bash
dotnet build src/ComfoBox.sln -c Release
dotnet test  src/ComfoBox.sln -c Release

cd src/ComfoBox.Web && npm ci && npm run lint && npm start
````

> Die Solution wird in **Phase 0.4** angelegt – sie existiert noch nicht.

## Benutzerdoku

Die Anleitungen für Anwender liegen in [`docs/benutzer/`](../docs/benutzer) und werden zur
Build-Zeit nach `src/ComfoBox.Web/public/help/` übersetzt:

````bash
node tools/build-docs.mjs      # Markdown → HTML + index.json (Suchindex)
````

Ergebnis: eine Seite je Themenbereich, ein `index.json` für die clientseitige Suche und
ein `style.css`. Ausgeliefert wird das unter `/help` der Web-UI, im HACS-Dialog und im
Synology-Paketcenter.
→ [benutzerdokumentation.md](../docs/02-modernisierung/benutzerdokumentation.md),
[ADR-010](../docs/05-entscheidungen/adr-010-benutzerdokumentation.md)

## Arbeitsanweisungen

Die Umsetzung ist in [`docs/06-aufgaben/`](../docs/06-aufgaben) in Phasen zerlegt. Pro
Phase gibt es eine Datei mit Ziel-Dateien, Verifikationsbefehlen und Abnahmekriterien;
der Fortschritt steht in [`status.md`](../docs/06-aufgaben/status.md).

| Frage | Antwort |
|---|---|
| Was ist als Nächstes dran? | [status.md](../docs/06-aufgaben/status.md) |
| Was genau muss ich tun? | z. B. [phase-1.md](../docs/06-aufgaben/phase-1.md) |
| Welche Regeln gelten? | [copilot-instructions.md](../.github/copilot-instructions.md) |
