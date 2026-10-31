---
title: Prompt-Phase-4
description: Kopierfertiger Session-Prompt für die Phase 4 (REST/SSE-API)
order: 4
---

# Prompt – Phase 4: REST/SSE-API

> Kopierfähiger Prompt für die Session **nach** Phase 3.
>
> ⚠️ **Erst verwenden, wenn Phase 3 abgeschlossen ist.** Voraussetzung ist ein
> grüner `dotnet build src/ComfoBox.sln -c Release` und `dotnet test`
> (219 Tests grün).

---

## Prompt (zum Kopieren)

````text
Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze die **Phase 4** des Aufgaben-Dokuments um: REST/SSE-API.

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md                     ← die Projektregeln
2. .github/instructions/csharp.instructions.md        ← die C#-Regeln
3. .github/instructions/mqtt.instructions.md          ← die MQTT-Regeln (Topic-Vertrag!)
4. docs/06-aufgaben/README.md                        ← wie eine Session abläuft
5. docs/06-aufgaben/status.md                        ← Stand nach Phase 3
6. docs/06-aufgaben/phase-4.md                       ← die Aufgaben 4.1 bis 4.9
7. docs/02-modernisierung/datenmodell-und-api.md    ← die verbindliche API-Spec
8. docs/04-migration/mqtt-kompatibilitaet.md         ← der bindende Topic-Vertrag
9. docs/02-modernisierung/health-und-status.md      ← Health-Semantik
10. docs/05-entscheidungen/adr-009-ha-integration-ohne-mqtt.md ← die HA-Integration
                                               spricht mit dem Gateway, nicht mit dem Bus

Ziel der Phase 4: Das Gateway bekommt die REST-/SSE-Schnittstelle, die die
Web-UI (Phase 5) **und** die HACS-Integration (Phase 8a) brauchen. Die
Schreibpfade laufen **immer** über `BacnetDevice` – der 24-h-Schreibzähler
darf von keinem Pfad umgangen werden.

Stand: **Phase 3 ist vollständig abgeschlossen** (3.1–3.6 ✅, siehe status.md /
protokoll.md vom 2026-10-31). Offen ist die gesamte Phase 4.

Reihenfolge (aus phase-4.md):
- 4.1 Items-Endpunkte (`GET /api/items`, `GET /api/items/{id}`, `PUT /api/items/{id}`)
- 4.2 `/api/health`, `/api/status`, `/api/diagnostics/errors`
- 4.3 SSE-Stream (`GET /api/events`)
- 4.4 `/api/config` (GET/PUT) + Persistenz in `config.json`
- 4.5 `/api/service/restart` + `ServiceControl`
- 4.6 OpenAPI/Swagger
- 4.7 Setup-API (`/api/setup/*`, PBKDF2, `config.json`)
- 4.8 Token-API (`/api/security/tokens`)
- 4.9 Log-Endpunkte (`/api/diagnostics/logs`, SSE-Tail)

Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- ✅ `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- ✅ `dotnet test src/ComfoBox.sln -c Release` → **219 Tests grün**
  (45 BACnet + 30 Domain + 10 Contract + 134 Gateway). Das ist die Messlatte:
  nach Phase 4 müssen sie **ohne** Änderung grün bleiben.
- ✅ Die Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  ist **eingefroren** (SHA256
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B`,
  5.657 Bytes). Nicht verändern, nicht neu erzeugen.
- ✅ `docs/topics.json` ist **unverändert** (SHA256
  `67D7485BF961B2B6D11BA5E788932056F073B564F52922D2DE8B34DFC10C1463`,
  33.845 Bytes) und wird vom Contract-Test Feld für Feld gegen den
  Domain-Katalog geprüft. **Die Fixture nicht verändern.**
- ✅ `src/ComfoBox.Domain/` ist fertig:
  - `Items/ItemDefinition.cs` – Id, Group, Name, BacnetObjectId, Kind, Min/Max,
    Unit, IsReadOnly, RequiresExpertMode, Priority, Description, EnumValues
  - `Items/Catalog.cs` – **generiert**, 127 Items, Reihenfolge = Legacy-Reihenfolge
  - `Items/ItemEnums.cs` – **generiert**, 4 Enums, 70 Namen
  - `Items/ItemCatalog.cs` – `Find(id)`, `FindByTopicPath(path)`, `ByGroup(group)`
  - `Values/AnalogValue.cs` / `Values/EnumValue.cs` / `Values/ItemValue.cs`
  - `AnalogValue.TryParse` / `Parse` sind die **InvariantCulture**-Helfer (A5)
