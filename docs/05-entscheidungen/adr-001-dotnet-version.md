# ADR-001: Ziel-Framework .NET 10 (LTS)

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Runtime, Build, Deployment |

## Kontext

Der Ist-Zustand läuft auf **.NET Framework 4.5.2** (C# 6, VS 2015, `packages.config`).
Das erzwingt:

- Mono auf Linux (Windows-Only-APIs im BACnet-Stack)
- kein `dotnet build`, nur Visual Studio + .NET-Framework-Targeting-Pack
- keine modernen Sprachfeatures, kein `Nullable`, keine Analyser
- Windows-Service-Hosting, kein Linux-Support

Die Modernisierung (Anforderung 1) verlangt eine aktuelle Laufzeit **und**
Linux-/Container-Tauglichkeit (Anforderung 3).

Stand 2026-10-31:

| Version | Status | Support bis |
|---|---|---|
| .NET 8 | LTS – End of Support Nov 2026 | 2026-11 |
| .NET 9 | STS – End of Support Mai 2026 | **bereits EOL** |
| **.NET 10** | **LTS** | **Nov 2028** |
| .NET 11 | Preview | Nov 2026 |

Installiert auf dem Entwicklungssystem: **SDK 10.0.400**, Runtime 10.0.11.

## Entscheidung

**Das neue Gateway zielt auf `net10.0` (LTS) und wird ausschließlich als SDK-Projekt mit
`PackageReference` aufgebaut.**

- Ein einziges TFM – kein Multi-Targeting
- `global.json` pinnt die SDK-Version (10.0.400, `rollForward: latestFeature`)
- `Central Package Management` über `Directory.Packages.props`
- `Nullable` aktiviert, `TreatWarningsAsErrors` aktiviert
- Windows-Service-Hosting nur noch als **optionale** Ergänzung
  (`Microsoft.Extensions.Hosting.WindowsServices`), Hauptpfad ist Docker

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **.NET 8 (LTS)** | noch bis Nov 2026 unterstützt | End of Support in **4 Wochen** – unbrauchbar als Ziel |
| **.NET 9 (STS)** | aktuell, aber STS | End of Support Mai 2026 – bereits in der Vergangenheit |
| **.NET 11 (Preview)** | neueste | noch nicht released, kein Supportfenster garantiert |
| **.NET 10 als Multi-Target (`net8.0` + `net10.0`)** | mehr Kompatibilität | doppelte Test-/Build-Aufwand, kein Consumer, der net8.0 braucht |
| **Mono weiterführen** | kein Rewrite nötig | Mono ist im Maintenance-Modus; .NET-Framework-APIs fehlen; kein Container-Support |
| **.NET Framework 4.8 weiterführen** | minimale Änderung | erfüllt Anforderung 1 und 3 nicht |

## Konsequenzen

### Positiv

- 3 Jahre Support (bis Nov 2028) → kein sofortiges Folgemigrations-Drama
- `System.IO.Ports` ist als offizielles Paket verfügbar → Linux-Serial ohne P/Invoke-Hacks
- `Microsoft.Extensions.Hosting` ersetzt `Nito.AsyncEx` → weniger Fremdabhängigkeiten
- Ein TFM → ein Build, ein Image, ein Testlauf
- Aktuelle Sprachfeatures (C# 14), `Nullable`, Source-Generated JSON-Serialisierung
- Offizieller Support durch Microsoft; Container-Images mit Langzeit-Support

### Negativ / Aufwand

- **Kein** Windows-Desktop-Targeting: der WPF-`DemoClient` wird durch die Web-UI ersetzt
- `ApplicationSettingsBase` entfällt → Settings müssen umgestellt werden (ADR-002-Randbedingung,
  [dotnet-migration](../02-modernisierung/dotnet-migration.md#4-settings--microsoftextensionsconfiguration))
- `System.IO.Ports` ist ein NuGet-Paket, nicht BCL → zusätzliche Abhängigkeit
- `log4net` → `Microsoft.Extensions.Logging` (+ Serilog) → Konfigurationsumbau
- Der BACnet-Stack muss portiert werden (eigene ADR-nahe Spezifikation in
  [bacnet-portierung](../02-modernisierung/bacnet-portierung.md))
- Alte `.csproj` (non-SDK) und `packages.config` werden vollständig ersetzt

### Neutral

- Multi-Targeting wird nicht angeboten → wer den Gateway unter .NET 8 braucht, muss auf
  .NET 10 warten (die DS923+ ist x86_64 und erfüllt .NET 10 problemlos).

## Verwandte Dokumente

- [dotnet-migration](../02-modernisierung/dotnet-migration.md)
- [zielversionen](../02-modernisierung/zielversionen.md)
- [ADR-004](adr-004-containerisierung.md)
