# Phase 0 – Repo-Hygiene & CI (2 PT)

**Ziel:** Eine belastbare Basis, bevor Code bewegt wird.
**Braucht:** – (Startphase)
**Spec:** [Roadmap §Phase 0](../04-migration/roadmap.md#phase-0--repo-hygiene--ci-2-pt)
**Prompt:** [prompts/Prompt-Phase-0.md](prompts/Prompt-Phase-0.md) – kopierfertig starten

> **Wichtig:** Diese Phase baut noch **keine** Features. Sie schafft die
> Voraussetzungen, damit jede spätere Phase automatisch geprüft wird.

---

## 0.1 – `global.json` (SDK-Pinning) · 0.2 PT

| | |
|---|---|
| **Datei** | `src/global.json` |
| **Ergebnis** | `dotnet --version` in `src/` liefert exakt 10.0.400 |
| **Verifikation** | `cd src && dotnet --version` |

````json
{
  "sdk": {
    "version": "10.0.400",
    "rollForward": "latestFeature"
  }
}
````

> **`rollForward: latestFeature`** statt `disable`: Ein CI-Runner ohne genau diese
> Patch-Version soll nicht abstürzen, sondern die nächste Feature-Band nehmen.

**Akzeptanzkriterien:**
- [ ] `src/global.json` existiert mit `version: 10.0.400`
- [ ] `cd src && dotnet --version` → `10.0.400`
- [ ] `dotnet build` außerhalb von `src/` nutzt weiterhin das System-SDK

---

## 0.2 – `Directory.Packages.props` (Central Package Management) · 0.3 PT

| | |
|---|---|
| **Datei** | `src/Directory.Packages.props` |
| **Verifikation** | `dotnet list package` zeigt die zentralen Versionen |

````xml
<Project>
  <PropertyGroup>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <CentralPackageTransitivePinningEnabled>true</CentralPackageTransitivePinningEnabled>
  </PropertyGroup>

  <ItemGroup>
    <!-- Hosting -->
    <PackageVersion Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.Json" Version="10.0.0" />
    <PackageVersion Include="Microsoft.Extensions.Configuration.EnvironmentVariables" Version="10.0.0" />

    <!-- API -->
    <PackageVersion Include="Microsoft.AspNetCore.OpenApi" Version="10.0.0" />

    <!-- MQTT -->
    <PackageVersion Include="MQTTnet" Version="5.2.0.1603" />

    <!-- Logging -->
    <PackageVersion Include="Serilog.AspNetCore" Version="9.0.0" />
    <PackageVersion Include="Serilog.Sinks.Console" Version="6.0.0" />
    <PackageVersion Include="Serilog.Sinks.File" Version="6.0.0" />

    <!-- Test -->
    <PackageVersion Include="xunit" Version="2.9.2" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="2.8.2" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.12.0" />
    <PackageVersion Include="FluentAssertions" Version="6.12.1" />
  </ItemGroup>
</Project>
````

> Die **exakten** Versionen stehen in
> [zielversionen.md](../02-modernisierung/zielversionen.md). Bei Abweichung gilt
> **die Spec**, nicht diese Liste.

**Akzeptanzkriterien:**
- [ ] `ManagePackageVersionsCentrally=true`
- [ ] Mindestens 10 Pakete mit **exakter** Version
- [ ] `TransitivePinning` aktiv (verhindert stillschweigend unterschiedliche Versionen)

---

## 0.3 – `Directory.Build.props` (Qualitäts-Gate) · 0.2 PT

| | |
|---|---|
| **Datei** | `src/Directory.Build.props` |
| **Verifikation** | ein absichtlich warnender Typ erzeugt einen **Fehler** |

````xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>14</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <InvariantGlobalization>false</InvariantGlobalization>
    <GenerateDocumentationFile>true</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
````

> **`InvariantGlobalization=false`** ist Absicht: die Web-UI formatiert Zahlen im
> deutschen Format (`21,5 °C`). Die **Payloads** bleiben trotzdem invariant
> (siehe [mqtt.instructions.md](../../.github/instructions/mqtt.instructions.md)).

**Akzeptanzkriterien:**
- [ ] `Nullable=enable`, `TreatWarningsAsErrors=true`, `LangVersion=14`
- [ ] Eine Warnung wird zum **Build-Fehler**
- [ ] `GenerateDocumentationFile=true` (fehlende XML-Kommentare sind dann ein Hinweis)

---

## 0.4 – `src/ComfoBox.sln` anlegen · 0.3 PT

| | |
|---|---|
| **Datei** | `src/ComfoBox.sln` |
| **Verifikation** | `dotnet build src/ComfoBox.sln -c Release` |

**Reihenfolge:**

> ⚠️ **Fallstrick (verifiziert 2026-10-31):** `dotnet new sln` erzeugt im .NET-10-SDK
> standardmäßig **`.slnx`** (XML-Format), nicht `.sln`. Die Spec verlangt `ComfoBox.sln`
> → **`--format sln` ist Pflicht**, sonst passen alle Build-Befehle der Spec nicht.

````bash
cd src

dotnet new sln -n ComfoBox --format sln

dotnet new classlib -n ComfoBox.Domain  -f net10.0
dotnet new classlib -n ComfoBox.Bacnet  -f net10.0
dotnet new web      -n ComfoBox.Gateway -f net10.0
dotnet new xunit    -n ComfoBox.Domain.Tests  -f net10.0
dotnet new xunit    -n ComfoBox.Bacnet.Tests  -f net10.0
dotnet new xunit    -n ComfoBox.Gateway.Tests -f net10.0
dotnet new xunit    -n ComfoBox.Contract.Tests -f net10.0

dotnet sln add (Get-ChildItem -Recurse -Filter *.csproj | % FullName)

dotnet add ComfoBox.Bacnet   reference ComfoBox.Domain
dotnet add ComfoBox.Gateway  reference ComfoBox.Domain ComfoBox.Bacnet
dotnet add ComfoBox.Domain.Tests  reference ComfoBox.Domain
dotnet add ComfoBox.Bacnet.Tests  reference ComfoBox.Bacnet
dotnet add ComfoBox.Gateway.Tests reference ComfoBox.Gateway
dotnet add ComfoBox.Contract.Tests reference ComfoBox.Domain
````

**Danach:**

| Projekt | Inhalt |
|---|---|
| `ComfoBox.Domain` | `<PackageReference>` **keine** – framework-frei |
| `ComfoBox.Bacnet` | `System.IO.Ports` (in .NET eingebaut) + Logging-Abstraktion |
| `ComfoBox.Gateway` | MQTTnet, ASP.NET Core, Serilog |
| `ComfoBox.Contract.Tests` | liest `docs/topics.json` |

> **`ComfoBox.Web`** (Angular) kommt in **Phase 5.1** – es ist kein .NET-Projekt
> und gehört nicht in die Solution.

### 0.4.1 Nachbearbeitung der Templates (zwingend)

> ⚠️ **Drei Fallen, alle am 2026-10-31 reproduziert und bestätigt.**

**1. `NU1008` – Central Package Management vs. Template-Versionen**
Die Templates schreiben `Version="…"` in jedes `<PackageReference>`. Mit
`ManagePackageVersionsCentrally=true` bricht das den Restore:

````text
error NU1008: Die folgenden PackageReference-Elemente können keinen Wert definieren
für Version: coverlet.collector, Microsoft.NET.Test.Sdk, xunit, xunit.runner.visualstudio.
````

→ In **allen** `.csproj` die `Version="…"`-Attribute **entfernen**:

````xml
<PackageReference Include="xunit" />
<PackageReference Include="xunit.runner.visualstudio" />
<PackageReference Include="Microsoft.NET.Test.Sdk" />
<PackageReference Include="coverlet.collector" />
````

`coverlet.collector` kommt mit dem xunit-Template mit, steht aber **nicht** in der
Paketliste der Spec → es braucht eine eigene `<PackageVersion>`-Zeile in
`Directory.Packages.props`.

**2. `CS1591` – fehlende XML-Kommentare werden zu Fehlern**
`Directory.Build.props` setzt `GenerateDocumentationFile=true` **und**
`TreatWarningsAsErrors=true`. Das Template liefert `Class1.cs` bzw. `UnitTest1.cs`
**ohne** XML-Kommentar → Build-Fehler.

→ Entweder die Platzhalterdateien **löschen** (empfohlen – sie sind leer) oder
in den Test-Projekten `NoWarn` setzen:

````xml
<PropertyGroup>
  <NoWarn>$(NoWarn);CS1591</NoWarn>
</PropertyGroup>
````

**3. `CA1716` – Namespace-Namen**
Prüfen, ob der gewählte Namespace ein reserviertes Schlüsselwort enthält
(z. B. `ComfoBox.Lib` → Fehler). `ComfoBox.Domain`, `ComfoBox.Bacnet` und
`ComfoBox.Gateway` sind **unproblematisch** (verifiziert).

### 0.4.2 Template-Versionen (verifiziert)

| Paket | Template (.NET-10-SDK) | Spec (`zielversionen.md`) | Bewertung |
|---|---|---|---|
| `xunit` | 2.9.3 | 2.9.x | ✅ passt |
| `xunit.runner.visualstudio` | 3.1.4 | 3.x | ✅ passt |
| `Microsoft.NET.Test.Sdk` | 17.14.1 | 17.x | ✅ passt |
| `coverlet.collector` | 6.0.4 | nicht gelistet | ➕ ergänzen |

> Die Beispiel-Liste in 0.2 nennt ältere Stände (17.12.0 / 2.8.2). Es gilt **die
> Spec** ([zielversionen.md](../../docs/02-modernisierung/zielversionen.md)), und die
> Template-Versionen erfüllen sie – die Template-Versionen sind zu übernehmen.

### 0.4.3 Verifikation

> ⚠️ **Aus `src/` bauen, nicht aus dem Repo-Root.** `global.json` wird gesucht, indem
> vom **aktuellen Arbeitsverzeichnis** nach oben gegangen wird. Aus dem Repo-Root wird
> `src/global.json` **nicht** gefunden und das System-SDK verwendet
> (so ist es in 0.1 auch spezifiziert).

````bash
cd src
dotnet build ComfoBox.sln -c Release
dotnet test  ComfoBox.sln -c Release
````

> ⚠️ **Nie vom Repo-Root aus `dotnet build` ohne Pfad aufrufen** – dort liegen die
> **net452-Projekte** in `src_old/`, die nicht gebaut werden dürfen.

**Akzeptanzkriterien:**
- [ ] `dotnet build src/ComfoBox.sln -c Release` → grün
- [ ] `dotnet test src/ComfoBox.sln -c Release` → grün (0 Tests sind erlaubt)
- [ ] `ComfoBox.Domain` hat **keine** `PackageReference` auf ein NuGet-Paket
- [ ] Projektreferenzen folgen der Schichtregel (keine Zyklen)
- [ ] Die Solution heißt `ComfoBox.sln` (nicht `.slnx`)
- [ ] Kein `<PackageReference>` enthält ein `Version=`-Attribut (kein `NU1008`)
- [ ] `Class1.cs` / `UnitTest1.cs` sind entfernt oder dokumentiert (kein `CS1591`)

---

## 0.5 – `.gitignore` vervollständigen · 0.1 PT

| | |
|---|---|
| **Datei** | `src/.gitignore`, Root-`.gitignore` |
| **Status** | ✅ erledigt und verifiziert (2026-10-31) |

> **Fallstrick:** Das Root-`.gitignore` enthält `[Bb]in/` und ignoriert damit
> `src/target/bin/` des SPKs. Die Ausnahme steht in `deploy/synology-spk/.gitignore`.

**Akzeptanzkriterien:**
- [x] `git ls-files deploy/synology-spk/src/target/bin/` zeigt **drei** `.sh`-Dateien
- [x] `git check-ignore -v src/.env` greift (`.env`, `appsettings.override.json`, `*.user` ignoriert)
- [x] `git check-ignore -v src/ComfoBox.Web/node_modules/x` greift (`node_modules/`, `dist/` ignoriert)
- [x] `git check-ignore -v custom_components/comfobox/tests/__pycache__/z.pyc` greift (`__pycache__/` ignoriert)

---

## 0.6 – CI: Build & Test · 0.4 PT

| | |
|---|---|
| **Datei** | `.github/workflows/build-test.yml` |
| **Verifikation** | grüner Lauf bei einem Test-PR |

````yaml
name: Build & Test
on:
  push: { branches: [main] }
  pull_request:

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          global-json-file: src/global.json
      - run: dotnet build src/ComfoBox.sln -c Release --nologo
      - run: dotnet test src/ComfoBox.sln -c Release --nologo --collect:"XPlat Code Coverage"
      - uses: actions/upload-artifact@v4
        with: { name: coverage, path: '**/coverage*.xml' }
````

**Akzeptanzkriterien:**
- [ ] Build und Test laufen bei jedem PR
- [ ] `global.json` wird verwendet (SDK ist gepinnt)
- [ ] `TreatWarningsAsErrors` → der Build bricht bei Warnungen ab

---

## 0.7 – Fixtures fixieren · 0.3 PT

| | |
|---|---|
| **Dateien** | `docs/topics.json`, `docs/Mono/MoMA Scan Results.html` |

> Diese beiden Dateien sind **Test-Fixtures** und werden **niemals** geändert –
> außer durch den Generator. Der Task ist: **prüfen**, dass sie vollständig sind,
> und einen Generator zu schreiben, falls die Items auseinanderlaufen.

**Akzeptanzkriterien:**
- [ ] `docs/topics.json` enthält **127** Items mit `Topic`, `Beschreibung`, `Typ`, `Min`, `Max`
- [ ] Ein Generator (`tools/generate-topics/`) kann die Datei **neu erzeugen**
- [ ] `MoMA Scan Results.html` ist unverändert (Prüfsumme im Protokoll vermerkt)

---

## 0.8 – `CHANGELOG.md` anlegen · 0.2 PT

| | |
|---|---|
| **Datei** | `CHANGELOG.md` (Root) |

````markdown
# Changelog

Alle wesentlichen Änderungen an diesem Projekt.

## [Unreleased]

### Added
- (noch nichts)

### Changed
- (noch nichts)

### Fixed
- (noch nichts)
````

**Akzeptanzkriterien:**
- [ ] Datei existiert mit den Abschnitten Added/Changed/Fixed
- [ ] `[Unreleased]`-Abschnitt vorhanden

---

## Abnahme der Phase 0

- [ ] `dotnet build src/ComfoBox.sln -c Release` läuft ohne Visual Studio
- [ ] `dotnet test src/ComfoBox.sln -c Release` läuft (auch bei 0 Tests)
- [ ] CI ist grün
- [ ] Fixtures sind unverändert

→ Weiter mit [Phase 1](phase-1.md)
