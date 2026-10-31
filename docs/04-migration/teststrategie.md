# Teststrategie

> Der Ist-Zustand hat **keine Tests**. Das ist die größte Risikoquelle der Modernisierung
> (Befund F5). Ohne Teststrategie ist die Portierung nicht abnahmefähig.

## 1. Testpyramide

````
            ╱╲
           ╱  ╲         E2E (wenige, teuer)
          ╱────╲        - Docker-Start + Health
         ╱      ╲       - MQTT-Roundtrip gegen Testbroker
        ╱────────╲      - HA-Discovery-Payload prüfen
       ╱          ╲
      ╱────────────╲    Integration (mittel)
     ╱              ╲   - BACnet gegen Simulator
    ╱────────────────╲  - Gateway mit echtem Broker (Testcontainers)
   ╱                  ╲
  ╱────────────────────╲ Unit (viele, schnell)
 ╱                      ╲
────────────────────────── 127 Item-Definitionen, Codec, Payload, Validierung
````

| Ebene | Zielwert | Teste |
|---|---|---|
| Unit | **> 80 %** Coverage in `Domain` + `Gateway` | ~250 Tests |
| Integration | ~40 Tests | BACnet-Simulator, Testbroker |
| E2E | ~10 Tests | Docker-Start, Smoke |

## 2. Testprojekte

| Projekt | Inhalt | Tooling |
|---|---|---|
| `ComfoBox.Domain.Tests` | Item-Katalog, Werttypen, Validierung | xUnit |
| `ComfoBox.Bacnet.Tests` | Codec, MSTP-Zustandsmaschine, Serial | xUnit + Golden-Files |
| `ComfoBox.Gateway.Tests` | MQTT-Publisher/Subscriber, Health, WriteCounter, API | xUnit + `Microsoft.AspNetCore.Mvc.Testing` |
| `ComfoBox.Contract.Tests` | **Topic-Kompatibilität** gegen `docs/topics.json` | xUnit |
| `ComfoBox.E2E.Tests` | Docker-Start, Health, Roundtrip | xUnit + Testcontainers |

## 3. BACnet: Golden-File-Tests

### 3.1 Fixture-Erstellung (einmalig, Phase 1)

Quelle: `docs/Mono/MoMA Scan Results.html` (BACnet-Monitor-Scan des ELESTA).

Manuelle Extraktion der relevanten Frames:

````json
{
  "readPropertyRequest": {
    "deviceId": 1,
    "objectType": 0,
    "objectInstance": 830,
    "propertyId": 85,
    "expectedBytes": "..."
  },
  "readPropertyResponse": {
    "expectedBytes": "...",
    "expectedValue": 21.5
  },
  "iam": {
    "expectedBytes": "...",
    "expectedDeviceId": 1
  }
}
````

### 3.2 Tests

````csharp
public sealed class BacnetCodecTests
{
    [Theory]
    [MemberData(nameof(GoldenFrames))]
    public void Decode_ProducesExpectedValues(GoldenFrame frame)
    {
        var value = BacnetCodec.Decode(frame.ResponseBytes);
        Assert.Equal(frame.ExpectedValue, value);
    }

    [Fact]
    public void EncodeReadPropertyRequest_MatchesGoldenBytes()
    {
        var bytes = BacnetCodec.EncodeReadPropertyRequest(1, 0, 830, 85, invokeId: 7);
        Assert.Equal(Hex("…"), Convert.ToHexString(bytes));
    }
}
````

> **Regel:** Die Golden-Files werden **nicht** angefasst, wenn die Portierung abgeschlossen ist.
> Ein notwendiger Fixture-Edit ist ein Zeichen für eine Verhaltensänderung → muss im PR
> begründet und im ADR dokumentiert werden.

### 3.3 BACnet-Simulator (Integration)

Ein minimaler MSTP-Simulator als Test-Double:

````csharp
internal sealed class FakeBacnetDevice : IAsyncDisposable
{
    public string PortName { get; }
    private readonly Dictionary<int, double> _values = new();

    public void SetValue(int objectInstance, double value) => _values[objectInstance] = value;

    /// <summary>Antwortet auf WhoIs mit IAm und auf ReadProperty mit PresentValue.</summary>
    public Task RunAsync(CancellationToken ct);
}
````

Der Simulator läuft über ein **Pseudo-TTY** (`/dev/pts/N`) oder über eine
`SerialPort`-Substitution → damit sind die Integrationstests ohne Hardware möglich.

> Alternativ: der Simulator spricht direkt gegen `BacnetMstpProtocolTransport` (ohne echten
> Port) – das ist einfacher und für die Zustandsmaschine ausreichend.

## 4. MQTT: Integration mit Testbroker

| Werkzeug | Einsatz |
|---|---|
| `Testcontainers.Mosquitto` | echter Broker im Docker (Integration/E2E) |
| `MQTTnet.Server` (eingebettet) | schneller Unit-Test-Broker ohne Docker |

````csharp
public sealed class MqttPublisherTests : IAsyncLifetime
{
    private MqttServer _broker = null!;
    private readonly List<MqttApplicationMessage> _received = [];

    public async Task InitializeAsync()
    {
        _broker = new MqttFactory().CreateMqttServer();
        await _broker.StartAsync(new MqttServerOptionsBuilder().WithDefaultEndpoint().Build());
        _broker.InterceptingPublishReceived += e => { _received.Add(e.ApplicationMessage); return Task.CompletedTask; };
    }

