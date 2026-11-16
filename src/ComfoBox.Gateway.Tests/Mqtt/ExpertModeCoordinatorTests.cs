using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using ComfoBox.Gateway.Protection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für den Koordinator des Experten-Modus (Aufgabe 10.13).
/// </summary>
/// <remarks>
/// Der Koordinator verbindet drei Dienste, die kein gemeinsames Interface
/// haben: Topic-Zuordnung, Subscriber und Discovery. Die Tests bauen deshalb
/// die echten Typen mit Attrappen an den Rändern (MQTT-Client, BACnet-Client)
/// - so wird geprüft, was der Benutzer sieht: die <c>/Set</c>-Topics und die
/// Discovery-Payloads.
/// </remarks>
public sealed class ExpertModeCoordinatorTests : IDisposable
{
    private const string ExpertItemTopicPath = "Config/Ventilation/SpeedStage3";

    private static readonly ItemCatalog Catalog = new();

    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "comfobox-expert-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBacnetClient _bacnetClient = new();
    private readonly ErrorCollector _errors = new();

    public ExpertModeCoordinatorTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public async Task Ohne_Verbindung_Wird_Nur_Die_Zuordnung_Umgestellt()
    {
        using var context = CreateContext();
        var item = Catalog.FindByTopicPath(ExpertItemTopicPath)!;
        var setTopic = context.Map.ItemSetTopic(item);

        context.Settings.ExpertMode = true;
        await context.Coordinator.ApplyAsync();

        // Die Zuordnung gilt auch ohne Broker - der nächste Connect nutzt sie.
        Assert.True(context.Map.ExpertModeEnabled);
        Assert.Contains(setTopic, context.Map.WriteTopics());

        // "Keine Verbindung" ist kein Fehler.
        Assert.Empty(_errors.Snapshot());
    }

    [Fact]
    public async Task Mit_Verbindung_Werden_Abonnements_Und_Discovery_Umgestellt()
    {
        using var context = CreateContext();
        var client = new NoopMqttClient();
        context.Registry.Add(client);

        var item = Catalog.FindByTopicPath(ExpertItemTopicPath)!;
        var setTopic = context.Map.ItemSetTopic(item);

        await context.Subscriber.OnConnectedAsync(client, CancellationToken.None);
        Assert.DoesNotContain(setTopic, client.ActiveSubscriptions);

        context.Settings.ExpertMode = true;
        await context.Coordinator.ApplyAsync();

        Assert.True(context.Map.ExpertModeEnabled);
        Assert.Contains(setTopic, client.ActiveSubscriptions);
        Assert.NotNull(client.LastPayloadFor(context.Discovery.DiscoveryTopic(item, writable: true)));
        Assert.Empty(_errors.Snapshot());
    }

    /// <summary>
    /// Das Ereignis der Laufzeit-Einstellungen löst den Umbau aus.
    /// </summary>
    /// <remarks>
    /// Der Handler startet den Umbau im Hintergrund (der Setter läuft im
    /// HTTP-Request) - der Test wartet deshalb auf die Wirkung, nicht auf eine
    /// feste Zeit.
    /// </remarks>
    [Fact]
    public async Task Das_Ereignis_Loest_Den_Umbau_Aus()
    {
        using var context = CreateContext();
        var client = new NoopMqttClient();
        context.Registry.Add(client);

        var item = Catalog.FindByTopicPath(ExpertItemTopicPath)!;
        var setTopic = context.Map.ItemSetTopic(item);

        context.Settings.ExpertMode = true;

        Assert.True(
            await WaitForAsync(() => client.ActiveSubscriptions.Contains(setTopic)),
            "Das Schreib-Topic wurde nicht abonniert.");

        Assert.True(
            await WaitForAsync(() =>
                client.LastPayloadFor(context.Discovery.DiscoveryTopic(item, writable: true)) is not null),
            "Die Discovery wurde nicht neu publiziert.");
    }

    /// <summary>
    /// Ein Fehler in einem Schritt bricht die übrigen nicht ab.
    /// </summary>
    [Fact]
    public async Task Ein_Fehler_Im_Schritt_Bricht_Die_Anderen_Nicht_Ab()
    {
        using var context = CreateContext();
        var client = new NoopMqttClient { SubscribeFailure = new InvalidOperationException("Broker weg.") };
        context.Registry.Add(client);

        var item = Catalog.FindByTopicPath(ExpertItemTopicPath)!;

        context.Settings.ExpertMode = true;
        await context.Coordinator.ApplyAsync();

        // Die Zuordnung wurde umgestellt ...
        Assert.True(context.Map.ExpertModeEnabled);

        // ... und die Discovery lief weiter, obwohl das Abonnieren scheiterte.
        Assert.NotNull(client.LastPayloadFor(context.Discovery.DiscoveryTopic(item, writable: true)));

        // Gemeldet wird der Fehler mit Quelle und Code des Koordinators.
        // (Der Setter hat den Umbau zusätzlich im Hintergrund angestossen -
        // deshalb zählt der Test die Meldungen nicht, sondern prüft sie.)
        var fehler = _errors.Snapshot();
        Assert.NotEmpty(fehler);
        Assert.All(fehler, e =>
        {
            Assert.Equal(ExpertModeCoordinator.ErrorSource, e.Source);
            Assert.Equal(ExpertModeCoordinator.ErrorCode, e.Code);
        });
    }

