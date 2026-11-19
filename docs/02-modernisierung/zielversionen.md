# Zielversionen

Alle Versionen sind zum Stand **2026-11-19** fixiert. Sie werden hier gepflegt –
**nicht** in den Quelldateien.

## 1. Laufzeit & Framework

| Komponente | Version | Begründung |
|---|---|---|
| .NET SDK | **10.0.4xx** (installiert: 10.0.400) | Aktuelles LTS (Nov 2025 – Nov 2028), 3 Jahre Support |
| Target Framework | **net10.0** | Ein einziges TFM, kein Multi-Targeting (siehe ADR-001) |
| Runtime im Container | `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` | Jammy/Alpine, minimal |

> Warum nicht .NET 11? .NET 11 ist seit November 2026 verfügbar, ist aber eine
> STS-Version mit 18 Monaten Support. .NET 10 LTS bietet 3 Jahre Support und ist die
> stabilere Basis. Die `Microsoft.Extensions.*`-Pakete **11.0.0** liefern weiterhin
> `net10.0`-Assets und sind deshalb unter .NET 10 verwendbar.

## 2. NuGet-Pakete (Gateway)

| Paket | Version | Ersetzt | Zweck |
|---|---|---|---|
| `MQTTnet` | **5.2.0.1603** | M2Mqtt 4.3.0.0 | MQTT-Client (v3.1.1 + v5) |
| `MQTTnet.AspNetCore` | **5.2.0.1603** | – | optionaler eingebetteter Broker |
| `System.IO.Ports` | **11.0.x** | BCL (net452) | SerialPort-Zugriff |
| `Microsoft.Extensions.Hosting` | **11.0.x** | Nito.AsyncEx | Host, DI, Lifecycle |
| `Microsoft.Extensions.Configuration.*` | **11.0.x** | ApplicationSettingsBase | JSON + Env + CLI Config |
| `Microsoft.Extensions.Http` | **11.0.x** | – | (falls REST-Zugriff auf Fremdsysteme) |
| `Serilog.AspNetCore` | **11.x** | log4net | Logging (Console + Datei, JSON-Format) |
| `Serilog.Extensions.Hosting` | **11.x** | log4net | Serilog-Provider für den Generic Host |
| `Serilog.Sinks.Console` | **6.1.x** | log4net | Konsolen-Sink |
| `Serilog.Sinks.File` | **7.x** | log4net | Datei-Sink (Rolling) |
| `Swashbuckle.AspNetCore` | **11.x** | – | OpenAPI für die REST-API |
| `Microsoft.AspNetCore.OpenApi` | **10.0.x** | – | OpenAPI-Dokumentation |

> **Kein** `Newtonsoft.Json` – `System.Text.Json` ist in .NET 10 Built-in.
> **Kein** `Fasterflect` – der Item-Katalog wird deklarativ (siehe [Datenmodell](datenmodell-und-api.md)).
> **Kein** `Nito.AsyncEx` — der Host liefert den SynchronizationContext.

## 3. Test-Pakete

| Paket | Version | Zweck |
|---|---|---|
| `xunit` / `xunit.runner.visualstudio` | **2.9.x** / **4.x** | Test-Framework |
| `Microsoft.NET.Test.Sdk` | **18.x** | VSTest-Anbindung |
| `coverlet.collector` | **10.x** | Coverage |
| `FluentAssertions` | **8.x** | Assertions (MIT-Alternative `AwesomeAssertions` erwägen) |
| `NetArchTest.Rules` | **1.3.x** | Architektur-Tests (Schichtentrennung) |

## 4. Frontend

