---
title: Prompt-Phase-5
description: Kopierfertiger Session-Prompt für die Phase 5 (Angular/Optimus UI)
order: 5
---

# Prompt – Phase 5: Angular/Optimus UI

> Kopierfähiger Prompt für die Session **nach** Phase 4.
>
> ⚠️ **Erst verwenden, wenn Phase 4 abgeschlossen ist.** Voraussetzung ist ein
> grüner `dotnet build src/ComfoBox.sln -c Release` und `dotnet test`
> (278 Tests grün).

---

## Prompt (zum Kopieren)

````text
Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze die **Phase 5** des Aufgaben-Dokuments um: die Web-UI (Angular 22 +
Optimus UI).

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md                     ← die Projektregeln
2. .github/instructions/angular.instructions.md        ← die Angular/Optimus-Regeln
3. .github/instructions/csharp.instructions.md        ← die C#-Regeln
4. .github/instructions/mqtt.instructions.md          ← die MQTT-Regeln (Topic-Vertrag!)
5. docs/06-aufgaben/README.md                        ← wie eine Session abläuft
6. docs/06-aufgaben/status.md                        ← Stand nach Phase 4
7. docs/06-aufgaben/phase-5.md                       ← die Aufgaben 5.1 bis 5.12
8. docs/02-modernisierung/ui-angular-optimus.md      ← die verbindliche UI-Spec
9. docs/02-modernisierung/datenmodell-und-api.md    ← die API, die die UI benutzt
10. docs/05-entscheidungen/adr-003-frontend.md       ← Angular 22 + Optimus UI
11. docs/05-entscheidungen/adr-005-api-stil.md       ← REST + SSE
12. docs/05-entscheidungen/adr-012-api-zugriffskontrolle.md ← Bearer/Basic/LAN-Modus
13. docs/02-modernisierung/benutzerdokumentation.md  ← die Benutzerdoku (Phase 5.11)

Ziel der Phase 5: Die Oberfläche, mit der man die Anlage bedienen kann –
Dashboard, Item-Browser mit Schreiben, Konfigurationsseite, Diagnose mit
Log-Viewer, Getting-Started-Assistent, Login. Die UI spricht **ausschließlich**
mit der REST/SSE-API des Gateways (Phase 4) – sie spricht **nie** direkt auf
den BACnet-Bus und **nie** mit einem MQTT-Broker.

Stand: **Phase 4 ist vollständig abgeschlossen** (3.1–3.6 ✅, 4.1–4.9 ✅, siehe
status.md / protokoll.md vom 2026-10-31). Offen ist die gesamte Phase 5.