- ✅ `src/ComfoBox.Gateway/` hat:
  - `GatewayServiceCollectionExtensions.AddGatewayServices(configuration,
    contentRootPath, logBuffer)` – die **gesamte** DI-Registrierung. Die
    Endpunkte kommen als eigene Extension daneben (z. B.
    `AddGatewayApi()`), damit die Tests sie isoliert aufbauen können.
  - `Program.cs` – **nur** noch Serilog, `AddGatewayServices`, `AddGatewayApi`,
    `app.MapGet("/api/health", …)`, `app.Run()`. Alles andere ist ausgelagert.
  - `ValueCache.cs` – `IValueCache.Update(id, ItemValue?)`, `TryGet`,
    `MarkStale`, `IsStale`, `Snapshot`, `SubscribeAsync(ct)` (Broadcast an alle
    SSE-Abonnenten) – **die** Quelle für `/api/events`
  - `Protection/WriteCounter.cs` – **persistent**, atomar, 24-h-Fenster.
    API: `TryAcquire()` (reserviert), `Release()` (bei Fehler zurückgeben),
    `Count`, `Limit`, `IsLimitExceeded`, `GetStats()`.
  - `Health/ErrorCollector.cs` – Fehler-Senke (50), `Report(...)`, `Snapshot()`,
    Ereignis `Reported`
  - `Health/HealthReporter.cs` – `IHealthReporter` mit `Current`,
    `Message`, `Components`, `Changed`-Ereignis, `Report(...)`,
    `ReportHealthy(...)`. `HealthState`: `Online` / `Degraded` / `Offline`
  - `Bacnet/BacnetDevice.cs` – **der einzige Schreibpfad**.
    `WriteAsync(item, value, ct)` liefert `WriteOutcome` (Ok, ReadOnly,
    ExpertModeRequired, OutOfRange, InvalidType, LimitExceeded, BusError) und
    benutzt den Zähler intern (reservieren → schreiben → bei Fehler freigeben).
  - `Bacnet/PollPlan.cs` – welcher Zyklus welche Items liest
  - `Logging/InMemoryLogSink.cs` – Ringpuffer (1000) für den Log-Viewer
  - `Mqtt/` – die MQTT-Schicht aus Phase 3:
    - `MqttTopicMap` – **die einzige** Stelle, an der Topic-Adressen gebildet
      werden. Für Phase 4 relevant: `ConnectedTopic`, `VersionTopic`,
      `NumberOfWritesTopic`, `HealthTopic`, `StatusVersionTopic`,
      `UptimeTopic`, `WritesTopic`, `BacnetTopic`, `MqttTopic`,
      `LastErrorTopic`, `ErrorsTopic`, `RestartTopic`, `RestartCommandTopic`
    - `MqttVersion.Current` / `MqttVersion.Legacy` (`Ver 1.0.0`)
    - `MqttValueFormatter.DisplayName(item)` – der Anzeigename für die UI
    - `MqttPayload.Format/TryParse/TryParseEnum` – kultur-invariant
    - `MqttClientRegistry` – Verzeichnis der verbundenen Clients
    - `HomeAssistantDiscovery` / `HomeAssistantBirthHandler` / `MqttStatusPublisher`
- ✅ `src/ComfoBox.Bacnet/Bacnet/IBacnetClient.cs` – Testnaht. Der
  Gateway-Test spricht mit `FakeBacnetClient` (siehe
  `src/ComfoBox.Gateway.Tests/`).
- ⚠️ **Der Schreibzähler darf von keinem Pfad umgangen werden.** Jeder
  Schreibpfad (auch `PUT /api/items/{id}`) muss über
  `BacnetDevice.WriteAsync` gehen. Nur dort wird der Zähler erhöht.
- ⚠️ **Die Feldnamen von `/api/status` sind exakt vorgegeben** (phase-4.md
  §4.2) – die HA-Integration liest sie. Nicht umbenennen.
