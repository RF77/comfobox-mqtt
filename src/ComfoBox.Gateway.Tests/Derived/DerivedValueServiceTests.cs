using ComfoBox.Domain.Derived;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Derived;
using ComfoBox.Gateway.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Tests.Derived;

/// <summary>
/// Tests des <see cref="DerivedValueService"/>: echter Cache, echter Katalog,
/// protokollierender Publisher.
/// </summary>
/// <remarks>
/// Die Tests rufen die Naht <see cref="DerivedValueService.PublishAllAsync"/>
/// direkt auf, statt auf den Hintergrunddienst zu warten - dadurch sind sie
/// deterministisch und brauchen keine Verzögerung als Synchronisation.
/// </remarks>
public sealed class DerivedValueServiceTests
{
    private const string Superheat = "derived.superheat";
    private const string HeatingCircuitDelta = "derived.heating_circuit_delta";
    private const string BufferDelta = "derived.buffer_delta";
    private const string RuntimeRatio = "derived.runtime_ratio";
    private const string ServiceDue = "derived.service_due";
    private const string CoolingActive = "derived.cooling_active";

    private static readonly ItemCatalog Catalog = new();

    private static readonly MqttTopicMap TopicMap = new(Catalog, "ComfoBox", expertModeEnabled: false);

    /// <summary>
    /// Ein realistischer Satz Eingänge: Überhitzung 7 K, Spreizung 7 K,
    /// Schichtung 8 K, Laufzeit 75 %, Wartung fällig, keine Kühlung,
    /// 1000 kWh Wärme und 100 kWh Strom.
    /// </summary>
    private static readonly (int ObjectId, float Value)[] SampleValues =
    [
        (641, 12f),     // Sauggas
        (660, 5f),      // Verdampfer
        (600, 45f),     // Vorlauf
        (3468, 38f),    // Rücklauf
        (24, 50f),      // Puffer oben
        (25, 42f),      // Puffer unten
        (30, 1000f),    // Einschaltstunden
        (31, 500f),     // Stufe 2
        (3060, 2000f),  // Heizstunden
        (3366, 5000f),  // Service-Stunden
        (3365, 4000f),  // Wartungsintervall
        (3349, 1000f),  // Wärmezähler
        (3355, 100f),   // Stromzähler
        (662, 10f),     // HeatPumpStatus (HeatingWithHeatPump)
        (3098, 0f),     // Kühlvorlauf
    ];

    /// <summary>
    /// Alle Eingänge liegen frisch im Cache; der COP bekommt zwei Proben.
    /// </summary>
    [Fact]
    public async Task Alle_Eingaenge_Frisch_Publizieren_Sieben_Zahlen()
    {
        var harness = Harness.Create();
        FillAllInputs(harness.Cache);

        // Erste Probe: der Momentan-COP ist noch unbekannt (kein Fenster).
        await harness.Service.PublishAllAsync(CancellationToken.None);
        Assert.Equal(DerivedValueService.UnknownPayload, harness.Publisher.LastPayloadFor(TopicOf(DerivedValues.CopInstantId)));

        // Eine Stunde später schließt sich das Fenster: ΔWärme 300, ΔStrom 30.
        // (Der Poller liest in dieser Zeit alle Eingänge neu - der Test
        // schreibt sie deshalb mit frischem Zeitstempel.)
        harness.Time.Advance(TimeSpan.FromHours(1));
        FillAllInputs(harness.Cache);
        Update(harness.Cache, 3349, 1300f);
        Update(harness.Cache, 3355, 130f);

        await harness.Service.PublishAllAsync(CancellationToken.None);

        Assert.Equal("7", harness.Publisher.LastPayloadFor(TopicOf(Superheat)));
        Assert.Equal("7", harness.Publisher.LastPayloadFor(TopicOf(HeatingCircuitDelta)));
        Assert.Equal("8", harness.Publisher.LastPayloadFor(TopicOf(BufferDelta)));
        Assert.Equal("75", harness.Publisher.LastPayloadFor(TopicOf(RuntimeRatio)));
        Assert.Equal("1", harness.Publisher.LastPayloadFor(TopicOf(ServiceDue)));
        Assert.Equal("10", harness.Publisher.LastPayloadFor(TopicOf(DerivedValues.CopInstantId)));
        Assert.Equal("0", harness.Publisher.LastPayloadFor(TopicOf(CoolingActive)));

        // Die Topics liegen genau dort, wo der Vertrag sie verlangt.
        Assert.Equal("ComfoBox/Status/Derived/Superheat", TopicOf(Superheat));
    }

    /// <summary>
    /// Ein Eingang, der nie gelesen wurde, ergibt <c>unknown</c> - **nicht** 0.
    /// </summary>
    [Fact]
    public async Task Fehlender_Eingang_Publiziert_Unknown_Statt_0()
    {
        var harness = Harness.Create();
        FillAllInputs(harness.Cache, 25);

        await harness.Service.PublishAllAsync(CancellationToken.None);

        var payload = harness.Publisher.LastPayloadFor(TopicOf(BufferDelta));

        Assert.Equal(DerivedValueService.UnknownPayload, payload);
        Assert.NotEqual("0", payload);
    }

