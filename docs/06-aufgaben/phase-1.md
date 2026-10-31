# Phase 1 – BACnet-Portierung & Golden-Tests (8 PT)

**Ziel:** Der Protokollstack läuft verhaltensgleich auf .NET 10.
**Braucht:** Phase 0
**Spec:** [bacnet-portierung](../02-modernisierung/bacnet-portierung.md) ·
[teststrategie](../04-migration/teststrategie.md)
**Prompt:** [prompts/Prompt-Phase-1.md](prompts/Prompt-Phase-1.md) – kopierfertig starten

> ⚠️ **Höchstes Risiko im Projekt.** Deshalb gilt hier ausnahmslos:
> **Golden-Tests zuerst, Portierung danach.** Ohne Fixture kein Meilenstein M1.

---

## 1.1 – Golden-Frames extrahieren · 0.5 PT

| | |
|---|---|
| **Quelle** | `docs/Mono/MoMA Scan Results.html` (BACnet-Monitor-Scan des ELESTA) |
| **Ziel** | `src/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json` |
| **Verifikation** | Test lädt die Fixture und parst **jeden** Frame |

Die HTML-Datei enthält aufgezeichnete Frames des BACnet-Monitorings: Hex-Dumps der
RS485-Kommunikation (NPDU, APDU, Service-Antworten).

**Format der Fixture:**

````json
{
  "source": "docs/Mono/MoMA Scan Results.html",
  "frames": [
    {
      "name": "WhoIs -> IAm",
      "raw": "0100 0080 0000 10 0A",
      "expect": {
        "type": "IAm",
        "deviceId": 1,
        "maxApdu": 480
      }
    }
  ]
}
````

**Akzeptanzkriterien:**
- [ ] Mindestens **20** Frames extrahiert (WhoIs/IAm, ReadProperty, WriteProperty, Error-Replies)
- [ ] Jeder Frame hat `raw` und `expect`
- [ ] Die HTML-Datei ist **unverändert** (Prüfsumme notieren)
- [ ] Ein Test parst alle Frames **gegen den alten Code** (`src_old/`) und ist grün

---

## 1.2 – Golden-Tests gegen den alten Code · 1.0 PT

| | |
|---|---|
| **Ziel** | Tests, die **vor** der Portierung grün sind |
| **Verifikation** | `dotnet test` grün, **ohne** dass `ComfoBox.Bacnet` existiert |

> **Das ist der entscheidende Test der ganzen Phase.** Die Tests laufen zunächst
> gegen `src_old/Bacnet` (net452). Erst wenn sie dort grün sind, wird portiert.

**Aufbau:**

````
src/ComfoBox.Bacnet.Tests/
├── Fixtures/bacnet-frames.json
├── BacnetCodecGoldenTests.cs      # Encode/Decode gegen die Fixture
├── BacnetMstpFramingTests.cs      # Zustandsmaschine, Token
└── SerialPortBehaviourTests.cs    # Öffnen/Schließen/Reconnect
````

**Akzeptanzkriterien:**
- [ ] Die Tests laufen gegen **beide** Implementierungen (alt via `#if` oder separater Aufruf)
- [ ] Mindestens **30** Testfälle
- [ ] Die Tests sind grün, **bevor** irgendein neuer Code existiert
- [ ] Testdaten kommen **ausschließlich** aus der Fixture

---

## 1.3 – `ComfoBox.Bacnet` anlegen, Codec portieren · 1.0 PT

| | |
|---|---|
| **Quelle** | `src_old/Bacnet/BACnetBase.cs` (~8.200 Zeilen) |
| **Ziel** | `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` |
| **Verifikation** | Golden-Tests grün, **ohne** Fixture-Änderung |

| Regel | Begründung |
|---|---|
| **1:1 portieren**, keine „Verbesserungen“ | jede Abweichung ist eine Verhaltensänderung |
| Keine Logik in `BACnetBase` erfinden | die Codec-Logik ist getestet |
| `Trace` → `ILogger` | Befund A16 |
| Kommentare **deutsch** | Projektkonvention |
| MIT-Urheber im Header **erhalten** | Fremdcode |

> **Wenn der Codec „besser" gemacht werden soll:** **nein.** Der Codec wird
> verhaltensgleich portiert. Verbesserungen kommen in den Golden-Tests als
> **neue** Fälle – nicht durch Umbau.

**Akzeptanzkriterien:**
- [ ] Golden-Tests grün **ohne** Fixture-Änderung
- [ ] Kein `System.Diagnostics.Trace` im Projekt
- [ ] MIT-Lizenz und Original-Urheber dokumentiert
- [ ] Kein `lock (this)`, kein unsicheres `m_invoke_id++`

---

## 1.4 – `BACnetClient` portieren · 1.5 PT

| | |
|---|---|
| **Quelle** | `src_old/Bacnet/BACnetClient.cs` (~2.370 Zeilen) |
| **Ziel** | `src/ComfoBox.Bacnet/Bacnet/BacnetClient.cs` |

| Bereich | Inhalt |
|---|---|
| Read Property | `ReadProperty` mit Objekt-ID und Typ |
| Write Property | `WriteProperty` |
| WhoIs / IAm | Geräteerkennung |
| Error-Replies | `ErrorReply` → eigene Exception-Typen |