    [Fact]
    public async Task PublishesItemValue_WithRetain()
    {
        var publisher = CreatePublisher();
        await publisher.PublishAsync("ComfoBox/Zone/SetPointNormal", "21.5", retain: true);

        var msg = Assert.Single(_received);
        Assert.Equal("ComfoBox/Zone/SetPointNormal", msg.Topic);
        Assert.True(msg.Retain);
        Assert.Equal("21.5", Encoding.UTF8.GetString(msg.PayloadSegment));
    }
}
````

## 5. Contract-Tests (Kern der Kompatibilitätssicherung)

Siehe [mqtt-kompatibilitaet.md §4](mqtt-kompatibilitaet.md#4-contract-test).

**Zusätzlich:**

| Test | Prüft |
|---|---|
| `EveryLegacyItem_IsPresentInTheCatalog` | alle 127 Subscribe-Topics existieren |
| `EveryLegacyItem_HasTheSameSetTopic` | alle Publish-Topics existieren |
| `EveryLegacyItem_HasSameMetadata` | Min/Max/Unit/IsReadOnly unverändert |
| `NoVirtualTopics_Exist` | keine erfundenen Topics |
| `EnumNames_AreUnchanged` | Enum-Namen wie im Ist-Zustand |
| `Payload_Parses_RegardlessOfCurrentCulture` | A5-Fix |
| `InvalidEnumPayload_DoesNotWriteZero` | A6-Fix |

> Die Fixture `docs/topics.json` wird **mitkopiert** in das Testprojekt (oder per
> `CopyToOutputDirectory` referenziert), damit der Test nicht vom Arbeitsverzeichnis abhängt.

## 6. WriteCounter-Tests

| Test | Prüft |
|---|---|
| `Count_Increments_OnSuccessfulWrite` | Zähler steigt |
| `Count_DoesNotIncrement_OnFailedWrite` | A22 |
| `Count_Persists_AcrossRestart` | **A3** – Datei wird neu gelesen |
| `Count_Resets_AtCalendarDayChange` | kalendarisches Fenster |
| `Limit_IsEnforced` | Schreibvorgang wird abgelehnt |

## 7. Health-Tests

| Test | Prüft |
|---|---|
| `Health_IsDegraded_WhenBacnetFails` | Health-Zustand |
| `Health_BecomesOffline_AfterMqttTimeout` | LWT-Payload |
| `Errors_AreDeduplicated` | 60-s-Fenster |
| `ErrorRing_KeepsLast50` | Ringpuffer |
| `UserErrors_DoNotDegradeHealth` | `value_out_of_range` → healthy |

## 8. API-Tests

Mit `WebApplicationFactory`:

````csharp
public sealed class ItemsApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task PutItem_WithValueOutOfRange_Returns422()
    {
        var client = _factory.CreateClient();
        var response = await client.PutAsJsonAsync("/api/items/zone.setpoint_normal", new { value = 99f });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task PutItem_WhenReadOnly_Returns409() { … }

    [Fact]
    public async Task PostRestart_Returns202() { … }
}
````

## 9. E2E-Tests

| Test | Szenario |
|---|---|
| `Container_Starts_AndReportsHealthy` | `docker compose up` → `/api/health` = healthy |
| `Gateway_PublishesAllTopics` | Topics im Broker prüfen |
| `Gateway_Reconnects_AfterBrokerRestart` | Broker killen → reconnect → `Connected=1` (**A10**) |
| `Gateway_Handles_MissingSerialPort` | Serial-Port entfernen → Health `degraded`, **kein** CPU-Peak (**A8**) |
| `RestartEndpoint_TriggersRestart` | `POST /api/service/restart` → Container startet neu |
| `Discovery_Payloads_AreValidJson` | Schema-Prüfung |

## 10. Coverage-Ziele

| Projekt | Ziel |
|---|---|
| `ComfoBox.Domain` | ≥ 90 % |
| `ComfoBox.Gateway` | ≥ 80 % |
| `ComfoBox.Bacnet` | ≥ 60 % (Codec- und Zustandsmaschinen-Zweige) |

Abbruch in CI, wenn die Coverage **unter** dem Ziel liegt (CodeCov-Integration).

## 11. Test-Daten

| Datei | Verwendung | Herkunft |
|---|---|---|
| `docs/topics.json` | Contract-Tests | **im Repo** – nicht verlieren! |
| `docs/Mono/MoMA Scan Results.html` | BACnet-Golden-Frames | **im Repo** |
| `tests/…/Fixtures/bacnet-frames.json` | BACnet-Golden-Tests | neu (Phase 1) |
| `tests/…/Fixtures/discovery-schema.json` | Discovery-Payload-Schema | neu |

## 12. Testphilosophie

| Prinzip | Umsetzung |
|---|---|
| **Kein Test ohne Hardwarebedarf** | BACnet-Simulator, Testbroker |
| **Tests sind die Spezifikation** | Der Contract-Test *ist* die MQTT-Zusicherung |
| **Schnell** | Unit-Tests < 1 s, Suite < 30 s |
| **Deterministisch** | keine `DateTime.Now`-Abhängigkeiten → `TimeProvider` injizieren (.NET 8+) |
| **Keine Flakiness** | kein `Task.Delay` in Tests, kein Netzwerk außer im Testcontainer |
