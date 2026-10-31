---
title: Prompt-Phase-1
description: Kopierfertiger Session-Prompt für die Phase 1 (BACnet-Portierung & Golden-Tests)
order: 1
---

# Prompt – Phase 1: BACnet-Portierung & Golden-Tests

> Kopierfähiger Prompt für die Session **nach** Phase 0.
>
> ⚠️ **Erst verwenden, wenn Phase 0 abgeschlossen ist.** Voraussetzung ist ein
> grüner `dotnet build src/ComfoBox.sln -c Release` und `dotnet test` (0 Tests
> sind erlaubt).

---

## Prompt (zum Kopieren)

````text
Ich arbeite am ComfoBox-MQTT-Gateway in c:\_git\rf77\comfobox-mqtt.
Bitte setze die **Phase 1** des Aufgaben-Dokuments um: BACnet-Portierung &
Golden-Tests.

Lies zuerst (in dieser Reihenfolge):
1. .github/copilot-instructions.md              ← die Projektregeln
2. .github/instructions/bacnet.instructions.md  ← die BACnet-Regeln
3. docs/06-aufgaben/README.md                   ← wie eine Session abläuft
4. docs/06-aufgaben/status.md                   ← Stand der Phase 0
5. docs/06-aufgaben/phase-1.md                  ← die Aufgaben 1.1 bis 1.8
6. docs/02-modernisierung/bacnet-portierung.md  ← die verbindliche Spec
7. docs/04-migration/teststrategie.md           ← Teststrategie

Ziel der Phase 1: Der Protokollstack läuft verhaltensgleich auf .NET 10.

⚠️ WICHTIGSTE REGEL DIESER PHASE:
Golden-Tests ZUERST, Portierung DANACH. Der Codec (BACnetBase) wird 1:1
übernommen – KEINE "Verbesserungen" während der Portierung. Die Golden-Tests
müssen vor der Portierung existieren und danach OHNE Fixture-Änderung grün sein.

Stand: **1.1, 1.2 und 1.3 sind fertig** (siehe status.md / protokoll.md vom
2026-10-31). Offen sind 1.4 (BACnetClient), 1.5 (BACnetTransport), 1.6
(Pipe/UDP/PTP in den Transports), 1.7 (Trace in Client/Transport) und 1.8
(Timeout-/Reconnect-Härtung).

Reihenfolge (aus phase-1.md):
- 1.4 BACnetClient portieren (2.098 Zeilen) → async/await, kein .Result
- 1.5 BACnetTransport portieren (2.005 Zeilen, MSTP + Serial) → A8-Spin-Loop-Fix
- 1.6 Pipe-/UDP-/PTP-Transport ENTFERNEN (im Codec bereits erledigt)
- 1.7 Trace → ILogger (im Codec bereits erledigt, Client/Transport offen)
- 1.8 Timeout-/Reconnect-Härtung

Hinweise, die ich schon verifiziert habe – bitte nicht neu recherchieren:
- ✅ `docs/Mono/MoMA Scan Results.html` enthält **KEINE BACnet-Frames** (es ist ein
  Mono-Missing-Analysis-Report). Die Golden-Frames kommen aus dem Legacy-Codec
  selbst, erzeugt über `tools/port-bacnet/oracle/`. Die Fixture
  `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json` (25 Frames) ist
  eingefroren und darf **nicht** geändert werden.
- ✅ Der Codec ist portiert: `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs`.
  Das Port-Skript ist `tools/port-bacnet/port-bacnet-base.mjs` – **benutze es**,
  statt den Codec von Hand umzubauen. Es entfernt `BVLC` + `PTP`, zieht den
  Namespace um und ersetzt `Trace` durch `BacnetLog`.
- ✅ `dotnet test src/ComfoBox.sln -c Release` → 26 Tests grün. Das ist die
  Messlatte: nach 1.4/1.5 müssen sie **ohne** Fixture-Änderung grün bleiben.
- ⚠️ `BACnetBase.cs` referenziert `BacnetIpUdpProtocolTransport` aus
  `BACnetTransport.cs` – der Codec ist nicht standalone. Für 1.4/1.5 gilt das
  umgekehrt: `BACnetTransport.cs` braucht den Codec.
- ⚠️ Der Legacy-Codec braucht `#nullable disable` (7.500 Zeilen) – mit
  `Nullable=enable` + `TreatWarningsAsErrors` sind das ~40 Fehler. Das Projekt
  `ComfoBox.Bacnet` hat daher `EnableNETAnalyzers=false` und ein `NoWarn`
  (CS1591, CS0162, CS1572, CS1587).
