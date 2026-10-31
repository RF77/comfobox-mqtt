---
title: Prompt-Phase-2
description: Kopierfertiger Session-Prompt für die Phase 2 (Domain & Gateway-Kern)
order: 2
---

# Prompt – Phase 2: Domain & Gateway-Kern

> Kopierfähiger Prompt für die Session **nach** Phase 1.
>
> ⚠️ **Erst verwenden, wenn Phase 1 abgeschlossen ist.** Voraussetzung ist ein
> grüner `dotnet build src/ComfoBox.sln -c Release` und `dotnet test`
> (45 Tests grün).

---

## Prompt (zum Kopieren)

````text
Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze die **Phase 2** des Aufgaben-Dokuments um: Domain & Gateway-Kern.

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md              ← die Projektregeln
2. .github/instructions/csharp.instructions.md   ← die C#-Regeln
3. docs/06-aufgaben/README.md                   ← wie eine Session abläuft
4. docs/06-aufgaben/status.md                   ← Stand nach Phase 1
5. docs/06-aufgaben/phase-2.md                  ← die Aufgaben 2.1 bis 2.7
6. docs/02-modernisierung/datenmodell-und-api.md  ← die verbindliche Spec
7. docs/02-modernisierung/logging.md            ← Spec für 2.7 (Serilog + Ringpuffer)
8. docs/05-entscheidungen/adr-011-logging-serilog-und-ringbuffer.md

Ziel der Phase 2: Der Gateway liest alle 127 Items und schreibt in den Cache –
noch OHNE MQTT.

Stand: **Phase 1 ist vollständig abgeschlossen** (1.1–1.8 ✅, siehe status.md /
protokoll.md vom 2026-10-31). Offen ist die gesamte Phase 2.

