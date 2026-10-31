# ComfoBox MQTT – Projektregeln

Diese Regeln gelten für **alle** Aufgaben in diesem Workspace.

## 1. Was dieses Projekt ist

Ein **MQTT-Gateway** für die Zehnder **ComfoBox Series 5** mit ELESTA-Controller.
Es liest BACnet/MS-TP-Objekte über einen RS485-USB-Adapter und stellt sie als MQTT-Topics
bereit. Bedient wird es über **Home Assistant** und eine **Web-UI**.

```
ComfoBox (ELESTA) ──RS485/USB──▶ Gateway (.NET) ──MQTT──▶ Home Assistant / Web-UI
```

## 2. Aktueller Stand vs. Ziel

| | Stand | Ziel |
|---|---|---|
| Runtime | .NET Framework 4.5.2 (net452), C# 6 | **.NET 10** (`net10.0`), C# 14 |
| Build | `packages.config`, non-SDK-csproj, Visual Studio | SDK-Projekte, `PackageReference`, `dotnet build` |
| MQTT | M2Mqtt 4.3.0.0 (2017, tot) | **MQTTnet 5** (async, TLS, Auth, MQTT 5) |
| Logging | log4net 2.0.5 | `Microsoft.Extensions.Logging` + Serilog |
| Config | `ApplicationSettingsBase` + `app.config` | `Microsoft.Extensions.Configuration` (JSON + Env) |
| Tests | **keine** | xUnit, Golden-Files, Contract-Tests |
| CI | **keine** | GitHub Actions (Build, Test, Image, Trivy) |
| Betrieb | Mono auf Raspberry Pi / Windows-Service | **Docker** auf Synology DS923+ |
| UI | WPF-DemoClient (Windows-only) | **Angular 22 + Optimus UI** |

> **Wichtig:** Die vollständige Ziel-Spezifikation liegt in [`docs/`](../docs/README.md).
> **Lies `docs/02-modernisierung/README.md`**, bevor du Code änderst.

## 3. Unverhandelbare Regeln

### 3.1 MQTT-Kompatibilität

> **Die bestehenden Topic-Adressen und Payload-Formate bleiben gültig.**

- Topic-Pfad: `{BaseTopic}/{Group}/{Name}` (z. B. `ComfoBox/Zone/SetPointNormal`)
- Schreiben: `{Topic}/Set`, Enum als Zahl: `{Topic}/AsNumber` und `{Topic}/AsNumber/Set`
- `BaseTopic`-Default bleibt `ComfoBox`
- `ComfoBox/Connected` (`1`/`0`) und `ComfoBox/Version` bleiben bestehen
- Enum-**Namen** nicht umbenennen
- Neue Topics nur unter `ComfoBox/Status/` bzw. `ComfoBox/Command/`

Details: [`docs/04-migration/mqtt-kompatibilitaet.md`](../docs/04-migration/mqtt-kompatibilitaet.md)

### 3.2 Zahlen immer kultur-invariant

````csharp
float.TryParse(payload, NumberStyles.Float, CultureInfo.InvariantCulture, out var value);
value.ToString("0.###", CultureInfo.InvariantCulture);
````

> **Nie** `float.Parse`/`ToString` ohne explizite `CultureInfo`. Das war der Grund für
> kaputte Schreibvorgänge auf deutschen Systemen (Befund A5).

### 3.3 Der BACnet-Stack wird **portiert**, nicht neu geschrieben

- Der Codec (`BACnetBase`) wird **1:1** übernommen – keine „Verbesserungen" während der Portierung
- Golden-File-Tests müssen **vor** der Portierung existieren und danach **ohne**
  Fixture-Änderung grün sein
- Nur MSTP/Serial bleiben; Pipe/UDP/PTP werden entfernt

### 3.4 Schreibschutz ist heilig

- `MaxNumberOfWritesPer24h` (Default 200) ist der EEPROM-Schutz
- Der Zähler ist **persistent** (nicht im Prozessspeicher)
- **Kein** Schreibvorgang darf den Zähler umgehen
- Ein ungültiger Wert wird **abgelehnt** – niemals ein Default (insbesondere **nicht** `0`) geschrieben

### 3.5 Fehler dürfen nicht verschluckt werden

Jeder Fehler landet in **drei** Kanälen: Log, `ComfoBox/Status/Errors` und die UI.

## 4. Code-Konventionen

### C#

| Regel | Begründung |
|---|---|
| `Nullable` aktiviert, `TreatWarningsAsErrors` | Qualitäts-Gate |
| `async`/`await` statt `.Result`/`.Wait()` | Blockierung → Deadlock-Risiko |
| **Kein** `async void` (außer echten Event-Handlern mit `try/catch`) | unbehandelbare Exceptions |
| **Kein** `lock (this)` | öffentlich sperrbar |
| **Keine** statischen, veränderlichen Zustände | nicht testbar |
| `throw;` statt `throw ex;` | Stack-Trace erhalten |
| `ILogger` statt `System.Diagnostics.Trace` | der BACnet-Stack nutzte `Trace` → Diagnose unsichtbar |
| `record` für Datenklassen | Immutability by default |
| Benennungen: `PascalCase` (Typen), `camelCase` (Felder), `_camelCase` (private Felder) |

### TypeScript / Angular

