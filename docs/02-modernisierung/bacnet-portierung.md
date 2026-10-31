# BACnet-Portierung

> **Quelle:** Der zu portierende Stack liegt unter
> [`src_old/Bacnet/`](../../src_old/Bacnet) und ist **Fremdcode** (MIT, von
> bacnet.sourceforge.net). Das Ziel entsteht neu in
> [`src/ComfoBox.Bacnet/`](../../src).

Der Protokollstack in `src_old/Bacnet` (~12.800 Zeilen) ist **Fremdcode** ohne Tests und die größte
Portierungsrisiko-Quelle. Dieses Dokument beschreibt, wie er sicher auf .NET 10 kommt.

## 1. Bestandsaufnahme des Fremdcodes

| Datei | Größe | Inhalt |
|---|---|---|
| `BACnetBase.cs` | ~8.200 Z. | Structs, Encoder/Decoder (Tag → Bytes) |
| `BACnetClient.cs` | ~2.370 Z. | Protokoll-Logik (Read/Write Property, WhoIs/IAm) |
| `BACnetTransport.cs` | ~2.260 Z. | 4 Transport-Implementierungen + MSTP-State-Machine |

Herkunft: Portierung von `bacnet.sourceforge.net` (Morten Kvistgaard), MIT-Lizenz.
Die MIT-Lizenz des Originals **muss** beim Portieren erhalten bleiben
(LICENSE-Datei im neuen Projekt ablegen, Urheber nennen).

## 2. Strategie: Portieren, nicht neu schreiben

| Option | Bewertung |
|---|---|
| **A. 1:1 portieren** | ✅ Gewählt. Der Stack funktioniert im Feld. Risiko liegt im Kodieren/Decoding, nicht in der Architektur. |
| B. Neu implementieren | ❌ 12.800 Z. BACnet-Protokollcode neu zu schreiben ist ohne Golden-Tests nicht verantwortbar. |
| C. Bestehende C-Bibliothek einbinden | ❌ Plattformabhängigkeit, Build-Komplexität auf der DS923+. |

## 3. Was bleibt, was fliegt raus

| Komponente | Entscheidung | Begründung |
|---|---|---|
| `BacnetSerialPortTransport` | **portieren** | wird genutzt |
| `BacnetMstpProtocolTransport` | **portieren** | wird genutzt |
| `BacnetClient` | **portieren** | wird genutzt |
| `BACnetBase` (Codec) | **portieren** | wird genutzt |
| `BacnetPipeTransport` | **verwerfen** | Windows-only (`kernel32.dll!PeekNamedPipe`), ungenutzt, verhindert Linux-Build (Befund A26) |
| `BacnetIpUdpProtocolTransport` | **verwerfen** | ungenutzt; `UdpClient.BeginReceive` existiert in .NET Core nicht mehr (Befund B12) |
| `BacnetPtpProtocolTransport` | **verwerfen** | ungenutzt |

> **Effekt:** Die 4–6 harten Portierungsprobleme (`BeginReceive`, `ExclusiveAddressUse`,
> `IPAddress.IPv4Mask`, `kernel32`-P/Invoke) entfallen vollständig.

## 4. Portierungsregeln

### 4.1 `System.IO.Ports`

In .NET Framework ist `System.IO.Ports` Teil der BCL, in .NET 10 nicht:

````xml
<PackageReference Include="System.IO.Ports" Version="10.0.0" />
````

Das Paket unterstützt Linux/macOS/Windows. Auf Linux braucht der Prozess Zugriff auf
`/dev/ttyUSB0` (Rechte via `dialout`-Gruppe, siehe
[synology-ds923](../03-deployment/synology-ds923.md)).

### 4.2 `System.Diagnostics.Trace` → `ILogger`

26 Fundstellen. Der BACnet-Stack ist heute **nicht diagnostizierbar** (Befund A16).

````csharp
// alt
Trace.WriteLine("Connection Error");

// neu
internal sealed class BacnetMstpTransport(ILogger<BacnetMstpTransport> logger, …)
{
    logger.LogWarning("BACnet MSTP connection error in {Method}", nameof(Idle));
}
````

