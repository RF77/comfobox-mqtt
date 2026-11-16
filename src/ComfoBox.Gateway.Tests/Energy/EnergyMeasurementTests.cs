using System.Text.Json;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Energy;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Tests.Energy;

/// <summary>
/// Tests für das DP-3340-Gate: ohne Wärmemengenmessung werden kWh und COP
/// **nicht als 0** publiziert.
/// </summary>
/// <remarks>
/// Die Tests fahren die Naht direkt an (<see cref="EnergyMeasurementGate.ApplyAsync"/>)
/// statt auf den Hintergrunddienst zu warten - dadurch sind sie
/// deterministisch und brauchen keine Verzögerung als Synchronisation.
/// </remarks>
public sealed class EnergyMeasurementTests
{
    private const string ModeId = EnergyMeasurementGate.MeasurementModeId;

    private static readonly ItemCatalog Catalog = new();

    private static readonly ItemDefinition[] EnergyItems =
        [.. Catalog.All.Where(static item => item.RequiresEnergyMeasurement)];

    private static readonly MqttTopicMap TopicMap =
        new(Catalog, "ComfoBox", expertModeEnabled: false);

    private static readonly string[] EnergyTopics =
        [.. EnergyItems.Select(item => TopicMap.ItemTopic(item))];

    [Fact]
    public async Task ModeNone_Unterdrueckt_Alle_Energie_Topics()
    {
        var harness = Harness.Create();
        FillEnergyValues(harness.Cache);
        harness.Cache.Update(ModeId, Mode(0));

        await harness.Gate.ApplyAsync(CancellationToken.None);

        Assert.Equal(EnergyAvailability.Unavailable, harness.Gate.Availability);
        Assert.False(harness.Gate.IsAvailable);
        Assert.Equal("0", harness.Publisher.LastPayloadFor(harness.Topics.EnergyAvailableTopic));

        // Das Gate leert die Topics - es darf aber nirgends ein Wert stehen.
        Assert.DoesNotContain(harness.Publisher.Messages, IsValueMessage);

        // Auch der Werte-Publisher überspringt die 11 Items.
        var client = new NoopMqttClient();
        await harness.StatePublisher.PublishAllAsync(client, CancellationToken.None);

        Assert.DoesNotContain(client.Published, message => IsEnergyTopic(message.Topic));
    }

    [Fact]
    public async Task ModeOn_Publiziert_Energie_Topics()
    {
        var harness = Harness.Create();
        FillEnergyValues(harness.Cache);
        harness.Cache.Update(ModeId, Mode(1));

        await harness.Gate.ApplyAsync(CancellationToken.None);

        Assert.True(harness.Gate.IsAvailable);

        var availability = harness.Publisher.Messages
            .Single(message => message.Topic == harness.Topics.EnergyAvailableTopic);

        Assert.Equal("1", availability.Payload);
        Assert.True(availability.Retain);

        // Das Gate publiziert alle 11 gecachten Werte erneut.
        var published = harness.Publisher.Messages
            .Where(IsValueMessage)
            .Select(message => message.Topic)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Equal(11, published.Count);
        Assert.All(EnergyTopics, topic => Assert.Contains(topic, published));
    }

    [Fact]
    public async Task Unbekannter_Mode_Publiziert_Keine_0()
    {
        var harness = Harness.Create();

        // Werte sind da - aber DP 3340 wurde noch nie gelesen.
        FillEnergyValues(harness.Cache);

        await harness.Gate.ApplyAsync(CancellationToken.None);

        Assert.Equal(EnergyAvailability.Unknown, harness.Gate.Availability);

        // Kein Zustand, keine 0: es gibt noch nichts zu sagen.
        Assert.Empty(harness.Publisher.Messages);

        var client = new NoopMqttClient();
        await harness.StatePublisher.PublishAllAsync(client, CancellationToken.None);

        Assert.DoesNotContain(client.Published, message => IsEnergyTopic(message.Topic));
    }

    [Fact]
    public async Task Fehler_Wird_Nur_Bei_Wechsel_Gemeldet()
    {
        var harness = Harness.Create();
        harness.Cache.Update(ModeId, Mode(0));

        await harness.Gate.ApplyAsync(CancellationToken.None);
        await harness.Gate.ApplyAsync(CancellationToken.None);

        var entries = harness.Errors.Snapshot()
            .Where(entry => entry.Code == EnergyMeasurementGate.DisabledCode)
            .ToArray();

        var entry = Assert.Single(entries);
        Assert.Equal("warning", entry.Severity);
        Assert.Equal(EnergyMeasurementGate.Source, entry.Source);
        Assert.Equal(EnergyMeasurementGate.DisabledMessage, entry.Message);
    }

    [Fact]
    public async Task Start_Leert_Availability()
    {
        var harness = Harness.Create();

        await harness.Gate.ClearAvailabilityAsync(CancellationToken.None);

        var message = Assert.Single(harness.Publisher.Messages);
        Assert.Equal(TopicMap.EnergyAvailableTopic, message.Topic);
        Assert.Equal(string.Empty, message.Payload);
        Assert.True(message.Retain);
    }