    /// <summary>
    /// Ein als veraltet markierter Eingang ergibt <c>unknown</c>.
    /// </summary>
    [Fact]
    public async Task Veralteter_Eingang_Publiziert_Unknown()
    {
        var harness = Harness.Create();
        FillAllInputs(harness.Cache);

        await harness.Service.PublishAllAsync(CancellationToken.None);
        Assert.Equal("7", harness.Publisher.LastPayloadFor(TopicOf(Superheat)));

        harness.Cache.MarkStale(ItemIdOf(641));
        await harness.Service.PublishAllAsync(CancellationToken.None);

        Assert.Equal(DerivedValueService.UnknownPayload, harness.Publisher.LastPayloadFor(TopicOf(Superheat)));
    }

    /// <summary>
    /// Stündlich gelesene Eingänge bleiben in **ihrem** Fenster verwendbar,
    /// auch wenn das Ablauf-Fenster des Caches sie längst ablaufen lässt.
    /// </summary>
    /// <remarks>
    /// Ohne diese Regel wäre der Momentan-COP nie berechenbar: seine Eingänge
    /// werden stündlich gelesen, das Cache-Fenster liegt bei drei Zyklen der
    /// sekundären Priorität (15 Minuten).
    /// </remarks>
    [Fact]
    public async Task Langsame_Eingaenge_Ueberleben_Das_Cache_Fenster()
    {
        var harness = Harness.Create(TimeSpan.FromMinutes(3));
        FillAllInputs(harness.Cache);

        await harness.Service.PublishAllAsync(CancellationToken.None);

        harness.Time.Advance(TimeSpan.FromHours(1));
        Update(harness.Cache, 3349, 1300f);
        Update(harness.Cache, 3355, 130f);

        await harness.Service.PublishAllAsync(CancellationToken.None);

        Assert.Equal("10", harness.Publisher.LastPayloadFor(TopicOf(DerivedValues.CopInstantId)));
    }

    /// <summary>
    /// Nach dem eigenen Fenster (3 × 1 h) ist der Wert wieder unbekannt.
    /// </summary>
    [Fact]
    public async Task Zu_Alte_Langsame_Eingaenge_Publizieren_Unknown()
    {
        var harness = Harness.Create(TimeSpan.FromMinutes(3));
        FillAllInputs(harness.Cache);

        await harness.Service.PublishAllAsync(CancellationToken.None);

        harness.Time.Advance(TimeSpan.FromHours(1));
        FillAllInputs(harness.Cache);
        Update(harness.Cache, 3349, 1300f);
        Update(harness.Cache, 3355, 130f);

        await harness.Service.PublishAllAsync(CancellationToken.None);
        Assert.Equal("10", harness.Publisher.LastPayloadFor(TopicOf(DerivedValues.CopInstantId)));

        // Vier Stunden ohne neuen Read: das Slow-Fenster (3 h) ist zu.
        harness.Time.Advance(TimeSpan.FromHours(4));

        await harness.Service.PublishAllAsync(CancellationToken.None);

        Assert.Equal(DerivedValueService.UnknownPayload, harness.Publisher.LastPayloadFor(TopicOf(DerivedValues.CopInstantId)));
    }

    /// <summary>
    /// Unveränderte Werte werden **nicht** erneut publiziert.
    /// </summary>
    [Fact]
    public async Task Unveraenderte_Werte_Publizieren_Nichts_Erneut()
    {
        var harness = Harness.Create();
        FillAllInputs(harness.Cache);

        await harness.Service.PublishAllAsync(CancellationToken.None);
        await harness.Service.PublishAllAsync(CancellationToken.None);

        foreach (var definition in DerivedValues.All)
        {
            var topic = TopicOf(definition.Id);
            Assert.Single(harness.Publisher.Messages, message => message.Topic == topic);
        }
    }

    /// <summary>
    /// Eine Änderung rechnet nur die betroffenen Definitionen neu.
    /// </summary>
    [Fact]
    public async Task Nur_Betroffene_Definitionen_Werden_Neu_Gerechnet()
    {
        var harness = Harness.Create();
        FillAllInputs(harness.Cache);
        Update(harness.Cache, 24, 55f);

        await harness.Service.PublishForItemAsync(ItemIdOf(24), CancellationToken.None);

        var message = Assert.Single(harness.Publisher.Messages);
        Assert.Equal(TopicOf(BufferDelta), message.Topic);
        Assert.Equal("13", message.Payload);
    }

    /// <summary>
    /// Die Werte werden retained publiziert (Standard: <c>Mqtt:UseMqttRetain</c>).
    /// </summary>
    [Fact]
    public async Task Werte_Werden_Retained_Publiziert()
    {
        var harness = Harness.Create();
        FillAllInputs(harness.Cache);

        await harness.Service.PublishAllAsync(CancellationToken.None);

        Assert.Equal(7, harness.Publisher.Messages.Count);
        Assert.All(harness.Publisher.Messages, message => Assert.True(message.Retain));
    }