| Regel | Begründung |
|---|---|
| `strict: true` | Type-Sicherheit |
| Standalone-Komponenten, keine NgModule | Angular-Idiome |
| **Signals** statt RxJS-Subscriptions im Template | moderne Angular-22-Empfehlung |
| `inject()` statt Constructor-Injection | Angular-Idiome |
| Optimus UI: `import { ButtonModule } from '@openng/optimus-ui/button';` | PrimeNG-v21-kompatible API |
| `provideOptimus({ theme: { preset: Aura } })` | Theming-Konfiguration |
| UI-Texte **deutsch**, Code-Bezeichner **englisch** | Zielgruppe |

## 5. Projektstruktur (Ziel)

````
src/
├── ComfoBox.Bacnet/          # BACnet-MSTP-Stack (portiert)
├── ComfoBox.Domain/          # Item-Katalog + Werttypen (framework-frei!)
├── ComfoBox.Gateway/         # Host: BACnet + MQTT + API + Health (+ SPA aus wwwroot)
├── ComfoBox.Web/             # Angular 22 + Optimus UI (inkl. Getting-Started-Assistent)
├── ComfoBox.*.Tests/         # Unit-/Integrationstests
└── ComfoBox.Contract.Tests/  # MQTT-Kompatibilität
deploy/synology-spk/          # Synology-Paket (SPK) + Supervisor
docs/                         # Spezifikation (siehe docs/README.md)
````

**Schichtregeln:**

```
ComfoBox.Domain   ← keine Abhängigkeiten (auch kein log4net/ILogger)
ComfoBox.Bacnet   ← nur System.IO.Ports + Logging-Abstraktion
ComfoBox.Gateway  ← MQTTnet, ASP.NET Core, Domain, Bacnet
ComfoBox.Web      ← spricht nur mit der REST/SSE-API des Gateways
````

- `ComfoBox.Domain` darf **kein** `ILogger` führen → Framework-freiheit
- Keine Zyklen zwischen Projekten

## 6. Build, Test, Ausführen

````bash
# Solution bauen
dotnet build src/ComfoBox.sln -c Release

# Tests
dotnet test src/ComfoBox.sln -c Release

# Nur die schnellen Unit-Tests
dotnet test src/ComfoBox.Domain.Tests -c Release

# Frontend
cd src/ComfoBox.Web && npm ci && npm run lint && npm start

# Container
docker compose up -d
docker compose logs -f gateway

# Synology-Paket (SPK) bauen und prüfen
bash deploy/synology-spk/tools/check-shell.sh
bash deploy/synology-spk/build-spk.sh
````

| Voraussetzung | Version |
|---|---|
| .NET SDK | 10.0.400 (in `global.json` gepinnt) |
| Node.js | 22 |
| Docker | 27+ |

## 7. Konventionen für Beiträge

| Aspekt | Konvention |
|---|---|
| Commits | Conventional Commits (`feat(scope): …`, `fix(bacnet): …`) |
| Branches | `main`, `feature/…`, `fix/…`, `release/…` |
| PRs | gegen `main`, Checkliste aus [`docs/04-migration/checkliste.md`](../docs/04-migration/checkliste.md) |
| Doku | Änderungen an Verhalten → `docs/` aktualisieren; neue Entscheidung → ADR in [`docs/05-entscheidungen/`](../docs/05-entscheidungen/README.md) |
| Versionierung | SemVer |

## 8. Dauerhafte Fallstricke

| Fallstrick | Wo | Merksatz |
|---|---|---|
| Kulturelles Zahlenformat | Payload-Pfad | immer `InvariantCulture` |
| `DateValue`/`CommandValue` sind tot | Domäne | nicht wiederbeleben |
| Prioritäts-Attribute waren wirkungslos | ItemFactory | jetzt **wirklich** auswerten |
| `Trace` statt Logging | BACnet-Stack | immer `ILogger` |
| `docs/topics.json` ist die **Test-Fixture** | Contract-Tests | **nicht** verändern |
| `MoMA Scan Results.html` ist die **BACnet-Fixture** | Golden-Tests | **nicht** verändern |
| M2Mqtt hat kein Async/TLS/Auth | MQTT | MQTTnet verwenden |
| `keepAlivePeriod = 2` war der Broker-Killer | MQTT | ≥ 15 s, hier 30 s |
| `Connected=1` fehlte nach Reconnect | MQTT | bei **jedem** Connect publizieren |
| Ein Image für Gateway **und** UI | Container | kein nginx-Container (→ ADR-007) |
| `devices:` ist zur Installzeit **nicht** bekannt | SPK/Supervisor | wird aus der Portwahl gerendert |
| Kein Docker-Socket im Container | Sicherheit | Host-seitiger Supervisor |
| `postuninst` darf das `var`-Verzeichnis **nicht** löschen | SPK | sonst ist der Schreibzähler weg |
| Root-`.gitignore` hat `[Bb]in/` | SPK | `src/target/bin/*.sh` würde ignoriert → Ausnahme in `deploy/synology-spk/.gitignore` |

## 9. Detailliertere Regeln

| Thema | Datei |
|---|---|
| C#-Kodierung, Architektur, Tests | [`csharp.instructions.md`](instructions/csharp.instructions.md) |
| Angular / Optimus UI | [`angular.instructions.md`](instructions/angular.instructions.md) |
| BACnet-Stack | [`bacnet.instructions.md`](instructions/bacnet.instructions.md) |
| MQTT / Home Assistant | [`mqtt.instructions.md`](instructions/mqtt.instructions.md) |
| Docker / Deployment | [`docker.instructions.md`](instructions/docker.instructions.md) |
| Dokumentation | [`docs.instructions.md`](instructions/docs.instructions.md) |
