---
description: "Use when changing the BACnet MS/TP stack, the serial RS485 transport, or the ComfoBox device protocol code — framing, timeouts, MSTP state machine, BACnet object IDs, or serial port handling and reconnect behaviour."
applyTo: ["src/ComfoBox.Bacnet/**/*.cs", "src/Bacnet/**/*.cs", "src/ComfoBox.Domain/**/*.cs"]
---

# BACnet / Serielle Schnittstelle – Regeln

## 1. Was hier lebt

| Bereich | Ort |
|---|---|
| BACnet-Codec (Structs, Encode/Decode) | `src/ComfoBox.Bacnet/Bacnet/BacnetCodec.cs` (aus `src/Bacnet/BACnetBase.cs`) |
| Protokoll-Logik (Read/Write Property, WhoIs/IAm) | `src/ComfoBox.Bacnet/Bacnet/BacnetClient.cs` |
| MSTP-Zustandsmaschine | `src/ComfoBox.Bacnet/Bacnet/BacnetMstpTransport.cs` |
| Serien-Port | `src/ComfoBox.Bacnet/Serial/SerialPortFactory.cs` |
| Item-Katalog (BACnet-Objekt-IDs) | `src/ComfoBox.Domain/Items/Catalog.cs` |

> **Herkunft:** `src/Bacnet` ist eine Portierung von `bacnet.sourceforge.net`
> (Morten Kvistgaard, **MIT**). Die MIT-Lizenz **muss** erhalten bleiben →
> `LICENSE-bacnet.md` im neuen Projekt ablegen, Urheber nennen.

## 2. Portierungsregeln (während der Migration)

| Regel | Grund |
|---|---|
| **Codec 1:1 übernehmen** | jedes Byte zählt; „Verbesserungen" sind Verhaltensänderungen |
| **Golden-Tests zuerst** | Fixtures aus `docs/Mono/MoMA Scan Results.html` |
| **Keine Fixture-Änderung** nach der Portierung | ein Fehlschlag = Verhaltensänderung → zurück zur Analyse |
| `Trace` → `ILogger` | 26 Fundstellen, im Release unsichtbar (Befund A16) |
| `throw ex` → `throw` | 3 Fundstellen |
| `m_invoke_id++` → `Interlocked.Increment` | 20+ Fundstellen, nicht threadsicher |
| `lock (this)` → eigenes `SemaphoreSlim` | öffentlich sperrbar |
| `ThreadPriority.Highest` → `Normal` | OS-Tasks nicht starven |
| Pipe-/UDP-/PTP-Transport **löschen** | ungenutzt; `kernel32.dll`-P/Invoke und `UdpClient.BeginReceive` verhindern Linux |

## 3. Hardware-Fakten (nicht „korrigieren")

| Parameter | Wert | Quelle |
|---|---|---|
| Baudrate | **38400** (ComfoBox muss darauf umgestellt + neu gestartet werden) | README, Settings |
| Parität | `None` | `BACnetTransport.cs:400` |
| Datenbits / Stopbits | 8 / 1 | `BACnetTransport.cs:400` |
| MSTP This-Station | 3 (`BacnetClientId`) | Settings |
| ELESTA Device-ID | 1 (`BacnetMasterId`) | Settings |
| `MaxInfoFrames` | 1 | Transport-Default |

### MSTP-Timings (nicht ändern!)

| Konstante | Wert |
|---|---|
| `T_FRAME_ABORT` | 80 ms |
| `T_NO_TOKEN` | 500 ms |
| `T_REPLY_TIMEOUT` | 295 ms |
| `T_USAGE_TIMEOUT` | 95 ms |
| `T_REPLY_DELAY` | 250 ms |
| `m_max_poll` | 50 |
| `MaxBufferLength` | 502 |

> Änderungen an diesen Werten verändern das Zeitverhalten des Busses und damit die
> Zuverlässigkeit. Sie brauchen eine Begründung im PR.

## 4. Zustandsmaschine

```
Reset → Initialize → Idle ⇄ (PollForMaster | PassToken | UseToken | WaitForReply
                                           | AnswerDataRequest) → DoneWithToken
```

- Der MSTP-Thread läuft als **Background-Thread**
- Alle Zugriffe auf den Port werden über **ein** `SemaphoreSlim` serialisiert
- `m_port == null` **muss** nach einem Port-Fehler gesetzt werden →
  die Bedingung `while (m_port != null)` beendet die Schleife.
  **Das war der 100-%-CPU-Bug (Befund A8).**

## 5. Serial-Port: Regeln