    [Fact]
    public async Task Wechsel_Auf_Unavailable_Leert_Retained_Werte()
    {
        var harness = Harness.Create();
        FillEnergyValues(harness.Cache);
        harness.Cache.Update(ModeId, Mode(1));

        await harness.Gate.ApplyAsync(CancellationToken.None);
        Assert.True(harness.Gate.IsAvailable);

        harness.Cache.Update(ModeId, Mode(0));
        await harness.Gate.ApplyAsync(CancellationToken.None);

        Assert.False(harness.Gate.IsAvailable);
        Assert.Equal("0", harness.Publisher.LastPayloadFor(harness.Topics.EnergyAvailableTopic));

        var cleared = harness.Publisher.Messages
            .Where(message => message.Retain && message.Payload.Length == 0 && IsEnergyTopic(message.Topic))
            .Select(message => message.Topic)
            .ToArray();

        Assert.Equal(11, cleared.Length);
        Assert.Equal(11, cleared.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task Veralteter_Mode_Sperrt_Und_Meldet_Unavailable()
    {
        var harness = Harness.Create();
        FillEnergyValues(harness.Cache);
        harness.Cache.Update(ModeId, Mode(1));

        await harness.Gate.ApplyAsync(CancellationToken.None);
        Assert.True(harness.Gate.IsAvailable);

        // Ein fehlgeschlagener Read markiert den Mode-Wert als veraltet.
        harness.Cache.MarkStale(ModeId);
        await harness.Gate.ApplyAsync(CancellationToken.None);

        Assert.Equal(EnergyAvailability.Unavailable, harness.Gate.Availability);

        var entry = Assert.Single(
            harness.Errors.Snapshot(),
            candidate => candidate.Code == EnergyMeasurementGate.UnavailableCode);

        Assert.Equal("warning", entry.Severity);
        Assert.False(harness.Gate.IsAvailable);
    }

    /// <summary>
    /// Trägt für alle 11 gesperrten Items einen Wert in den Cache ein.
    /// </summary>
    private static void FillEnergyValues(ValueCache cache)
    {
        foreach (var item in EnergyItems)
        {
            cache.Update(item.Id, new AnalogValue(42.5f, item.Min, item.Max, item.Unit));
        }
    }

    /// <summary>
    /// Die Betriebsart der Wärmemengenmessung als Enum-Wert.
    /// </summary>
    private static EnumValue Mode(int value) => new(value, ItemEnums.EnergyMeasurementModes);

    /// <summary>
    /// Ist das Topic eines der 11 gesperrten Energie-Items?
    /// </summary>
    private static bool IsEnergyTopic(string topic)
        => EnergyTopics.Contains(topic, StringComparer.Ordinal);

    /// <summary>
    /// Ist die Nachricht ein **Wert** eines gesperrten Energie-Items?
    /// </summary>
    /// <remarks>
    /// Ein leeres retained Publish ist das Aufräumen - es ist erlaubt (und
    /// beim Wechsel auf „nicht verfügbar" gewollt).
    /// </remarks>
    private static bool IsValueMessage(PublishedMessage message)
        => message.Payload.Length > 0 && IsEnergyTopic(message.Topic);

    /// <summary>
    /// Die Testumgebung: echter Cache, echter Katalog, protokollierender Publisher.
    /// </summary>
    private sealed class Harness
    {
        private Harness(
            ValueCache cache,
            FakeMqttPublisher publisher,
            ErrorCollector errors,
            MqttTopicMap topics,
            EnergyMeasurementGate gate,
            MqttStatePublisher statePublisher)
        {
            Cache = cache;
            Publisher = publisher;
            Errors = errors;
            Topics = topics;
            Gate = gate;
            StatePublisher = statePublisher;
        }

        public ValueCache Cache { get; }

        public FakeMqttPublisher Publisher { get; }

        public ErrorCollector Errors { get; }

        public MqttTopicMap Topics { get; }

        public EnergyMeasurementGate Gate { get; }

        public MqttStatePublisher StatePublisher { get; }

        public static Harness Create()
        {
            var cache = new ValueCache();
            var topics = new MqttTopicMap(Catalog, "ComfoBox", expertModeEnabled: false);
            var publisher = new FakeMqttPublisher();
            var errors = new ErrorCollector();
            var options = new MqttOptions
            {
                Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
            };

            var gate = new EnergyMeasurementGate(
                cache,
                Catalog,
                topics,
                publisher,
                errors,
                options,
                NullLogger<EnergyMeasurementGate>.Instance);

            var statePublisher = new MqttStatePublisher(
                cache,
                Catalog,
                topics,
                publisher,
                gate,
                options,
                NullLogger<MqttStatePublisher>.Instance);

            return new Harness(cache, publisher, errors, topics, gate, statePublisher);
        }
    }

    /// <summary>
    /// Eine publizierte Nachricht mit dem Retain-Flag.
    /// </summary>
    private sealed record PublishedMessage(string Topic, string Payload, bool Retain);

    /// <summary>
    /// Ein Publisher, der die Nachrichten aufzeichnet - ohne Broker.
    /// </summary>
    private sealed class FakeMqttPublisher : IMqttPublisher
    {
        private readonly List<PublishedMessage> _messages = [];
        private readonly object _sync = new();

        public IReadOnlyList<PublishedMessage> Messages
        {
            get
            {
                lock (_sync)
                {
                    return [.. _messages];
                }
            }
        }

        public Task<bool> PublishAsync(
            string topic,
            string payload,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _messages.Add(new PublishedMessage(topic, payload, retain));
            }

            return Task.FromResult(true);
        }

        public Task<bool> PublishJsonAsync(
            string topic,
            object value,
            MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
            bool retain = false,
            CancellationToken cancellationToken = default)
            => PublishAsync(topic, JsonSerializer.Serialize(value), qos, retain, cancellationToken);

        /// <summary>
        /// Die zuletzt publizierte Payload zu einem Topic (oder <c>null</c>).
        /// </summary>
        public string? LastPayloadFor(string topic)
            => Messages.LastOrDefault(message => message.Topic == topic)?.Payload;
    }
}
