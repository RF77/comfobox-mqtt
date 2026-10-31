---
title: Prompt-Phase-3
description: Kopierfertiger Session-Prompt für die Phase 3 (MQTT-Migration & HA-Discovery)
order: 3
---

# Prompt – Phase 3: MQTT-Migration & HA-Discovery

> Kopierfähiger Prompt für die Session **nach** Phase 2.
>
> ⚠️ **Erst verwenden, wenn Phase 2 abgeschlossen ist.** Voraussetzung ist ein
> grüner `dotnet build src/ComfoBox.sln -c Release` und `dotnet test`
> (143 Tests grün).

---

## Prompt (zum Kopieren)

````text
Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze die **Phase 3** des Aufgaben-Dokuments um: MQTT-Migration & HA-Discovery.

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md                     ← die Projektregeln
2. .github/instructions/csharp.instructions.md        ← die C#-Regeln
3. .github/instructions/mqtt.instructions.md          ← die MQTT-Regeln (Topic-Vertrag!)
4. docs/06-aufgaben/README.md                        ← wie eine Session abläuft
5. docs/06-aufgaben/status.md                        ← Stand nach Phase 2
6. docs/06-aufgaben/phase-3.md                       ← die Aufgaben 3.1 bis 3.6
7. docs/02-modernisierung/mqtt-library-migration.md  ← die verbindliche Spec
8. docs/04-migration/mqtt-kompatibilitaet.md         ← der bindende Topic-Vertrag
9. docs/02-modernisierung/home-assistant.md         ← Discovery-Schema
10. docs/05-entscheidungen/adr-002-mqtt-client.md    ← MQTTnet 5 statt M2Mqtt

Ziel der Phase 3: Der Gateway publiziert alle 127 Topics, abonniert die
`/Set`-Topics, meldet sich bei Home Assistant an und bleibt MQTT-seitig stabil.

Stand: **Phase 2 ist vollständig abgeschlossen** (2.1–2.7 ✅, siehe status.md /
protokoll.md vom 2026-10-31). Offen ist die gesamte Phase 3.

Reihenfolge (aus phase-3.md):
- 3.1 MQTTnet-Client (MqttConnectionFactory, MqttConnectionManager, MqttPublisher)
- 3.2 Subscriber für `/Set` und `/AsNumber/Set`
- 3.3 MqttPayload (InvariantCulture) + Enum-Parser ohne 0-Fallback (A5, A6)
- 3.4 LWT, `Connected`, `Version`, `Special/NumberOfWritesPer24h`
- 3.5 Home-Assistant-Discovery (alle 127 Items)
- 3.6 Health-/Status-Publisher

Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- ✅ `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- ✅ `dotnet test src/ComfoBox.sln -c Release` → **143 Tests grün**
  (45 BACnet + 30 Domain + 10 Contract + 58 Gateway). Das ist die Messlatte:
  nach Phase 3 müssen sie **ohne** Änderung grün bleiben.
- ✅ Die Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  ist **eingefroren** (SHA256
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B`).
  Nicht verändern, nicht neu erzeugen.
- ✅ `docs/topics.json` ist **unverändert** und wird vom Contract-Test
  (`src/ComfoBox.Contract.Tests/TopicContractTests.cs`) Feld für Feld gegen den
  Domain-Katalog geprüft. **Die Fixture nicht verändern.**
- ✅ `src/ComfoBox.Domain/` ist fertig:
  - `Items/ItemDefinition.cs` – Id, Group, Name, BacnetObjectId, Kind, Min/Max,
    Unit, IsReadOnly, RequiresExpertMode, Priority, Description, EnumValues
  - `Items/Catalog.cs` – **generiert**, 127 Items, Reihenfolge = Legacy-Reihenfolge
  - `Items/ItemEnums.cs` – **generiert**, 4 Enums (OperationModes,
    HeatingCoolingModes, HeatPumpStatusEnums, KondensatFrostschutzEnum), 70 Namen
  - `Items/ItemCatalog.cs` – `Find(id)`, `FindByTopicPath(path)`, `ByGroup(group)`
  - `Values/AnalogValue.cs` / `Values/EnumValue.cs` – kultur-invariant
  - `AnalogValue.TryParse` / `AnalogValue.Parse` sind **bereits** die
   InvariantCulture-Helfer (A5) – für MQTT bitte darauf aufsetzen.