````csharp
// ✅ Port öffnen, benutzen, bei Fehler schließen
try
{
    _port = SerialPortFactory.Create(_options);
    _port.Open();
}
catch (Exception ex)
{
    _logger.LogError(ex, "Seriellen Port {Port} nicht öffnbar", _options.Port);
    await SafeClosePortAsync();     // _port = null, Port geschlossen
    return false;                   // → Retry mit Backoff
}
````

| Regel | Grund |
|---|---|
| Port nach Fehler **schließen** | sonst hält der Prozess das Device-Node |
| **Kein** Endlos-Retry ohne Backoff | Bus/Adapter-Hotplug |
| `ReadTimeout`/`WriteTimeout` **setzen** | sonst blockiert der Read unbegrenzt |
| Kein `DtrEnable`/`RtsEnable` | Some CH340-Adapter reagieren darauf empfindlich |
| Port **nur** öffnen, wenn `PortName` konfiguriert ist | Start ohne Hardware muss möglich sein |
| Kein `SerialPort` in statischen Feldern | nicht testbar, nicht parallelisierbar |

> **Wichtig:** Der Port darf **nicht** dauerhaft offen gehalten bleiben, wenn nichts gelesen
> wird – unter Linux blockiert ein offenes tty den anderen Prozess (z. B. `serial-getty` auf
> dem NAS).

## 6. Timeout- und Retry-Strategie

| Ebene | Wert | Verhalten |
|---|---|---|
| Einzelner Lesevorgang | `ResponseTimeoutMs` (1000) | 3 Retries → max. 3 s |
| Item-Timeout (gesamt) | `ItemTimeoutMs` (3000) | Abbruch, Poll-Loop weiter |
| Reconnect (Port) | `ReconnectDelaySeconds` (5) + Backoff | Port neu öffnen, `WhoIs`, `IAm` abwarten |
| `IAm`-Timeout | `MqttNoResponseTimeout` (30 s) | Start gilt als fehlgeschlagen |

> **Ein einzelner Fehler darf den Gateway NICHT zerstören.** Im Legacy-Code führte ein
> BACnet-Fehler zu `Dispose()` + `throw` (Befund A7). Das wird nicht übernommen.

## 7. Item-Katalog (BACnet-Objekt-IDs)

- Die **BACnet-Objekt-ID** ist die Identität des Werts → unveränderlich
- Neue Item-Definition braucht: `Id`, `Group`, `Name`, `BacnetObjectId`, `Kind`,
  `Min`/`Max`/`Unit`, `IsWritable`, `RequiresExpertMode`, `Priority`, `Description`
- Alle BACnet-Objekte sind `OBJECT_ANALOG_VALUE` → die Instanznummer identifiziert
- Der Katalog ist **statisch** → keine Laufzeit-Erweiterung
- Neue Einträge müssen in `docs/topics.json` nachgezogen werden (Contract-Test-Fixture!)

## 8. Diagnose

- Jede Protokollentscheidung wird geloggt (`Debug` für Frame-Ebene)
- **Jeder** Fehler enthält: Objekt-ID, Zeitouts, Versuchszähler
- Die Health-Checks nutzen diese Informationen:
  `bacnet-connection` (IAm-Alter), `bacnet-read` (letzter erfolgreicher Read)

## 9. Tests

````csharp
[Fact]
public void EncodeReadPropertyRequest_MatchesGoldenBytes()
{
    var bytes = BacnetCodec.EncodeReadPropertyRequest(1, 0, 830, 85, invokeId: 7);
    Assert.Equal(Hex("…"), Convert.ToHexString(bytes));
}

[Fact]
public async Task SerialPort_Failure_DoesNotSpin()
{
    // Arrange: Port öffnen wird fehlschlagen
    // Act:     Read aufrufen
    // Assert:  Rückkehr innerhalb von X ms, CPU nicht am Limit
}
````

| Test | Prüft |
|---|---|
| `Encode*_MatchesGoldenBytes` | Request-Encoding unverändert |
| `Decode*_MatchesGoldenBytes` | Response-Decoding unverändert |
| `DecodeIAm_*` | Geräte-Erkennung |
| `MstpStateMachine_*` | Zustandsübergänge, Token-Verlust |
| `PortReopen_*` | Reconnect nach Fehler |
| `NoSpinOnPortError` | **Regression gegen Befund A8** |

## 10. Docker / Linux

- `System.IO.Ports` ist ein **NuGet-Paket**, keine BCL
- Unter Linux braucht der Prozess Zugriff auf `/dev/ttyUSB0` → `group_add: dialout`
- `/dev/ttyUSB0` ist nicht stabil → **`/dev/serial/by-id/…`** verwenden
- Der Port darf erst **nach** dem Öffnen des seriellen Ports geöffnet werden:
  1. Serial-Port öffnen
  2. `WhoIs` senden
  3. `IAm` abwarten
  4. Items pollen