- ⚠️ **Die Statuscodes von `PUT /api/items/{id}` sind verbindlich**
  (204/400/403/409/422/502) – siehe phase-4.md §4.1.
- ⚠️ **Befund A6:** Eine ungültige Payload darf **nie** den Wert `0`
  schreiben – der Vorgang wird abgelehnt und gemeldet.
- ⚠️ **Befund A5:** Alle Zahlen-Payloads sind `InvariantCulture`
  (`"21.5"`, niemals `"21,5"`). `AnalogValue.TryParse` macht das schon.
- ⚠️ **Enum-Namen nicht übersetzen und nicht umbenennen** – `ItemEnums` ist die
  Quelle.
- ⚠️ **`ItemDefinition.Id`** folgt camelCase → snake_case
  (`zone.set_point_normal`). Die REST-Routen nutzen die Id, die MQTT-Topics
  dagegen `Group`/`Name` (`Zone/SetPointNormal`). Nichts vermischen!
- ⚠️ **Die beiden Legacy-DateItems** (`states.controller.initial_operation_date`,
  `states.controller.last_parameter_change_date`) haben **keinen** Wert
  (Befund A20) – für sie darf kein Schreibpfad existieren.
- ⚠️ `src_old/` ist Archiv: lesen ist erlaubt und erwünscht, ändern nie.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root.
- `create_file` **überschreibt keine vorhandene Datei** – bei "File already
  exists" die Datei erst löschen (`Remove-Item`) oder mit
  `replace_string_in_file` bearbeiten.
- `CA1707` moniert Unterstriche in Bezeichnern – auch in **Testmethodennamen**.
  Die sind Haustil → `NoWarn CA1707` in den Testprojekten (schon gesetzt).
- `TreatWarningsAsErrors=true` – jeder Analyzer-Fehler stoppt den Build.

Fallstricke aus Phase 3 (bitte beachten):
- ⚠️ **MQTT 5 erlaubt nur EINEN Last Will pro Verbindung.** Der Vertrag
  verlangt zwei → der Gateway hält je Broker zwei Verbindungen
  (`MqttConnectionRole.Main` / `.Will`, Client-ID `{ClientId}-will`).
- ⚠️ **Ein zirkulärer DI-Pfad blockiert den Testhost** (nicht abstürzen!):
  `MqttConnectionManager` → `IMqttConnectionHandler` → `MqttSubscriber` →
  `IMqttPublisher` → `MqttPublisher` → `IMqttClientProvider` →
  `MqttConnectionManager`. MS-DI erkennt den Kreis bei einem
  `Func`-Factory **nicht**, sondern blockiert beim Auflösen. Deshalb gibt es
  `MqttClientRegistry` als eigenes Singleton. **Merksatz:** Der
  Connection-Manager darf **nie** der `IMqttClientProvider` sein, wenn er
  Handler injiziert bekommt. Für Phase 4 gilt das genauso: die
  API-Endpunkte dürfen **keinen** Kreis mit den MQTT-Diensten bilden.
- ⚠️ **`dotnet test` bei einem hängenden Testhost:** der Standard-Output zeigt
  nur „Der Testhostprozess ist abgestürzt". Diagnose:
  `dotnet-dump collect --process-id <PID> --type Full`, dann
  `"clrthreads" | dotnet-dump analyze <dump>` und
  `"clrstack -all" | dotnet-dump analyze <dump>`. Die SOS-Kommandos
  `set threads` / `~*s` gibt es **nicht**.
- ⚠️ **Ein Test, der den echten `BacnetMstpTransport` startet, blockiert** –
  `BacnetMstpTransport.Dispose()` ruft `StopAsync().GetAwaiter().GetResult()`
  (blockierendes Dispose). Für API-/End-to-End-Tests den `IBacnetClient` per
  `AddSingleton<IBacnetClient, FakeBacnetClient>()` überschreiben.
- ⚠️ **`new HostBuilder().ConfigureServices((_, c) => c.AddGatewayServices(...))`**
  – **nicht** `Host.CreateDefaultBuilder()` +
  `ConfigureServices(c => c.AddSingleton(services))` (das registriert nur den
  Container als Singleton, die eigenen Dienste fehlen dann).