- ✅ `src/ComfoBox.Gateway/` hat:
  - `Configuration/{Gateway,Bacnet,Mqtt,Logging}Options.cs` – MqttOptions hat
    bereits `Brokers[]` (Host, Port, Username, Password, UseTls), `ClientId`,
    `KeepAliveSeconds` (Default 30). `GatewayOptions.BaseTopic` = "ComfoBox".
  - `ValueCache.cs` – `IValueCache.Update(id, ItemValue?)`, `TryGet`, `MarkStale`,
    `IsStale`, `Snapshot`, `SubscribeAsync(ct)` (Broadcast an alle SSE-Abonnenten)
  - `Protection/WriteCounter.cs` – **persistent**, atomar, 24-h-Fenster.
    API: `TryAcquire()` (reserviert), `Release()` (bei Fehler zurückgeben),
    `Count`, `Limit`, `IsLimitExceeded`, `GetStats()`.
  - `Health/ErrorCollector.cs` – Fehler-Senke (50), `Report(...)`, `Snapshot()`
  - `Bacnet/BacnetDevice.cs` – **der einzige Schreibpfad**. `WriteAsync(item, value, ct)`
    liefert `WriteOutcome` (Ok, ReadOnly, ExpertModeRequired, OutOfRange,
    InvalidType, LimitExceeded, BusError) und benutzt den Zähler intern
    (reservieren → schreiben → bei Fehler freigeben).
  - `Bacnet/PollPlan.cs` – welcher Zyklus welche Items liest
  - `Logging/InMemoryLogSink.cs` – Ringpuffer (1000) für den Log-Viewer
  - `Program.cs` – Serilog, DI, `/api/health`
- ✅ `src/ComfoBox.Bacnet/Bacnet/IBacnetClient.cs` – Testnaht. Der
  Gateway-Test spricht mit `FakeBacnetClient` (siehe `src/ComfoBox.Gateway.Tests/`).
- ⚠️ **Der Schreibzähler darf von keinem Pfad umgangen werden.** Der MQTT-
  Subscriber darf **nicht** direkt `BacnetClient.WritePropertyAsync` rufen,
  sondern muss über `BacnetDevice.WriteAsync` gehen. Nur dort wird der Zähler
  erhöht. Ein Schreibvorgang per `/Set` **muss** den Zähler erhöhen – das ist
  ein Abnahmekriterium von 3.2.
- ⚠️ **Befund A10:** Nach **jedem** Reconnect muss `ComfoBox/Connected` = `1`
  publiziert werden. Der alte Code tat das nur beim ersten Connect.
- ⚠️ **Befund A6:** Eine ungültige Enum-Payload darf **nie** den Wert `0`
  schreiben – der Schreibvorgang wird abgelehnt und gemeldet.
- ⚠️ **Befund A5:** Alle Zahlen-Payloads sind `InvariantCulture`
  (`"21.5"`, niemals `"21,5"`). `AnalogValue.TryParse` macht das schon.
- ⚠️ **Enum-Namen nicht übersetzen und nicht umbenennen** – `ItemEnums` ist die
  Quelle. Der Legacy hat den Enum-**Namen** publiziert (`Normal`), nicht die Zahl.
- ⚠️ **Die beiden Legacy-DateItems** (`states.controller.initial_operation_date`,
  `states.controller.last_parameter_change_date`) haben **keinen** Wert
  (Befund A20) – für sie darf kein Discovery-`command_topic` erzeugt werden.
- ⚠️ **`ItemDefinition.Id`** folgt camelCase → snake_case
  (`zone.set_point_normal`). Die REST-Routen nutzen die Id, die MQTT-Topics
  dagegen `Group`/`Name` (`Zone/SetPointNormal`). Nichts vermischen!
- ⚠️ `src_old/` ist Archiv: lesen ist erlaubt und erwünscht, ändern nie.
  Die Legacy-MQTT-Logik liegt in `src_old/ComfoBoxMqtt/ComfoBoxMqttClient.cs`
  und `src_old/ComfoBoxMqtt/Models/Items/*.cs`.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root.
- `create_file` **überschreibt keine vorhandene Datei** – bei "File already
  exists" die Datei erst löschen (`Remove-Item`) oder mit `replace_string_in_file`
  bearbeiten.
- `CA1707` moniert Unterstriche in Bezeichnern – auch in **Testmethodennamen**.
  Die sind Haustil → `NoWarn CA1707` in den Testprojekten (schon gesetzt).
- `TreatWarningsAsErrors=true` – jeder Analyzer-Fehler stoppt den Build.

Fallstricke aus Phase 2 (bitte beachten):
- ⚠️ **Der Altbestand ist nicht einheitlich kodiert.** Die Item-Dateien sind
  **Windows-1252**, die Enum-Dateien und `Time.cs` sind **UTF-8**. Der Generator
  `tools/generate-topics/generate-topics.mjs` erkennt das jetzt automatisch
  (`readLegacyFile`). Wenn du den Legacy-Katalog änderst: Katalog mit
  `node tools/generate-topics/generate-topics.mjs --catalog src/ComfoBox.Domain/Items`
  neu erzeugen und `--catalog-check` laufen lassen.