    /// <summary>
    /// Ein Publisher-Fehler verlässt den Dienst nicht - und wird beim nächsten
    /// Lauf erneut versucht.
    /// </summary>
    [Fact]
    public async Task Publisher_Fehler_Verlaesst_Den_Dienst_Nicht()
    {
        var harness = Harness.Create();
        FillAllInputs(harness.Cache);
        harness.Publisher.Fail = true;

        await harness.Service.PublishAllAsync(CancellationToken.None);
        Assert.Empty(harness.Publisher.Messages);

        harness.Publisher.Fail = false;
        await harness.Service.PublishAllAsync(CancellationToken.None);

        Assert.Equal(7, harness.Publisher.Messages.Count);
    }

    /// <summary>
    /// Das vollständige Topic eines abgeleiteten Wertes.
    /// </summary>
    private static string TopicOf(string id)
        => $"ComfoBox/Status/{DerivedValues.All.Single(definition => definition.Id == id).TopicPath}";

    /// <summary>
    /// Die Katalog-Id des Items zu einer BACnet-Objekt-Id.
    /// </summary>
    private static string ItemIdOf(int objectId)
        => Catalog.All.Single(item => item.BacnetObjectId == objectId).Id;

    /// <summary>
    /// Schreibt einen Wert in den Cache - Enum-Items als <see cref="EnumValue"/>.
    /// </summary>
    private static void Update(ValueCache cache, int objectId, float value)
    {
        var item = Catalog.All.Single(candidate => candidate.BacnetObjectId == objectId);

        ItemValue itemValue = item.Kind == ItemValueKind.Enum
            ? new EnumValue((int)value, item.EnumValues)
            : new AnalogValue(value, item.Min, item.Max, item.Unit);

        cache.Update(item.Id, itemValue);
    }

    /// <summary>
    /// Trägt für alle Eingänge der abgeleiteten Werte einen Wert ein.
    /// </summary>
    /// <param name="cache">Der Cache.</param>
    /// <param name="skip">Objekt-Ids, die **nicht** geschrieben werden (nie gelesen).</param>
    private static void FillAllInputs(ValueCache cache, params int[] skip)
    {
        foreach (var (objectId, value) in SampleValues)
        {
            if (skip.Contains(objectId))
            {
                continue;
            }

            Update(cache, objectId, value);
        }
    }

    /// <summary>
    /// Die Testumgebung: Cache mit steuerbarer Zeit, protokollierender Publisher.
    /// </summary>
    private sealed class Harness
    {
        private Harness(TestTimeProvider time, ValueCache cache, RecordingPublisher publisher, DerivedValueService service)
        {
            Time = time;
            Cache = cache;
            Publisher = publisher;
            Service = service;
        }

        public TestTimeProvider Time { get; }

        public ValueCache Cache { get; }

        public RecordingPublisher Publisher { get; }

        public DerivedValueService Service { get; }

        public static Harness Create(TimeSpan? cacheStaleAfter = null)
        {
            var time = new TestTimeProvider(new DateTimeOffset(2026, 11, 16, 12, 0, 0, TimeSpan.Zero));

            // Standard: großzügiges Fenster, damit die meisten Tests nicht mit
            // dem Ablauf des Caches zu kämpfen haben. Der Test für die
            // Prioritäts-Fenster setzt bewusst ein kurzes Fenster.
            var cache = new ValueCache(time, cacheStaleAfter ?? TimeSpan.FromHours(6));
            var publisher = new RecordingPublisher();
            var options = new MqttOptions
            {
                Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
            };

            var service = new DerivedValueService(
                cache,
                Catalog,
                publisher,
                TopicMap,
                options,
                new GatewayOptions(),
                time,
                NullLogger<DerivedValueService>.Instance);

            return new Harness(time, cache, publisher, service);
        }
    }

    /// <summary>
    /// Eine publizierte Nachricht mit dem Retain-Flag.
    /// </summary>
    private sealed record PublishedMessage(string Topic, string Payload, bool Retain);

    /// <summary>
    /// Ein Publisher, der die Nachrichten aufzeichnet - ohne Broker.
    /// </summary>
    private sealed class RecordingPublisher : IMqttPublisher
    {
        private readonly List<PublishedMessage> _messages = [];

        public List<PublishedMessage> Messages => _messages;

        /// <summary>Soll der Publish fehlschlagen?</summary>
        public bool Fail { get; set; }

        public Task<bool> PublishAsync(
            string topic,
            string payload,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            CancellationToken cancellationToken = default)
        {
            if (Fail)
            {
                throw new InvalidOperationException("Der Publish wurde abgelehnt.");
            }

            _messages.Add(new PublishedMessage(topic, payload, retain));
            return Task.FromResult(true);
        }

        public Task<bool> PublishJsonAsync(
            string topic,
            object value,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            CancellationToken cancellationToken = default)
            => PublishAsync(topic, System.Text.Json.JsonSerializer.Serialize(value), qos, retain, cancellationToken);

        /// <summary>
        /// Die zuletzt publizierte Payload zu einem Topic (oder <c>null</c>).
        /// </summary>
        public string? LastPayloadFor(string topic)
            => _messages.LastOrDefault(message => message.Topic == topic)?.Payload;
    }
}
