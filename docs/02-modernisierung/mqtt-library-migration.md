# MQTT-Library-Migration: M2Mqtt → MQTTnet

## 1. Warum der Wechsel

M2Mqtt 4.3.0.0 (2017, `uPLibrary.Networking.M2Mqtt`) hat im Ist-Zustand folgende Probleme:

| Problem | Beleg |
|---|---|
| **Kein Async-Client** | Alles eventbasiert (`MqttMsgPublishReceived`), blockierend in Callbacks |
| **Kein .NET-Standard/.NET-Core-Support** | Paket ist auf .NET Framework 4.x beschränkt |
| **Keine TLS-Konfiguration im genutzten Pfad** | `InitMqttClient` (mit `MqttSslProtocols`) ist toter Code |
| **Keine Authentifizierung** | `Connect(..., null, null, ...)` |
| **Reconnect-Logik handgeschrieben und fehlerhaft** | Rekursion ohne Backoff/Cancellation (Befund A9) |
| **Kein `WillDelay`-Semantik-Klarheit**, `keepAlivePeriod = 2` | unnötig aggressive Disconnects (Befund A24) |
| **Paket nicht mehr gepflegt** | — |

**MQTTnet 5** (dotnet/MQTTnet, MIT) ist das De-facto-Standard für .NET:
async/await, `net8.0`+, MQTT 3.1/3.1.1/**5.0**, TLS, Client-Certificate, WebSocket-Transport,
eingebetteter Server (Broker), strukturierte Events, aktive Entwicklung.

## 2. API-Mapping

| Ist (M2Mqtt) | Soll (MQTTnet 5) |
|---|---|
| `new MqttClient(host)` | `new MqttClientFactory().CreateMqttClient()` |
| `client.Connect(clientId, user, pass, cleanSession, keepAlive, willFlag, willTopic, willQos, willRetain, willDelay)` | `client.ConnectAsync(options, ct)` mit `MqttClientOptionsBuilder` |
| `client.Disconnect()` | `await client.DisconnectAsync(new MqttClientDisconnectOptions(), ct)` |
| `client.IsConnected` | `client.IsConnected` (vorhanden) |
| `client.MqttMsgPublishReceived += handler` | `client.ApplicationMessageReceivedAsync += handler` |
| `client.ConnectionClosed += handler` | `client.DisconnectedAsync += handler` |
| `client.Subscribe(new[]{t}, new[]{qos})` | `await client.SubscribeAsync(opts, ct)` |
| `client.Publish(topic, bytes, qos, retain)` | `await client.PublishAsync(msg, ct)` |
| `MqttMsgBase.QOS_LEVEL_EXACTLY_ONCE` | `MqttQualityOfServiceLevel.ExactlyOnce` |
| `MqttMsgPublishEventArgs.Topic/.Message` | `ApplicationMessageReceivedEventArgs.ApplicationMessage.Topic/.PayloadSegment` |
| `MqttApplicationMessage` | `MQTTnet.Protocol.MqttApplicationMessage` |
| `MqttClientFactory` | `MQTTnet.Client` |
| — | `MQTTnet.Server` (optionaler eingebetteter Broker) |

### Verbindungsoptionen

````csharp
// src/ComfoBox.Gateway/Mqtt/MqttConnectionFactory.cs
internal static class MqttConnectionFactory
{
    public static MqttClientOptions BuildOptions(MqttBrokerOptions broker, string clientId, string baseTopic)
    {
        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(broker.Host, broker.Port)
            .WithCleanSession()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(30))       // statt 2 s
            .WithProtocolVersion(MqttProtocolVersion.V500)       // HA empfiehlt v5
            .WithLastWillTopic($"{baseTopic}/Status/Health")     // LWT
            .WithLastWillPayload("offline")
            .WithLastWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithLastWillRetain();

        if (!string.IsNullOrEmpty(broker.Username))
            builder = builder.WithCredentials(broker.Username, broker.Password);

        if (broker.UseTls)
        {
            builder = builder.WithTlsOptions(o =>
            {
                o.UseTls();
                if (broker.AllowUntrustedCertificates)
                    o.WithCertificateValidationHandler((_, _, _, _) => true);   // nur LAN/self-signed
            });
        }

        return builder.Build();
    }
}
````

> **Client-ID:** `comfobox-gateway-{hostname}` – stabil und lesbar (statt `ComfoBox{DateTime.Ticks}`).
> MQTTnet 5 unterstützt außerdem `WithWillDelayInterval`.

## 3. Publisher

````csharp
// src/ComfoBox.Gateway/Mqtt/MqttPublisher.cs
internal sealed class MqttPublisher(
    IMqttClient client,
    IOptions<MqttOptions> options,
    IOptions<GatewayOptions> gateway,
    ILogger<MqttPublisher> logger) : IMqttPublisher
{
    public async Task PublishAsync(string topic, string payload, bool retain = false,
                                   MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
                                   CancellationToken ct = default)
    {
        if (!client.IsConnected)
        {
            logger.LogWarning("Publish verworfen (nicht verbunden): {Topic}", topic);
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)          // string-Payload → UTF-8
            .WithQualityOfServiceLevel(qos)
            .WithRetainFlag(retain)
            .Build();

        await client.PublishAsync(message, ct);
    }
}
````

> **QoS-Änderung:** Im Ist-Zustand war Publish-QoS 0 / Subscribe-QoS 2. MQTTnet/Switching:
> Publish **QoS 0** (Status, hohe Frequenz), **QoS 1** für Set-Bestätigungen und Discovery.
> Subscribe auf `/Set` → **QoS 1** (AtLeastOnce). QoS 2 verursacht 4-Part-Handshake und
> Latenz ohne Nutzen bei lokalem Broker.
> **Die QoS-Änderung ist kompatibel**, da QoS vom Client abhängig: ein Subscriber mit QoS 1
> bekommt QoS-0-Publishes als QoS 0, QoS-1-Publishes als QoS 1.

## 4. Reconnect mit Backoff

Der zentrale Fehler des Ist-Zustands (Rekursion ohne Abbruch) wird durch eine **ereignisbasierte
Reconnect-Schleife** ersetzt:

````csharp
// src/ComfoBox.Gateway/Mqtt/MqttConnectionManager.cs
internal sealed class MqttConnectionManager(
    MqttClientFactory factory,
    IMqttPublisher publisher,
    IHealthReporter health,
    ILogger<MqttConnectionManager> logger) : IHostedService, IMqttPublisher
{
    private MqttClient? _client;
    private readonly CancellationTokenSource _cts = new();

    public Task StartAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task StopAsync(CancellationToken ct)
    {
        await _cts.CancelAsync();
        if (_client is { IsConnected: true })
        {
            // Sauber: "offline" publizieren, LWT wird NICHT ausgelöst
            await publisher.PublishAsync($"{_baseTopic}/Status/Health", "offline", retain: true);
            await _client.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
        }
        _client?.Dispose();
    }

    private async Task RunAsync(CancellationToken ct)
    {
        var attempt = 0;
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var client = factory.CreateMqttClient();
                client.DisconnectedAsync += async e => await OnDisconnectedAsync(e, ct);

                await client.ConnectAsync(BuildOptions(), ct);
                attempt = 0;
                health.Report(HealthState.Online, "MQTT verbunden");
                await PublishDiscoveryAsync(ct);
                return;
            }
            catch (Exception ex)
            {
                attempt++;
                var delay = TimeSpan.FromSeconds(Math.Min(60, Math.Pow(2, Math.Min(attempt, 6))));
                logger.LogError(ex, "MQTT-Verbindung fehlgeschlagen (Versuch {Attempt}), Retry in {Delay}",
                                attempt, delay);
                health.Report(HealthState.Degraded, $"MQTT nicht verbunden: {ex.Message}");
                await Task.Delay(delay, ct);
            }
        }
    }

    private async Task OnDisconnectedAsync(MqttClientDisconnectedEventArgs e, CancellationToken ct)
    {
        logger.LogWarning(e.Exception, "MQTT getrennt: {Reason}", e.Reason);
        health.Report(HealthState.Degraded, "MQTT getrennt");
        await RunAsync(ct);           // endlicher Backoff, kein Stack-Overflow
    }
}
````

**Behebt:** A9 (Rekursion), A10 (`Connected=1` wird bei jedem Connect publiziert),
A25 (TLS/Auth), A24 (Keepalive 30 s), A11 (Stop trennt sauber).

## 5. LWT (Last Will) & Availability

| Topic | Payload | Retain | Zweck |
|---|---|---|---|
| `{BaseTopic}/Status/Health` | `online` / `offline` | ja | HA-`availability_topic` |
| `{BaseTopic}/Connected` | `1` / `0` | ja | **Alt-Topic, Kompatibilität** |

Der LWT wird **einmal** bei `ConnectAsync` gesetzt und gilt solange die Verbindung steht.
MQTTnet sendet den Will automatisch, wenn die Verbindung unerwartet abbricht (Prozess-Kill,
Netzwerkfehler). Beim ** orderly Shutdown** wird er **nicht** ausgelöst – deshalb publishen wir
`offline` explizit vor dem Disconnect.

> **Kompatibilität:** `ComfoBox/Connected` bleibt bestehen, wird aber jetzt bei **jedem**
> Connect mit `1` publiziert (behebt A10).

## 6. Payload-Fixes (kultur-invariant)

Diese Fixes sind **Pflicht**, weil die Schreibpfade im Ist-Zustand auf deutschen Systemen defekt sind.

````csharp
// alt – MqttItem.cs:110 / :139
float.Parse(message);                       // CurrentCulture!
value.ToString();                           // CurrentCulture!

// neu
public static class MqttPayload
{
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static string Format(float value) => value.ToString("0.###", Inv);
    public static bool TryParse(string payload, out float value)
        => float.TryParse(payload, NumberStyles.Float, Inv, out value);
}
````

| Fall | Ist | Soll |
|---|---|---|
| `23.5` schreiben (de-DE) | `FormatException` → verworfen | ✅ `23.5` |
| `23,5` schreiben (de-DE) | → `235` (falsch!) | wird als `23,5` **nicht** akzeptiert (klarer Fehler) |
| Publish `23.5` | `23,5` | `23.5` |

> **Kompatibilitäts-Auswirkung:** Bestehende HA-Automationen, die `23,5` publishen, würden
> nach dem Fix abgelehnt. Das ist **richtig**, weil `23,5` formal kein gültiger MQTT-Zahlwert
> ist. Die Fehlermeldung landet in `Status/Errors` und im UI.
> → Siehe [mqtt-kompatibilitaet §3](../04-migration/mqtt-kompatibilitaet.md#3-payload-interpretation).

### Enum-Schreibpfad

````csharp
// alt – EnumMqttItem.cs:91-98  (schreibt bei ungültiger Eingabe den Wert 0!)
float newValue;
if (float.TryParse(message, out newValue))
    newValue = Convert.ToInt32(Enum.Parse(enumValue.GetEnumType(), message));

// neu
public bool TryParseEnumPayload(string payload, Type enumType, out int value)
{
    value = 0;
    var text = payload.Trim();

    // 1) als Zahl
    if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
        return Enum.IsDefined(enumType, value);

    // 2) als Name (case-insensitive)
    if (Enum.TryParse(enumType, text, ignoreCase: true, out var parsed) && parsed is not null)
    {
        value = Convert.ToInt32(parsed);
        return true;
    }

    return false;   // → Wert wird NICHT geschrieben, sondern abgelehnt + gemeldet
}
````

> **Kompatibilitäts-Auswirkung:** Die bisherigen Namen bleiben gültig. Zusätzlich werden jetzt
> auch **Zahlen** auf `/AsNumber/Set` akzeptiert – das ist eine **Erweiterung**, keine Brechung.
> Der gefährliche „schreibe 0"-Fall entfällt (Befund A6).

## 7. Home-Assistant-Discovery

Siehe [home-assistant.md](home-assistant.md). Kurzfassung:

````csharp
var payload = new
{
    name = item.DisplayName,
    unique_id = $"comfobox_{item.Id}",
    state_topic = item.StateTopic,
    command_topic = item.IsWritable ? item.SetTopic : null,
    availability_topic = $"{baseTopic}/Status/Health",
    payload_available = "online",
    payload_not_available = "offline",
    device = new { identifiers = new[] { "comfobox" }, name = "ComfoBox", manufacturer = "Zehnder", model = "ComfoBox Series 5" },
    origin = new { name = "comfobox-gateway", sw_version = version }
};
await publisher.PublishAsync(
    $"{discoveryPrefix}/{component}/{item.Id}/config",
    JsonSerializer.Serialize(payload), retain: true);
````

## 8. Optional: eingebetteter Broker

Für den Standalone-Betrieb (ohne externen Broker) kann MQTTnet-Server im selben Container laufen:

````csharp
if (mqttOptions.EmbedBroker)
{
    var server = new MqttFactory().CreateMqttServer();
    await server.StartAsync(new MqttServerOptionsBuilder()
        .WithDefaultEndpoint()
        .WithDefaultEndpointPort(mqttOptions.EmbeddedBrokerPort)
        .Build());
}
````

> **Standard: aus.** Der Normalfall ist ein externer Broker (Mosquitto als HA-Add-on oder auf
> der DS923+). Siehe [ADR-004](../05-entscheidungen/adr-004-containerisierung.md).

## 9. Checkliste M2Mqtt → MQTTnet

| # | Aufgabe | Status |
|---|---|---|
| 1 | Paket `MQTTnet` in `Directory.Packages.props` | ✅ 5.2.0.1603 |
| 2 | `MqttConnectionFactory` mit TLS/Credentials/Protokollversion | ✅ |
| 3 | `MqttConnectionManager` mit Backoff + Cancellation | ✅ |
| 4 | `MqttPublisher` mit QoS-Wahl + Retain | ✅ |
| 5 | `MqttSubscriber` mit Handler pro Topic | ✅ |
| 6 | LWT auf `Status/Health` + `Connected` | ✅ (zwei Verbindungen je Broker – MQTT 5 erlaubt nur **einen** Will pro Verbindung) |
| 7 | `MqttPayload` (InvariantCulture) einführen | ✅ |
| 8 | Enum-Parser ohne „0-Fallback" | ✅ |
| 9 | Discovery-Publisher | ✅ |
| 10 | Contract-Test gegen `docs/topics.json` | ✅ |
| 11 | `ComfoBoxMqtt`/`M2Mqtt` vollständig entfernt | ✅ (aus `src/`; `src_old/` bleibt Archiv) |

> **Stand 2026-10-31:** Phase 3 abgeschlossen – siehe
> [protokoll.md](../06-aufgaben/protokoll.md). 217 Tests grün.