| Komponente | Version | Begründung |
|---|---|---|
| Angular | **22.x** (aktuell 22.3.x) | Aktive Version, Support bis 2028-06 |
| `@openng/optimus-ui` | **2.0.x** | Angular-21+-UI-Library, MIT, PrimeNG-v21-kompatibel |
| TypeScript | **6.0.x** | von Angular 22 vorgegeben (`>=6.0 <6.1`; TypeScript 7 ist noch nicht freigegeben) |
| RxJS | **7.8.x** | |
| `@angular/build` (ESBuild/Vite) | mit CLI 22 | Standard-Builder |
| ESLint / `typescript-eslint` | **10.x** / **8.x** | Linting |
| Vitest / jsdom | **5.x** / **31.x** | Unit-Tests |

> **Optimus UI 2.x benötigt Angular 21+** – Angular 22 erfüllt das.
> Optimus UI ist API-kompatibel zu PrimeNG v21; `providePrimeNG` → `provideOptimus`,
> `primeng/button` → `@openng/optimus-ui/button`.
> Doku: <https://optimus.openng.org/>

### Bewusste Nicht-Nutzung

| Paket | Grund |
|---|---|
| PrimeNG | von Optimus UI abgelöst; Migrationsschematic vorhanden falls nötig |
| Angular Material | Optimus UI bringt Aura/Lara/Material/Nora-Presets bereits mit |
| NgRx | Signal-Store reicht; weniger Boilerplate |

## 5. Container & Tooling

| Tool | Version | Zweck |
|---|---|---|
| Docker Engine | 27+ | Build/Run |
| `mcr.microsoft.com/dotnet/sdk:10.0` | | CI-Build-Image |
| `mcr.microsoft.com/dotnet/aspnet:10.0-alpine` | | Laufzeit-Image (Gateway **+** UI) |
| `node:26-alpine` | | Angular-Build-Stage im Image-Build |
| ~~`nginx:1.27-alpine`~~ | – | **entfallen** – kein nginx-Container (→ [ADR-007](../05-entscheidungen/adr-007-ein-container-image.md)) |
| Docker Compose | v2 (Plugin) | `compose.yaml` |
| GitHub Actions | `actions/checkout@v7`, `actions/setup-dotnet@v6`, `actions/setup-node@v7`, `actions/upload-artifact@v7`, `actions/download-artifact@v8`, `docker/setup-qemu-action@v4`, `docker/setup-buildx-action@v4`, `docker/build-push-action@v7`, `github/codeql-action@v4` | CI/CD |
| Trivy / `dotnet list package --vulnerable` | – | Security-Scan in CI |

## 6. Home Assistant

| Aspekt | Wert |
|---|---|
| HA-Version | ≥ 2024.10 (Discovery-Device-Support vorhanden) |
| MQTT-Protokoll | **MQTT 5** (HA empfiehlt v5) → Gateway muss v5 können |
| Discovery-Prefix | `homeassistant` (konfigurierbar) |
| Broker | Mosquitto (HA-Add-on oder eigener Container) |

## 7. Update-Politik

| Ebene | Rhythmus | Verfahren |
|---|---|---|
| .NET | LTS-Wechsel (Nov 2028) | manuell, mit Branch |
| NuGet-Pakete | monatlich | Dependabot/Renovate-PR, `dotnet list package --outdated` |
| Angular | minor (6–8 Wochen) | `ng update`, CI prüft |
| Optimus UI | nach Major | Breaking changes laut Changelog prüfen |
| Docker-Basis-Images | monatlich | Rebuild + Tag `latest` + `:stable` |
| Firmware/Protokoll | anlassbezogen | – |

### Renovate-/Dependabot-Konfiguration

```jsonc
// .github/dependabot.yml
{
  "package-ecosystem": "nuget",
  "directory": "/src",
  "schedule": { "interval": "monthly" },
  "open-pull-requests-limit": 5
}
```

## 8. Versions-Kompatibilitätsmatrix (Pinning im Repo)

Zusätzlich zu `Directory.Packages.props` (Central Package Management) wird `global.json` gepinnt:

````json
// global.json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestFeature"
  }
}
````

Damit ist der Build deterministisch und in CI reproduzierbar.