Reihenfolge (aus phase-2.md):
- 2.1 Item-Katalog (127 Items) aus src_old/ComfoBoxLib/Items/**
- 2.2 Werttypen (AnalogValue, EnumValue) – DateValue/CommandValue bleiben tot
- 2.3 Gateway-Host, DI, Options, Validierung
- 2.4 BacnetDevice (Serialisierung, Priorisierung, Timeouts)
- 2.5 ValueCache (letzter Wert + Zeitstempel, IsStale)
- 2.6 Persistenter Schreibzähler (A3 – der Schreibschutz)
- 2.7 Serilog-Setup + In-Memory-Ringbuffer (1000)

Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- ✅ `dotnet build src/ComfoBox.sln -c Release` → 0 Warnungen, 0 Fehler
- ✅ `dotnet test src/ComfoBox.sln -c Release` → **45 Tests grün**
  (26 Golden-Tests + 19 neue BACnet-Tests). Das ist die Messlatte: nach Phase 2
  müssen sie **ohne** Änderung grün bleiben.
- ✅ Die Golden-Fixture `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`
  ist **eingefroren** (SHA256
  `F95AA7633D6C563D309BE6A7A4AC6797CAB198E315BA365B205E7C5C81CE681B`).
  Nicht verändern, nicht neu erzeugen.
- ✅ Der BACnet-Stack liegt fertig unter `src/ComfoBox.Bacnet/`:
  - `Bacnet/BacnetClient.cs` – **async** Client. `ReadPropertyAsync(...)` liefert
    `IList<BacnetValue>`, `WritePropertyAsync(...)` schreibt. Typisierte Fehler:
    `BacnetTimeoutException`, `BacnetDeviceErrorException`, `BacnetDeviceAbortException`,
    `BacnetDecodeException`.
  - `Bacnet/BacnetMstpTransport.cs` – Port, Reconnect, exponentieller Backoff.
  - `Serial/ISerialPort.cs` + `Serial/ISerialPortFactory.cs` – Testnaht für Tests.
  - Der Client meldet IAm über das Ereignis `IamReceived` (mit `BacnetIamEventArgs`:
    Address, DeviceId, MaxApdu, Segmentation, VendorId).
- ✅ `src/ComfoBox.Domain/` ist **leer** (nur die csproj) – die Domäne wird
  komplett neu gebaut. `src/ComfoBox.Gateway/` hat nur `Program.cs` + `appsettings*.json`.
- ⚠️ **`ComfoBox.Domain` darf KEINE Abhängigkeit haben** – kein `ILogger`,
  kein `ComfoBox.Bacnet`, kein `System.IO.Ports`. Die BACnet-Objekt-IDs müssen
  als eigene Typen in der Domäne stehen (nicht `BacnetObjectId` aus dem Codec).
- ⚠️ **Befund A15:** Im Altbestand (`src_old/ComfoBoxLib/Items/**`) werden die
  Prioritäts-Attribute (`[Primary]`, `[Secondary]`) **gelesen und verworfen** –
  alle Items bekommen `RefreshPriority.None`. In 2.1 müssen sie **wirklich**
  ausgewertet werden (und das muss ein Test beweisen).
- ⚠️ **Befund A5:** Die Einheit `°C` steht im Altbestand als `"\u00b0C"` (Encoding-
  Problem der Legacy-Dateien) – in der Domäne korrekt als `"°C"`.
  Und: **jede** Zahlendarstellung/-umwandlung in der Domäne nutzt
  `CultureInfo.InvariantCulture`, ohne Ausnahme.
- ⚠️ **Befund A20:** `DateValue` und `CommandValue` sind tot und werden **nicht**
  wiederbelebt. `InitialOperationDate` bleibt leer.
- ⚠️ **Befund A3:** Der Schreibzähler ist im Altbestand **nicht** persistent.
  Er wird persistent (JSON-Datei im Datenverzeichnis), atomar geschrieben, und
  **nur bei erfolgreichen** Schreibvorgängen erhöht (A22). Kein Pfad darf ihn
  umgehen – der Zähler wird **im Writer** erhöht, nicht im Aufrufer.
- ⚠️ `dotnet test` meldet für `ComfoBox.Domain.Tests`, `ComfoBox.Contract.Tests`
  und `ComfoBox.Gateway.Tests` "kein Test verfügbar" – das ist **vorbestehend**
  (diese Projekte haben noch keine Tests) und kein Fehler.
- ⚠️ `create_file` **überschreibt keine vorhandene Datei** – bei "File already
  exists" die Datei erst löschen oder mit `replace_string_in_file` bearbeiten.
- ⚠️ `CA1707` moniert Unterstriche in Bezeichnern – auch in **Testmethodennamen**.
  Die sind Haustil → `NoWarn CA1707` in den Testprojekten (schon gesetzt).
- src_old/ ist Archiv: lesen ist erlaubt und erwünscht, ändern nie.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root.
- Die Item-Metadaten stehen in `src_old/ComfoBoxLib/Items/*.cs` als C#-Properties
  mit Attributen (`[Description]`, `[Primary]`, `[Secondary]`). Die Enum-Namen
  stehen in `src_old/ComfoBoxLib/Items/Enums/*.cs`. `docs/topics.json` ist die
  **Test-Fixture** für den Katalog-Vergleich – nicht verändern.
- Die Zeichencodierung der Legacy-Dateien ist **Windows-1252**, nicht UTF-8.
  Beim Lesen der Umlaute aufpassen (die Dateien sehen "falsch" aus).

Regeln für diese Session:
- Nur die Phase 2 umsetzen, nichts darüber hinaus. **Kein MQTT.**
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Die 45 grünen Tests aus Phase 1 müssen grün bleiben.
- Nach jeder Aufgabe: Status in docs/06-aufgaben/status.md setzen
  (⬜ offen / 🟡 in Arbeit / ✅ fertig und verifiziert / ⛔ blockiert).
- Am Ende: Eintrag in docs/06-aufgaben/protokoll.md (Datum, Aufgabe, Ergebnis,
  Verifikation, offene Punkte, Überraschungen).
- Wenn eine neue Entscheidung nötig ist: ADR anlegen und docs/README.md sowie
  docs/05-entscheidungen/README.md aktualisieren.
- Keine Secrets, keine Passwörter in Dateien oder Logs.
- Sprache: Code-Bezeichner englisch, Kommentare und Doku deutsch.

Am Ende der Session:
1. Führe alle Verifikationsbefehle aus phase-2.md aus und zeige das Ergebnis.
   Der wichtigste ist: `dotnet test src/ComfoBox.sln -c Release` ist grün und
   die Golden-Fixture ist byte-identisch (Prüfsumme vergleichen!).
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 3), in
   derselben Form wie dieser Prompt – inklusive der Fallstricke, die in dieser
   Session aufgetreten sind.
````

---

## Aufgaben

| Aufgabe | PT | Titel |
|---|---|---|
| 2.1 | 1.5 | Item-Katalog erstellen (127 Items) |
| 2.2 | 1.0 | Werttypen (`AnalogValue`, `EnumValue`) |
| 2.3 | 1.0 | Gateway-Host, DI, Options |
| 2.4 | 1.5 | `BacnetDevice` |
| 2.5 | 0.5 | `ValueCache` |
| 2.6 | 0.5 | Persistenter Schreibzähler |
| 2.7 | 1.0 | Serilog-Setup + In-Memory-Ringbuffer |

> **Summe:** 6 PT

---

## Verwandte Dokumente

- [phase-2.md](../phase-2.md) – die Aufgaben im Detail
- [datenmodell-und-api.md](../../02-modernisierung/datenmodell-und-api.md) – die Spec
- [logging.md](../../02-modernisierung/logging.md) – Spec für 2.7
