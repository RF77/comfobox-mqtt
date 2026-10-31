# port-bacnet

Werkzeuge für die Portierung des Legacy-BACnet-Stacks (`src_old/Bacnet/`) nach
`src/ComfoBox.Bacnet/`.

## `port-bacnet-base.mjs`

Portiert `src_old/Bacnet/BACnetBase.cs` nach `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs`.

```bash
node tools/port-bacnet/port-bacnet-base.mjs \
     src_old/Bacnet/BACnetBase.cs \
     src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs
```

Das Skript macht **nur** drei Dinge:

1. entfernt die Klassen `BVLC` (BACnet/IP) und `PTP` – der Stack läuft
   ausschließlich über MS/TP (Aufgabe 1.6),
2. zieht den Namespace von `System.IO.BACnet` auf `ComfoBox.Bacnet`,
3. ersetzt `System.Diagnostics.Trace` durch die `ILogger`-Fassade
   `BacnetLog` (Befund A16).

Der Codec wird **nicht** umgeschrieben. Das Skript ist idempotent: ein
erneuter Lauf erzeugt dieselbe Datei.

> **Warum ein Skript statt eines Ports von Hand?** Der Codec ist 7.500 Zeilen
> Fremdcode. Ein Skript garantiert, dass die Portierung **mechanisch** ist und
> damit dem Vertrag „1:1 portieren, keine Verbesserungen" entspricht.

## `oracle/` – Referenz-Oracle für die Golden-Fixture

`oracle/` ist ein **Werkzeug**, kein Teil der Solution: es wird von CI **nicht**
gebaut und liegt bewusst außerhalb von `src/`.

Es kompiliert den Legacy-Codec `src_old/Bacnet/BACnetBase.cs` **einmalig** und
protokolliert dessen Ausgabebytes. Das Ergebnis ist die Golden-Fixture
`src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`.

```bash
cd tools/port-bacnet/oracle
dotnet run -c Release -- ../../../src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json
```

> **Wichtig:** Die Fixture wird **nicht** verändert, wenn sich der Legacy-Code
> ändert. Sie ist der Vertrag. Der `--check` in der CI prüft, ob der Katalog
> noch zur Fixture passt.

### Warum es einen Oracle braucht

Die Aufgabenbeschreibung der Phase 1 nimmt an, `docs/Mono/MoMA Scan Results.html`
enthalte aufgezeichnete BACnet-Frames. Das ist **nicht** der Fall: die Datei
ist ein **Mono-Missing-Analysis-Report** (welche BCL-Methoden fehlen in Mono) und
enthält **keinen einzigen** Hex-Dump. Prüfsumme der Originaldatei:

| Datei | SHA-256 |
|---|---|
| `docs/Mono/MoMA Scan Results.html` | `012C9862185C83B6E3432BDABD3C8C39F5EC57884CD837F92E399BFB2C14822F` |

Die Golden-Frames stammen daher vom Legacy-Codec selbst – dem einzigen
verlässlichen Referenz-Oracle, das es gibt.

## Geprüfte Frames

| Art | Anzahl | Beispiel |
|---|---|---|
| `iam` | 2 | `C4 02 00 00 01 22 01 E0 91 00 21 00` |
| `whois` | 1 | `09 00 1B 3F FF FF` |
| `readProperty` | 4 | `0C 00 80 00 3E 19 55` |
| `writeProperty` | 4 | `0C 00 80 00 3E 19 55 3E 44 41 AC 00 00 3F 49 10` |
| `readPropertyAcknowledge` | 1 | `0C 00 80 00 3E 19 55 3E 44 41 AC 00 00 3F` |
| `error` | 1 | `91 02 91 20` |
| `confirmedRequest` | 1 | `00 13 01 0C` |
| `unconfirmedRequest` | 1 | `10 00 00` |
| `simpleAck` | 1 | `20 01 0F 07` |
| `complexAck` | 1 | `30 01 0C 03 00 00` |
| `apduError` | 1 | `50 01 0C 09` |
| `abort` | 1 | `70 04 01` |
| `segmentAck` | 1 | `40 02 01 04` |
| `npdu` | 2 | `01 04 …` |
| `address*` | 3 | `01 02 03 04 05 06` |
| **Summe** | **25** | |