    /// <summary>
    /// Nach <c>Dispose</c> wirkt kein Wechsel mehr - das Ereignis ist abbestellt.
    /// </summary>
    [Fact]
    public async Task Nach_Dispose_Wirkt_Kein_Wechsel_Mehr()
    {
        var context = CreateContext();
        context.Coordinator.Dispose();

        // Das Ereignis ist abbestellt und ApplyAsync steigt sofort aus.
        context.Settings.ExpertMode = true;
        await context.Coordinator.ApplyAsync();

        Assert.False(context.Map.ExpertModeEnabled);
    }

    /// <summary>
    /// Baut den Koordinator mit echten Diensten und Attrappen an den Rändern.
    /// </summary>
    /// <param name="expertMode">Der Startwert des Experten-Modus.</param>
    /// <returns>Der Kontext (entsorgt den Koordinator).</returns>
    private TestContext CreateContext(bool expertMode = false)
    {
        var settings = new GatewayRuntimeSettings(expertMode);
        var map = new MqttTopicMap(Catalog, "ComfoBox", expertMode);

        var bacnet = Options.Create(new BacnetOptions
        {
            Port = "COM3",
            Baudrate = 38400,
            DeviceId = 1,
            ItemTimeoutMs = 500,
        });

        var gateway = Options.Create(new GatewayOptions
        {
            BaseTopic = "ComfoBox",
            ExpertMode = expertMode,
        });

        var device = new BacnetDevice(
            _bacnetClient,
            new ValueCache(),
            _errors,
            new WriteCounter(Path.Combine(_directory, WriteCounter.FileName)),
            bacnet,
            gateway,
            new BacnetRuntimeSettings(bacnet.Value.DeviceId, bacnet.Value.ItemTimeoutMs),
            settings,
            NullLogger<BacnetDevice>.Instance);

        var subscriber = new MqttSubscriber(
            map,
            device,
            _errors,
            new RecordingMqttPublisher(),
            NullLogger<MqttSubscriber>.Instance);

        var mqttOptions = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
            DiscoveryPrefix = "homeassistant",
        };

        var discovery = new HomeAssistantDiscovery(
            map,
            Catalog,
            mqttOptions,
            settings,
            NullLogger<HomeAssistantDiscovery>.Instance);

        var registry = new MqttClientRegistry();

        var coordinator = new ExpertModeCoordinator(
            settings,
            map,
            subscriber,
            discovery,
            registry,
            _errors,
            NullLogger<ExpertModeCoordinator>.Instance);

        return new TestContext(settings, map, subscriber, discovery, registry, coordinator);
    }

    /// <summary>
    /// Wartet, bis die Bedingung erfüllt ist (der Umbau läuft im Hintergrund).
    /// </summary>
    /// <param name="condition">Die Bedingung.</param>
    /// <returns><c>true</c>, wenn sie innerhalb von fünf Sekunden erfüllt war.</returns>
    private static async Task<bool> WaitForAsync(Func<bool> condition)
    {
        var end = DateTime.UtcNow + TimeSpan.FromSeconds(5);

        while (DateTime.UtcNow < end)
        {
            if (condition())
            {
                return true;
            }

            await Task.Delay(20).ConfigureAwait(false);
        }

        return condition();
    }

    /// <summary>
    /// Die Bausteine eines Tests.
    /// </summary>
    /// <param name="Settings">Die Laufzeit-Einstellungen.</param>
    /// <param name="Map">Die Topic-Zuordnung.</param>
    /// <param name="Subscriber">Der Subscriber.</param>
    /// <param name="Discovery">Die Discovery.</param>
    /// <param name="Registry">Das Client-Verzeichnis.</param>
    /// <param name="Coordinator">Der Koordinator.</param>
    private sealed record TestContext(
        GatewayRuntimeSettings Settings,
        MqttTopicMap Map,
        MqttSubscriber Subscriber,
        HomeAssistantDiscovery Discovery,
        MqttClientRegistry Registry,
        ExpertModeCoordinator Coordinator) : IDisposable
    {
        /// <inheritdoc />
        public void Dispose() => Coordinator.Dispose();
    }
}