Reihenfolge (aus phase-5.md):
- 5.1 Angular-Projekt + Optimus UI (Preset Aura, zoneless)
- 5.2 API-Client, Signal-Stores, SSE-Client (Reconnect + Backoff)
- 5.3 Dashboard (Health, Verbindungen, Kacheln)
- 5.4 Item-Browser + Schreiben (Analog/Enum)
- 5.5 Konfigurationsseite
- 5.6 Diagnoseseite
- 5.7 Responsive, A11y, i18n (de)
- 5.8 SPA-Auslieferung aus dem .NET-Host + Docker-Stage
- 5.9 Getting-Started-Assistent (6 Schritte)
- 5.10 Login + Cookie-Auth
- 5.11 Hilfe-Bereich (/help/:slug) + Suche
- 5.12 Log-Viewer (Live-Tail, Filter, Export)

Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- ✅ `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- ✅ `dotnet test src/ComfoBox.sln -c Release` → **278 Tests grün**
  (45 BACnet + 30 Domain + 10 Contract + 193 Gateway). Das ist die Messlatte:
  nach Phase 5 müssen sie **ohne** Änderung grün bleiben.
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
- ✅ `src/ComfoBox.Gateway/Api/` – die **fertige** REST-API (Phase 4):
  - `Api/Models/` – **die DTOs sind der Vertrag** für die UI:
    `ItemDto` (id, group, name, displayName, kind, value, valueText, unit, min,
    max, writable, requiresExpertMode, stateTopic, setTopic, lastUpdated, stale,
    enumValues), `StatusReport` (deviceId, version, uptimeSeconds,
    bacnet{connected,port,lastError}, mqtt{connected,broker},
    writes{count,limit,resetsAt}), `HealthReport` (state, message, checks[],
    version, uptimeSeconds, startedAt), `ConfigDto`, `LogEntryDto`,
    `ErrorEntryDto`, `SetupStateDto`, `TestResultDto`, `CreatedTokenDto`
  - `Api/ApiJson.cs` – camelCase (wie der ASP.NET-Host)
  - `Api/SseWriter.cs` – SSE-Format (`event: …` / `data: …`)
  - Endpunkte: `/api/items`, `/api/items/{id}`, `/api/items/group/{group}`,
    `/api/events` (SSE), `/api/health`, `/api/status`, `/api/diagnostics/errors`,
    `/api/diagnostics/writes`, `/api/diagnostics/logs`,
    `/api/diagnostics/logs/stream` (SSE), `/api/config`,
    `/api/service/restart`, `/api/setup/*`, `/api/security/tokens`
  - Fehlerformat: `{ "error": "value_out_of_range", "message": "…", … }`
    (Codes: `invalid_request`, `invalid_value`, `invalid_type`,
    `value_out_of_range`, `read_only`, `expert_mode_required`,
    `write_limit_exceeded`, `bus_error`, `item_not_found`, `unauthorized`,
    `forbidden`, `too_many_streams`)
- ✅ `src/ComfoBox.Gateway/Program.cs` mappt **alle** Endpunkte über
  `app.MapGatewayApi()`. Die SPA wird in Phase 5.8 aus `wwwroot/` ausgeliefert –
  der .NET-Host ist der einzige Server (→ ADR-007, kein nginx).
- ✅ `src/ComfoBox.Gateway/Security/ApiAccessGuard.cs` – Bearer-Token und
  Basic-Auth des Administrators; im LAN-Modus (`Api:RequireAuthentication =
  false`) sind administrative Endpunkte offen, die `/api/setup/*`-Endpunkte sind
  **nach** der Ersteinrichtung immer geschützt.
- ⚠️ **Die Feldnamen der API sind Vertrag.** Die HA-Integration (Phase 8a)
  liest `/api/items` und `/api/status` unverändert. Nichts umbenennen.
- ⚠️ **`GET /api/items/{id}` liefert den *gecachten* Wert** (kein blockierender
  BACnet-Read). Die UI bekommt die Live-Werte über `/api/events`
  (`snapshot` + `value`).
- ⚠️ **`PUT /api/config` schreibt nach `config.json`; die Werte werden erst
  nach einem Neustart wirksam.** Die Konfigurationsseite muss den Neustart
  anbieten (oder auf „Neustart erforderlich" hinweisen).
- ⚠️ **Ein Schreibfehler setzt den Serverwert zurück** – die UI zeigt nie einen
  optimistischen Wert, den das Gerät nicht angenommen hat (phase-5.md §5.4).
- ⚠️ **Die Benutzerdoku hat YAML-Front-Matter** (`title`, `description`,
  `order`, `icon`, `tags`). Fehlt ein Feld → `tools/build-docs.mjs` bricht ab.
  Der `/help`-Bereich (5.11) baut daraus HTML: `node tools/build-docs.mjs`.
- ⚠️ `src_old/` ist Archiv: lesen ist erlaubt und erwünscht, ändern nie.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root.
- `create_file` **überschreibt keine vorhandene Datei** – bei "File already
  exists" die Datei erst löschen (`Remove-Item`) oder mit
  `replace_string_in_file` bearbeiten.
- `CA1707` moniert Unterstriche in Bezeichnern – auch in **Testmethodennamen**.
  Die sind Haustil → `NoWarn CA1707` in den Testprojekten (schon gesetzt).
- `TreatWarningsAsErrors=true` – jeder Analyzer-Fehler stoppt den Build.

Fallstricke aus Phase 4 (bitte beachten):
- ⚠️ **Ein nicht registrierter Typ als Endpunkt-Parameter antwortet mit
  HTTP 500.** Minimal API hält einen unauflösbaren Parameter für einen
  **Request-Body**: „Body was inferred but the method does not allow inferred
  body parameters". Der Fehler tritt erst beim ersten Request auf. Wenn die
  API 500 liefert: prüfen, ob jeder Parameter-Typ als konkreter Dienst
  registriert ist.
- ⚠️ **`RemoveAll<IHostedService>()` zerstört die Pipeline des Webhosts**
  (der `GenericWebHostService` startet Kestrel). Für Tests den Schalter
  `AddGatewayServices(..., startBackgroundServices: false)` benutzen.
- ⚠️ **SSE ohne Daten schickt keine Kopfzeilen.** Der Client wartet dann bis
  zum ersten Eintrag. Der Server flusht die Kopfzeilen sofort
  (`SseWriter.PrepareAsync`) – die UI muss das beim Verbinden abwarten
  (`onopen`).
- ⚠️ **PBKDF2 kostet ≈ 0,3 s.** Die Anmeldung im Browser fühlt sich sonst
  träge an – die UI soll den Klick-feedback sofort zeigen.
- ⚠️ **`File.Move(…, overwrite: true)` scheitert unter Windows sporadisch**
  („Access to the path is denied", Virenscanner). `AtomicFile.WriteAllText`
  wiederholt den Vorgang sechsmal – **nicht** nachbauen.
- ⚠️ **`dotnet test` zeigt bei einem Fehler im Testhost keinen Body.** Für die
  Diagnose: `dotnet test --logger "console;verbosity=detailed"` – dann steht
  die Server-Exception im Log.
- ⚠️ **Ein Test, der den echten `BacnetMstpTransport` startet, blockiert** –
  `Dispose()` ruft `StopAsync().GetAwaiter().GetResult()`. Für API-Tests den
  `IBacnetClient` per `AddSingleton<IBacnetClient, FakeBacnetClient>()`
  überschreiben.
- ⚠️ **`new HostBuilder().ConfigureServices((_, c) => c.AddGatewayServices(...))`**
  – **nicht** `Host.CreateDefaultBuilder()` + `ConfigureServices(c => c.AddSingleton(services))`.
- ⚠️ **`Interlocked.Read` gibt es nur für `long`.** Für `int`-Zähler:
  `Volatile.Read(ref x)`.
- ⚠️ **Serilog 4 kennt am `LogEvent` keine `LoggerName`-Eigenschaft** – die
  Kategorie steckt in der Property `SourceContext`.
  `ScalarValue.ToString()` gibt Strings **in Anführungszeichen** zurück – für
  die Anzeige `ScalarValue.Value` nehmen.
- ⚠️ **`CA1848`** (LoggerMessage-Delegaten) und **`CA1873`** (teure
  Log-Argumente) sind im Gateway-Projekt per `NoWarn` unterdrückt.
- ⚠️ **`CA1716`** verbietet `Set` als Schnittstellenmember → `IValueCache.Update`.
- ⚠️ **`CA1001`**: Klassen mit `SemaphoreSlim` müssen `IDisposable` sein.
- ⚠️ **XML-Kommentare sind auf lokalen Funktionen in Top-Level-Statements
  nicht erlaubt** (CS1587) → `//`-Kommentare.
- ⚠️ **DataAnnotations validieren verschachtelte Objekte nicht** – verschachtelte
  Options-Klassen einzeln prüfen.
- ⚠️ **Die `docs/topics.json` ist die Test-Fixture** – nicht verändern.
- ⚠️ **`MoMA Scan Results.html` ist die BACnet-Fixture** – nicht verändern.

Regeln für diese Session:
- Nur die Phase 5 umsetzen, nichts darüber hinaus.
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Die 278 grünen Tests aus Phase 4 müssen grün bleiben.
- **Der Topic-Vertrag ist unverhandelbar:** Topic-Adressen und Payload-Formate
  bleiben gültig. Enum-Namen nicht umbenennen.
- **Der Schreibzähler (24 h) ist heilig.** Kein Schreibpfad darf ihn umgehen –
  die UI schreibt ausschließlich über `PUT /api/items/{id}`.
- **UI-Texte deutsch, Code-Bezeichner englisch.**
- Nach jeder Aufgabe: Status in docs/06-aufgaben/status.md setzen
  (⬜ offen / 🟡 in Arbeit / ✅ fertig und verifiziert / ⛔ blockiert).
- Am Ende: Eintrag in docs/06-aufgaben/protokoll.md (Datum, Aufgabe, Ergebnis,
  Verifikation, offene Punkte, Überraschungen).
- Wenn eine neue Entscheidung nötig ist: ADR anlegen und docs/README.md sowie
  docs/05-entscheidungen/README.md aktualisieren.
- Keine Secrets, keine Passwörter in Dateien oder Logs.
- Sprache: Code-Bezeichner englisch, Kommentare und Doku deutsch.

Am Ende der Session:
1. Führe alle Verifikationsbefehle aus phase-5.md aus und zeige das Ergebnis.
   Der wichtigste ist: `dotnet test src/ComfoBox.sln -c Release` ist grün, die
   Golden-Fixture ist byte-identisch (Prüfsumme vergleichen!) und
   `npm run lint` + `npm run build` sind grün.
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 6), in
   derselben Form wie dieser Prompt – inklusive der Fallstricke, die in dieser
   Session aufgetreten sind.
````
