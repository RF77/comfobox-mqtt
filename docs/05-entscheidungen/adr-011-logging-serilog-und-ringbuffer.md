# ADR-011: Logging – Serilog als Provider, In-Memory-Ringbuffer für die Web-UI

| | |
|---|---|
| **Status** | Accepted |
| **Datum** | 2026-10-31 |
| **Bereich** | Backend, Frontend, Deployment |

## Kontext

Der Altbestand schreibt mit **log4net 2.0.5** in per `app.config` konfigurierte
Textdateien. Drei Probleme:

1. **Unsichtbar im Betrieb.** Der BACnet-Stack protokollierte mit
   `System.Diagnostics.Trace` (26 Fundstellen, Befund A16) – im Release-Build landete
   davon **nichts** in einer Datei. Diagnose war praktisch unmöglich.
2. **Nicht strukturiert.** Eine Textzeile mit Interpolation kann nicht maschinell
   ausgewertet werden. Auswertung nur mit einem Editor.
3. **Nicht erreichbar für den Betreiber.** Die Logs liegen auf dem NAS-Volume. Wer den
   Container per Compose betreibt, hat sie nur über `docker logs` gefunden; wer den
   SPK-Paket nutzt, über den DSM-Logbrowser. Für die 95 %-Fälle („warum ist die
   Heizung `unavailable`?") ist das **zu viel Aufwand**.

Die Web-UI hat bisher nur eine **Fehlerhistorie** (letzte 50 Fehler). Das beantwortet
„was ist zuletzt schiefgegangen", aber nicht „was macht das Gateway **gerade**".

> *„Bitte auch auf nehmen, dass für Logging im BE Serilog verwendet wird und dass wir
> einen in Memory Ringbuffer von 1000 Logs haben (konfigurierbar), damit wir Logs
> direkt im Angular UI anschauen können"*

## Entscheidung

### E1 – `ILogger<T>` als Abstraktion, Serilog als Provider

Im gesamten neuen Code gilt `Microsoft.Extensions.Logging.ILogger<T>`. Der Provider ist
**Serilog** (`Serilog.Extensions.Hosting`).

| Punkt | Entscheidung |
|---|---|
| Abstraktion | `Microsoft.Extensions.Logging` (wie gewohnt, DI-freundlich) |
| Provider | **Serilog** – strukturiert, eigene Sinks, JSON-Formatter |
| Formate | Console + Datei als JSON (`RenderedCompactJsonFormatter`) |
| Rotation | täglich, 14 Dateien aufbewahrt |
| Verboten | `System.Diagnostics.Trace`, `Console.WriteLine` |

> **Warum Serilog und nicht der Standard-Provider `Console`/`Debug`?**
> Der In-Memory-Ringbuffer braucht einen **eigenen Sink**. Serilog ist die Abstraktion,
> in der Sinks ein citizens first citizen sind – der Ringbuffer wird eine Serilog-Sink
> und bekommt **jedes** Log-Ereignis automatisch. Beim Standard-Provider müsste man
> sich an `ILoggerProvider`/`ILogger` hängen und die Formatierung selbst nachbauen.

### E2 – Ein In-Memory-Ringbuffer mit fester Kapazität

Das Gateway hält die letzten Log-Einträge im Speicher, damit die Web-UI sie anzeigen
kann.

| Punkt | Entscheidung |
|---|---|
| Kapazität | **1000** Einträge (Standard), konfigurierbar |
| Konfiguration | `Logging:RingBuffer:Capacity`, `0` schaltet den Puffer ab |
| Level-Filter | `Logging:RingBuffer:MinimumLevel`, Standard `Information` |
| Speicherbedarf | ~400 KB bei 1000 Einträgen (flüchtig, nicht persistent) |
| Implementierung | eigene Serilog-Sink `InMemoryLogSink` auf festem Array |
| Thread-Sicherheit | `Interlocked` für Index/Zähler, `Volatile` für die Slots |
| I/O im Log-Pfad | **keins** – `Emit` schreibt nur in ein Array |
| Geheimnisse | werden **vor** dem Speichern maskiert (`••••`) |

> **Warum 1000?** Bei ~400 Byte pro Eintrag sind das ~400 KB – vernachlässigbar neben
> dem BACnet-Stack. 1000 Einträge decken bei normaler Betriebsaktivität (Poll-Intervall
> 60 s, also wenige Einträge pro Minute) **einen ganzen Betriebstag** ab. Der Wert ist
> konfigurierbar, weil ein Anwender mit 500 Fehlern pro Stunde mehr braucht.

### E3 – Die Web-UI zeigt die Logs

Die **Diagnoseseite** bekommt einen Reiter „Protokoll" mit Live-Ansicht, Filtern
(Level, Quelle, Volltext, Zeitbereich), Auto-Scroll, Export und „Protokoll leeren".

| Punkt | Entscheidung |
|---|---|
| Route | `/diagnostics` → Reiter „Protokoll" |
| Historisch | `GET /api/diagnostics/logs?level=&source=&search=&since=&limit=` |
| Live | `GET /api/diagnostics/logs/stream` (SSE) – event-getrieben, kein Polling |
| Leeren | `DELETE /api/diagnostics/logs` |
| Auth | wie die übrige API (Token/Cookie) |

> **Warum SSE statt WebSocket?** Das Protokoll läuft im Projekt bereits
> ([`/api/events`](../02-modernisierung/datenmodell-und-api.md#4-sse-server-sent-events-für-live-werte)),
> braucht keinen zweiten Port und keine zweite Technologie. Ein Log-Tail ist ein
> Einweg-Datenfluss – dafür ist SSE genau das richtige Werkzeug.

### E4 – Zwei Ringpuffer, klar getrennt

Der vorhandene **Fehler-Ringpuffer** (50 Einträge, Health-Ereignisse) bleibt bestehen.
Er ist **nicht** dieselbe Sache wie der Log-Ringpuffer:

| | Fehler-Ringpuffer | Log-Ringpuffer |
|---|---|---|
| Inhalt | nur Fehler mit Fehlercode | alle Log-Einträge |
| Kapazität | 50 | 1000 |
| Zweck | Health-Status, Alarmierung, `ComfoBox/Status/Errors` | Diagnose, Live-Ansicht |
| API | `/api/diagnostics/errors` | `/api/diagnostics/logs` |

> Jeder Fehler-Eintrag erzeugt **zusätzlich** einen Log-Eintrag – aber nicht umgekehrt.
> Sonst wäre der Fehlerpuffer redundant.

### E5 – Die Kapazität ist nur per Neustart änderbar

Der Ringpuffer wird beim Start allokiert. Eine Änderung der Kapazität zur Laufzeit
wäre eine Neuzuweisierung unter Last.

| Option | Verworfen, weil |
|---|---|
| Statische Kapazität ✅ | einfach, kein Speicher-Pit-Stop |
| Dynamisch zur Laufzeit | Neuzuweisierung unter Last; zusätzliche Synchronisation |

Der Log-Viewer bietet deshalb **keinen** Regler für die Kapazität an – nur Filter.

### E6 – Fehler im Logging stoppen den Gateway nie

Serilog kapselt Fehler im Sink intern. Zusätzlich ist `InMemoryLogSink.Emit`
defensiv implementiert (kein `throw`). Ein voller Puffer überschreibt den ältesten
Eintrag – **kein** Fehler, **kein** Log-Flood.

## Alternativen

| Option | Bewertung | Verworfen, weil |
|---|---|---|
| **Serilog + eigener Ringbuffer-Sink** ✅ | strukturiert, eigene Sinks, ein Provider | – |
| Nur `Microsoft.Extensions.Logging` ohne Serilog | wenig Code | Ringbuffer müsste selbst gebaut werden; keine JSON-Ausgabe, keine Rotation |
| log4net weiterverwenden | kein Umbau | .NET-Framework-Altlast, nicht strukturiert, `Trace`-Problem bleibt |
| Serilog.Sinks.File + Datei pollen | wenig Code | Datei-I/O pro Poll, Race auf dem Logfile, kein Live-Tail |
| Logdatei via HTTP ausliefern | einfach | Datei-I/O, Format-Coupling, Secrets in der Datei ungefiltert |
| `Serilog.Sinks.Async` + `Seq`/`Loki` | mächtig | externe Abhängigkeit, Aufwand, Overkill für eine Heizungsanlage |
| Ringpuffer mit `Queue<T>` | Standardstruktur | wächst intern, Allokation beim Verwerfen, nicht konstanter Speicherbedarf |
| Persistenter Logpuffer (SQLite) | überlebt Neustarts | der Puffer soll die *aktuelle* Sitzung zeigen; die Datei-Sink kümmert sich um Historie |

## Konsequenzen

### Positiv

- Der Betreiber sieht **ohne SSH und ohne NAS-Logs**, was das Gateway tut
- Fehlersuche direkt in der Oberfläche – ein Klick statt „Container-Log öffnen"
- Strukturierte JSON-Logs sind maschinell auswertbar (`jq`, Log-Auswertung)
- Der BACnet-Stack wird sichtbar – Befund A16 wird **behoben**
- ~400 KB Speicherbedarf sind vernachlässigbar
- Ein Provider für alle Ausgaben – keine doppelte Logdatei, kein `Console.WriteLine`

### Negativ

- Serilog ist eine zusätzliche Abhängigkeit (NuGet, `Directory.Packages.props`)
- Der In-Memory-Puffer ist **flüchtig** – nach einem Neustart ist er leer
  (die Datei-Sink übernimmt die Historie)
- Der Puffer enthält **alle** `Information`-Einträge und damit auch viel Rauschen –
  Filter in der UI sind Pflicht, keine Kür
- Ein eigener Sink muss getestet werden (Überschreiben des ältesten Eintrags)

### Neutral

- Die Kapazität ist zur Laufzeit nicht änderbar (E5)
- Zwei Ringpuffer existieren nebeneinander (E4) – die UI trennt sie durch verschiedene
  Reiter

## Verwandte Dokumente

- [logging.md](../02-modernisierung/logging.md) – die vollständige Spezifikation
- [dotnet-migration.md §5](../02-modernisierung/dotnet-migration.md#5-logging) – Serilog-Basis-Setup
- [health-und-status.md §4](../02-modernisierung/health-und-status.md#4-fehler-sammlung) – Fehler-Ringpuffer
- [ui-angular-optimus.md §7](../02-modernisierung/ui-angular-optimus.md#7-diagnoseseite) – Diagnoseseite
- [datenmodell-und-api.md §3](../02-modernisierung/datenmodell-und-api.md#3-rest-api) – REST-Endpunkte
- [befunde.md](../01-ist-zustand/befunde.md) – Befund A16 (`Trace` statt Logging)