- ⚠️ `CA1707` moniert Unterstriche in Bezeichnern – auch in **Testmethodennamen**.
  Die sind hier Haustil → `NoWarn CA1707` in den Testprojekten.
- ⚠️ `Services.DecodeIamBroadcast` gibt bei Erfolg **0** zurück
  (`return offset - org_offset`), nicht die Länge. Golden-Test schreibt das fest.
- ⚠️ `dotnet new web` liefert Pakete, die der Web-SDK schon mitbringt → `NU1510`.
- ⚠️ `RestorePackagesWithLockFile` braucht das TFM **in der Projektdatei**,
  nicht nur in `Directory.Build.props` → sonst `NETSDK1013`.
- src_old/ ist Archiv: lesen ist erlaubt und erwünscht, ändern nie.
- Baue immer aus src/ (cd src), damit global.json greift. Baue nie vom Repo-Root.
- Die Zeitlimits des MSTP-Frame-Encoders sind der heikelste Teil des Projekts.
  Keine "Aufrundung" zu Gunsten der Lesbarkeit – der alte Code ist die Norm.
- Jeder Test braucht einen deterministischen Zeitgeber (TimeProvider), keine
  echten Sleeps.

Regeln für diese Session:
- Nur die Phase 1 umsetzen, nichts darüber hinaus.
- Der Spec ist verbindlich. Bei Widerspruch Spec ↔ Aufgabe: Aufgabe anpassen,
  Spec nicht – und im Protokoll vermerken.
- Bestehende Tests nicht löschen oder abschwächen, um grün zu werden.
- Nach jeder Aufgabe: Status in docs/06-aufgaben/status.md setzen
  (⬜ offen / 🟡 in Arbeit / ✅ fertig und verifiziert / ⛔ blockiert).
- Am Ende: Eintrag in docs/06-aufgaben/protokoll.md (Datum, Aufgabe, Ergebnis,
  Verifikation, offene Punkte, Überraschungen).
- Wenn eine neue Entscheidung nötig ist: ADR anlegen und docs/README.md sowie
  docs/05-entscheidungen/README.md aktualisieren.
- Keine Secrets, keine Passwörter in Dateien oder Logs.
- Sprache: Code-Bezeichner englisch, Kommentare und Doku deutsch.

Am Ende der Session:
1. Führe alle Verifikationsbefehle aus phase-1.md aus und zeige das Ergebnis.
   Der wichtigste ist: die Golden-Tests sind grün, UND die Fixture-Datei ist
   byte-identisch zum Ausgangszustand (Prüfsumme vergleichen!).
2. Aktualisiere status.md und protokoll.md.
3. Gib mir am Schluss den Prompt für die nächste Session aus (Phase 2), in
   derselben Form wie dieser Prompt – inklusive der Fallstricke, die in dieser
   Session aufgetreten sind.
````

---

## Aufgaben

| Aufgabe | PT | Titel |
|---|---|---|
| 1.1 | 0.5 | Golden-Frames extrahieren |
| 1.2 | 1.0 | Golden-Tests gegen den alten Code |
| 1.3 | 1.5 | Codec portieren |
| 1.4 | 1.0 | Item-Metadaten / Objekt-IDs |
| 1.5 | 1.5 | MSTP-Transport portieren |
| 1.6 | 0.5 | Pipe/UDP/PTP entfernen |
| 1.7 | 0.5 | `Trace` → `ILogger` |
| 1.8 | 1.0 | Timeout-Härtung + Spin-Loop-Fix |
| | **8.0** | **Summe** |

---

## Risiko dieser Phase

> ⚠️ **Höchstes Risiko im Projekt.** Das Meilenstein **M1** („BACnet verhaltensgleich
> auf .NET 10") hängt an dieser Phase. Wenn die Golden-Tests nicht **vor** der
> Portierung existieren, ist die Verhaltensgleichheit nicht beweisbar.

| Risiko | Gegenmaßnahme |
|---|---|
| „Verbessern" beim Portieren | Codec 1:1 übernehmen, Abweichungen nur mit Begründung |
| Fixture wird angefasst | Prüfsumme vor und nach der Session vergleichen |
| Timing-Verhalten driftet | deterministischer `TimeProvider`, keine echten Sleeps |
| Alt-Code wird zum Build-Problem | net452-Projekte bleiben **außerhalb** der Solution |

---

## Verwandte Dokumente

- [phase-1.md](../phase-1.md) – die Aufgaben im Detail
- [bacnet-portierung.md](../../02-modernisierung/bacnet-portierung.md) – die Spec
- [teststrategie.md](../../04-migration/teststrategie.md) – Teststrategie
- [Prompt-Phase-0.md](Prompt-Phase-0.md) – der Prompt der Vorsession