- ⚠️ **`Interlocked.Read` gibt es nur für `long`.** Für `int`-Zähler:
  `Volatile.Read(ref x)`.
- ⚠️ **Serilog 4 kennt am `LogEvent` keine `LoggerName`-Eigenschaft** – die
  Kategorie steckt in der Property `SourceContext`.
  `ScalarValue.ToString()` gibt Strings **in Anführungszeichen** zurück –
  für die Anzeige `ScalarValue.Value` nehmen.
- ⚠️ **`CA1848`** (LoggerMessage-Delegaten) und **`CA1873`** (teure Log-Argumente)
  sind aktiv und widersprechen dem Stil in `csharp.instructions.md`. Im
  Gateway-Projekt sind sie per `NoWarn` unterdrückt (mit Begründung im csproj).
  Neue Projekte brauchen dieselbe Unterdrückung.
- ⚠️ **`CA1716`** verbietet `Set` als Schnittstellenmember → `IValueCache.Update`.
- ⚠️ **`CA1001`**: Klassen mit `SemaphoreSlim` müssen `IDisposable` sein.
- ⚠️ **XML-Kommentare sind auf lokalen Funktionen in Top-Level-Statements
  nicht erlaubt** (CS1587) → `//`-Kommentare.
- ⚠️ **`using N;` importiert keine verschachtelten Namespaces.**
  `ASN1` braucht `using ComfoBox.Bacnet.Serialize;`.
- ⚠️ **DataAnnotations validieren verschachtelte Objekte nicht** – verschachtelte
  Options-Klassen einzeln prüfen (siehe `OptionsValidationTests`).
- ⚠️ **MQTTnet 5 API:** Die Paketversion ist auf `5.2.0.1603` gepinnt
  (`src/Directory.Packages.props`). Prüfe die tatsächliche API im Paket
  (`%USERPROFILE%\.nuget\packages\mqttnet\5.2.0.1603\lib\`) – die
  Namespaces und Factory-Methoden haben sich gegenüber MQTTnet 4 geändert.
  `MQTTnet.Client` / `MQTTnet.Protocol` sind die relevanten Namespaces.

Regeln für diese Session:
- Nur die Phase 3 umsetzen, nichts darüber hinaus.
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Die 143 grünen Tests aus Phase 2 müssen grün bleiben.
- **Der Topic-Vertrag ist unverhandelbar:** Topic-Adressen und Payload-Formate
  bleiben gültig. Enum-Namen nicht umbenennen. Neue Topics nur unter
  `ComfoBox/Status/` bzw. `ComfoBox/Command/`.
- Nach jeder Aufgabe: Status in docs/06-aufgaben/status.md setzen
  (⬜ offen / 🟡 in Arbeit / ✅ fertig und verifiziert / ⛔ blockiert).
- Am Ende: Eintrag in docs/06-aufgaben/protokoll.md (Datum, Aufgabe, Ergebnis,
  Verifikation, offene Punkte, Überraschungen).
- Wenn eine neue Entscheidung nötig ist: ADR anlegen und docs/README.md sowie
  docs/05-entscheidungen/README.md aktualisieren.
- Keine Secrets, keine Passwörter in Dateien oder Logs.
- Sprache: Code-Bezeichner englisch, Kommentare und Doku deutsch.

Am Ende der Session:
1. Führe alle Verifikationsbefehle aus phase-3.md aus und zeige das Ergebnis.
   Der wichtigste ist: `dotnet test src/ComfoBox.sln -c Release` ist grün und
   die Golden-Fixture ist byte-identisch (Prüfsumme vergleichen!).
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 4), in
   derselben Form wie dieser Prompt – inklusive der Fallstricke, die in dieser
   Session aufgetreten sind.
````

---

## Aufgaben

| Aufgabe | PT | Titel |
|---|---|---|
| 3.1 | 1.5 | MQTTnet-Client (`MqttConnectionFactory`, `MqttConnectionManager`, `MqttPublisher`) |
| 3.2 | 0.5 | Subscriber für `/Set`-Topics |
| 3.3 | 0.5 | `MqttPayload` (InvariantCulture) + Enum-Parser ohne 0-Fallback |
| 3.4 | 0.5 | LWT, `Connected`, `Version` |
| 3.5 | 1.5 | Home-Assistant-Discovery |
| 3.6 | 1.0 | Health-/Status-Publisher |

> **Summe:** 6 PT

---

## Verwandte Dokumente

- [phase-3.md](../phase-3.md) – die Aufgaben im Detail
- [mqtt-library-migration.md](../../02-modernisierung/mqtt-library-migration.md) – die Spec
- [mqtt-kompatibilitaet.md](../../04-migration/mqtt-kompatibilitaet.md) – der bindende Vertrag
- [home-assistant.md](../../02-modernisierung/home-assistant.md) – Discovery-Schema