- ⚠️ **Ein Test, der per `Assert.True(Has(topic))` prüft, muss `topic=payload`
  vergleichen** – eine Discovery-Payload ist JSON.
- ⚠️ **`MqttApplicationMessage.PayloadSegment` hat keinen öffentlichen
  Getter** – `ConvertPayloadToString()` nehmen.
- ⚠️ **`MqttClientOptionsBuilder.WithTlsOptions` erwartet
  `Func<MqttClientCertificateValidationEventArgs, bool>`** (nicht vier
  Argumente – das war MQTTnet 4).
- ⚠️ **`MqttClientSubscribeOptionsBuilder.WithTopicFilter` nimmt EINEN
  Filter**, keine Liste.
- ⚠️ **`MqttApplicationMessageReceivedEventArgs` hat einen
  4-Parameter-Konstruktor** `(clientId, message, publishPacket, autoAck)`.
- ⚠️ **`Interlocked.Read` gibt es nur für `long`.** Für `int`-Zähler:
  `Volatile.Read(ref x)`.
- ⚠️ **Serilog 4 kennt am `LogEvent` keine `LoggerName`-Eigenschaft** – die
  Kategorie steckt in der Property `SourceContext`.
  `ScalarValue.ToString()` gibt Strings **in Anführungszeichen** zurück – für
  die Anzeige `ScalarValue.Value` nehmen.
- ⚠️ **`CA1848`** (LoggerMessage-Delegaten) und **`CA1873`** (teure
  Log-Argumente) sind im Gateway-Projekt per `NoWarn` unterdrückt. Neue
  Projekte brauchen dieselbe Unterdrückung.
- ⚠️ **`CA1716`** verbietet `Set` als Schnittstellenmember → `IValueCache.Update`.
- ⚠️ **`CA1001`**: Klassen mit `SemaphoreSlim` müssen `IDisposable` sein.
- ⚠️ **XML-Kommentare sind auf lokalen Funktionen in Top-Level-Statements
  nicht erlaubt** (CS1587) → `//`-Kommentare.
- ⚠️ **DataAnnotations validieren verschachtelte Objekte nicht** – verschachtelte
  Options-Klassen einzeln prüfen (siehe `OptionsValidationTests`).
- ⚠️ **`using N;` importiert keine verschachtelten Namespaces.**
  `ASN1` braucht `using ComfoBox.Bacnet.Serialize;`.

Regeln für diese Session:
- Nur die Phase 4 umsetzen, nichts darüber hinaus.
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Die 219 grünen Tests aus Phase 3 müssen grün bleiben.
- **Der Topic-Vertrag ist unverhandelbar:** Topic-Adressen und Payload-Formate
  bleiben gültig. Enum-Namen nicht umbenennen. Neue Topics nur unter
  `ComfoBox/Status/` bzw. `ComfoBox/Command/`.
- **Der Schreibzähler (24 h) ist heilig.** Kein Schreibpfad darf ihn umgehen.
- Nach jeder Aufgabe: Status in docs/06-aufgaben/status.md setzen
  (⬜ offen / 🟡 in Arbeit / ✅ fertig und verifiziert / ⛔ blockiert).
- Am Ende: Eintrag in docs/06-aufgaben/protokoll.md (Datum, Aufgabe, Ergebnis,
  Verifikation, offene Punkte, Überraschungen).
- Wenn eine neue Entscheidung nötig ist: ADR anlegen und docs/README.md sowie
  docs/05-entscheidungen/README.md aktualisieren.
- Keine Secrets, keine Passwörter in Dateien oder Logs.
- Sprache: Code-Bezeichner englisch, Kommentare und Doku deutsch.

Am Ende der Session:
1. Führe alle Verifikationsbefehle aus phase-4.md aus und zeige das Ergebnis.
   Der wichtigste ist: `dotnet test src/ComfoBox.sln -c Release` ist grün und
   die Golden-Fixture ist byte-identisch (Prüfsumme vergleichen!).
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 5), in
   derselben Form wie dieser Prompt – inklusive der Fallstricke, die in dieser
   Session aufgetreten sind.
````