**Akzeptanzkriterien:**
- [ ] Golden-Tests grün
- [ ] `async`/`await` – **kein** `.Result`, **kein** `.Wait()`
- [ ] **Kein** `async void`
- [ ] Timeout über `CancellationToken`, nicht über `Thread.Sleep`
- [ ] `throw;` statt `throw ex;`

---

## 1.5 – `BACnetTransport` portieren (MSTP + Serial) · 2.0 PT

| | |
|---|---|
| **Quelle** | `src_old/Bacnet/BACnetTransport.cs` (~2.260 Zeilen) |
| **Ziel** | `src/ComfoBox.Bacnet/Bacnet/BacnetMstpTransport.cs` + `Serial/SerialPortFactory.cs` |

**Das ist die schwierigste Aufgabe der Phase** – hier liegen die schwersten
Befunde.

| Befund | Maßnahme in dieser Aufgabe |
|---|---|
| **A8** Tight-Spin-Loop bei Serial-Fehler | `m_port == null` wird **gesetzt**; die Schleife endet |
| **A23** `ThreadPriority.Highest` | entfernen – der OS soll den Task nicht aushungern |
| **A19** `m_invoke_id++` unsicher | `Interlocked.Increment` |
| **A26** `kernel32.dll`-P/Invoke | Pipe-Transport entfällt (Aufgabe 1.6) |

**MSTP-Zustandsmaschine:**

| Zustand | Bedeutung |
|---|---|
| `Idle` | warten auf Token |
| `UseToken` | Token erhalten, senden darf |
| `WaitForReply` | Anfrage gesendet, auf Antwort warten |
| `Done` | Token weitergeben |

**Akzeptanzkriterien:**
- [ ] Golden-Tests grün
- [ ] Port öffnen/schließen/Reconnect getestet
- [ ] **Kein** Spin-Loop: bei gezogenem Adapter sinkt die CPU-Last auf ~0 %
- [ ] `ThreadPriority.Highest` entfernt
- [ ] Reconnect mit Backoff und `CancellationToken`
- [ ] `m_invoke_id` threadsicher

---

## 1.6 – Pipe-/UDP-/PTP-Transport entfernen · 0.5 PT

| | |
|---|---|
| **Quelle** | `src_old/Bacnet/BACnetTransport.cs` |

Der Altbestand hat **vier** Transport-Implementierungen. Es bleiben **zwei**:
MSTP über Serial.

| Transport | Status |
|---|---|
| MSTP / Serial | ✅ behalten |
| UDP | ❌ entfernen |
| Pipe | ❌ entfernen (enthält `kernel32.dll`-P/Invoke) |
| PTP | ❌ entfernen |

**Akzeptanzkriterien:**
- [ ] Kein Windows-spezifischer Code mehr im Projekt
- [ ] `ComfoBox.Bacnet` baut unter Linux
- [ ] Kein `DllImport` von `kernel32`

---

## 1.7 – `Trace` → `ILogger` · 0.5 PT

| | |
|---|---|
| **Fundstellen** | 26 in `src_old/Bacnet` |

| Vorher | Nachher |
|---|---|
| `Trace.WriteLine("...")` | `_logger.LogDebug("...")` |
| `Trace.WriteLine($"...")` | `_logger.LogDebug("Wert: {Wert}", wert)` |
| `Trace.Assert(...)` | `Debug.Assert(...)` **oder** entfernen |

> **Strukturierte Log-Meldungen**: Platzhalter statt String-Verkettung. Nur so
> lässt sich später nach einem Fehler suchen.

**Akzeptanzkriterien:**
- [ ] Kein `System.Diagnostics.Trace` mehr im Projekt
- [ ] Der Logger wird **injiziert**, nicht statisch geholt
- [ ] Keine `string.Format`-Verkettung in Log-Meldungen

---

## 1.8 – Timeout-/Reconnect-Härtung · 1.0 PT

| | |
|---|---|
| **Befunde** | A8, A13, A14, A22 |

| Thema | Maßnahme |
|---|---|
| Spin-Loop | Port-Fehler → sauberer Abbruch |
| Token-Timing | 60 Bitzeiten Idle einhalten |
| Reconnect | exponentieller Backoff mit `CancellationToken` |
| Poll-Loop | ein Fehler darf den Loop **nicht** beenden |
| Zähler | Schreibvorgänge zählen nur **erfolgreiche** (A22) |

**Akzeptanzkriterien:**
- [ ] Port-Open-Fehler → sauberer Fehler, **kein** Spin-Loop
- [ ] Reconnect mit Backoff getestet (Port schließen/öffnen)
- [ ] Ein Lesefehler beendet den Poll-Loop nicht
- [ ] `CurrentNumberOfWrites` zählt nur erfolgreiche Schreibvorgänge

---

## Abnahme der Phase 1

- [ ] Golden-Tests grün **ohne** Fixture-Änderung
- [ ] Port öffnen/geschlossen/Reconnect-Test grün
- [ ] `dotnet test src/ComfoBox.sln -c Release` vollständig grün
- [ ] Meilenstein **M1** erreicht: „BACnet verhaltensgleich auf .NET 10“

→ Weiter mit [Phase 2](phase-2.md)