**Regel:** Jede `Trace`-/`Debug`--Ausgabe wird zu `LogDebug`/`LogWarning`/`LogError`.
`LogDebug` nur für Frame-Ebene, sonst wird das Log unlesbar.

### 4.3 `throw ex` → `throw`

3 Fundstellen in `BACnetClient.cs` (z. B. `:932`).

### 4.4 `m_invoke_id++` threadsicher

20+ Fundstellen. Der Ist-Zustand ist nur durch `lock(this)` gerettet. Lösung: `Interlocked.Increment`
oder – besser – ein `Channel`/`SemaphoreSlim` als einziger Serialisierungspunkt.

### 4.5 `ThreadPriority.Highest` entfernen

Der MSTP-Thread läuft mit `ThreadPriority.Highest` (Befund A23). In .NET wird der Thread als
`Task`/`Thread` mit `ThreadPriority.Normal` gestartet. Priorität erhöht man nur im Notfall.

### 4.6 `System.Net.NetworkInformation` / `IPAddress.IPv4Mask`

Entfällt mit dem UDP-Transport (4.3).

## 5. Der Serial-Port: Crash-Sicherheit

**Befund A8 (P0):** Bei einem Serial-Fehler läuft `while (m_port != null)` endlos weiter →
100 % CPU. Und: Es gibt **keinen** Reconnect auf Port-Ebene.

### Soll

````csharp
// src/ComfoBox.Bacnet/Serial/SerialPortFactory.cs
internal static class SerialPortFactory
{
    public static SerialPort Create(BacnetOptions options)
    {
        var port = new SerialPort(options.Port, options.Baudrate, Parity.None, 8, StopBits.One)
        {
            ReadTimeout = options.ResponseTimeoutMs,
            WriteTimeout = options.ResponseTimeoutMs,
            Handshake = Handshake.None,
            DtrEnable = false,
            RtsEnable = false,
        };
        return port;
    }
}
````

Der Port wird **nicht** dauerhaft geöffnet gehalten, sondern:

1. geöffnet,
2. `WhoIs` gesendet,
3. `IAm` abgewartet (mit Timeout),
4. bei Erfolg: für die Session geöffnet gehalten,
5. bei Fehler: **geschlossen** und mit Backoff neu geöffnet.

Der Fehlerpfad ist damit explizit:

````csharp
private async Task<bool> EnsurePortOpenAsync(CancellationToken ct)
{
    if (_port is { IsOpen: true })
        return true;

    try
    {
        _port = SerialPortFactory.Create(_options);
        _port.Open();
        _logger.LogInformation("Serieller Port geöffnet: {Port} @ {Baudrate}",
                               _options.Port, _options.Baudrate);
        return true;
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "Seriellen Port {Port} nicht öffnbar", _options.Port);
        await SafeClosePortAsync();
        return false;
    }
}

private async Task SafeClosePortAsync()
{
    try { _port?.Close(); } catch { /* ignore */ }
    finally { _port?.Dispose(); _port = null; }
}
````

> **Effekt:** Kein Spin-Loop mehr (Port ist `null` nach Fehler), automatischer Reconnect,
> saubere Fehlerberichte an Health und UI.

## 6. Reconnect-Strategie auf BACnet-Ebene

```
ConnectAsync()
  └─ Wiederholungsschleife mit Backoff (1s, 2s, 4s, … max 30s)
       ├─ Port öffnen
       ├─ WhoIs senden
       ├─ auf IAm der MasterId warten (Timeout = MqttNoResponseTimeout / Options.ResponseTimeout)
       ├─ Erfolg → Gerät registrieren, Health = Online, Schleife verlassen
       └─ Fehler → Health = Degraded, Protokollieren, weiter mit Backoff
```

**Wichtig:** Ein BACnet-Fehler darf den MQTT-Teil **nicht** killen (behebt Befund A7).
Der MQTT-Teil läuft unabhängig weiter und publiziert `Status/Health = degraded`.

## 7. Timeout-Konfiguration

Die Defaults des `BacnetClient`-Konstruktors (`timeout=1000`, `retries=3`) wurden im Ist-Zustand
nie überschrieben. Im Soll werden sie explizit gesetzt und konfigurierbar gemacht:

````csharp
var client = new BacnetClient(transport)
{
    Timeout = options.ResponseTimeoutMs,     // Default 1000
    Retries = options.Retries,               // Default 3
};
````

Zusätzlich wird der **Gesamt-Timeout pro Item-Read** begrenzt, damit ein hängender Bus den
Poll-Loop nicht blockiert:

````csharp
using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(options.ItemTimeoutMs));
await transport.ReadPropertyAsync(request, timeoutCts.Token);
````

## 8. Teststrategie (der entscheidende Punkt)

Ohne Tests ist die Portierung nicht verifizierbar. Es wird ein **Golden-File-Test** aufgebaut:

### 8.1 Fixtures

| Fixture | Quelle | Verwendung |
|---|---|---|
| `docs/Mono/MoMA Scan Results.html` | BACnet-Monitor-Scan des ELESTA (im Repo) | **Encoder-Regression**: Request-Bytes ↔ bekannte Antwort |
| `docs/topics.json` | Item-Metadaten | **Topic-Regression** (siehe [Contract-Tests](../04-migration/teststrategie.md)) |
| `tests/…/Fixtures/bacnet-frames.json` | **neu**, aus dem MoMA-Scan extrahiert | Request/Response-Byte-Arrays |

### 8.2 Testfälle

| Test | Prüft |
|---|---|
| `EncodeReadPropertyRequest_MatchesGoldenBytes` | Request-Encoding unverändert |
| `DecodeReadPropertyResponse_MatchesGoldenBytes` | Response-Decoding unverändert |
| `DecodeIAm_ReturnsDeviceId` | `IAm`-Parsing |
| `MstpStateMachine_HandlesTokenLoss` | Zustandsmaschine bei Timeout |
| `MstpTransport_RecoversAfterPortReopen` | Reconnect-Verhalten |
| `SerialPort_Read_WhenPortMissing_ThrowsCleanly` | **Regression gegen Befund A8** (kein Spin-Loop) |

### 8.3 Ablauf

1. Aus `MoMA Scan Results.html` die relevanten Frames extrahieren (einmalig, manuell).
2. Als JSON-Fixture ablegen (`tests/ComfoBox.Bacnet.Tests/Fixtures/bacnet-frames.json`).
3. Golden-Tests **vor** der Portierung grün machen (gegen .NET-Framework-4.5.2-Code!).
4. Portieren → Tests müssen **ohne** Änderung grün bleiben.

> **Reihenfolge zwingend:** Erst die Tests gegen den alten Code schreiben, dann portieren.
> Sonst gibt es keinen Beweis, dass die Portierung verhaltensgleich ist.

## 9. Aufwand

| Aufgabe | Aufwand |
|---|---|
| Fixtures erstellen (MoMA-Scan → JSON) | 0,5 Tag |
| Golden-Tests gegen Alt-Code | 1 Tag |
| Portierung `BACnetBase` (Codec) | 1 Tag |
| Portierung `BACnetClient` | 1,5 Tage |
| Portierung `BACnetTransport` (MSTP + Serial) | 2 Tage |
| Verwerfen der 3 ungenutzten Transporte | 0,5 Tag |
| Timeout-/Reconnect-Härtung | 1 Tag |
| `Trace` → `ILogger` (26 Stellen) | 0,5 Tag |
| **Summe** | **≈ 8 Tage** |

## 10. Definition of Done

- [ ] `ComfoBox.Bacnet` baut ohne Windows-spezifische Abhängigkeiten
- [ ] Golden-Tests grün **ohne** Anpassung der Fixtures
- [ ] Kein `System.Diagnostics.Trace` mehr im Projekt
- [ ] Kein `lock (this)`, kein `m_invoke_id++` ohne `Interlocked`
- [ ] Serial-Port-Fehler führt zu `Health = degraded`, **nicht** zu 100 % CPU
- [ ] Reconnect mit Backoff verifiziert (Test mit USB-Adapter ziehen)
- [ ] MIT-Lizenz + Original-Urheber im neuen Projekt dokumentiert
