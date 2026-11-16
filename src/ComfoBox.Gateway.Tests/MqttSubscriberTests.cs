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
/// Tests für den MQTT-Subscriber - der Schreibpfad von außen.
/// </summary>
/// <remarks>
/// Der wichtigste Test ist <see cref="Ein_Schreibvorgang_Erhoeht_Den_Schreibzaehler"/>:
/// der Schreibschutz (24-h-Limit) darf **nicht** umgangen werden.
/// </remarks>
public sealed class MqttSubscriberTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "comfobox-mqtt-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBacnetClient _client = new();
    private readonly ValueCache _cache = new();
    private readonly ErrorCollector _errors = new();
    private readonly WriteCounter _counter;
    private readonly RecordingMqttPublisher _publisher = new();
    private readonly ItemCatalog _catalog = new();

    public MqttSubscriberTests()
    {
        Directory.CreateDirectory(_directory);
        _counter = new WriteCounter(Path.Combine(_directory, WriteCounter.FileName));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    private MqttSubscriber CreateSubscriber(bool expertMode = false)
        => CreateSubscriberWithMap(expertMode).Subscriber;

    /// <summary>
    /// Erzeugt Subscriber **und** Topic-Zuordnung - für die Tests des
    /// Moduswechsels, die beide brauchen.
    /// </summary>
    /// <param name="expertMode">Der Startwert des Experten-Modus.</param>
    /// <returns>Das Paar aus Subscriber und Zuordnung.</returns>
    private (MqttSubscriber Subscriber, MqttTopicMap Map) CreateSubscriberWithMap(bool expertMode = false)
    {
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
            _client,
            _cache,
            _errors,
            _counter,
            bacnet,
            gateway,
            new BacnetRuntimeSettings(bacnet.Value.DeviceId, bacnet.Value.ItemTimeoutMs),
            new GatewayRuntimeSettings(expertMode),
            NullLogger<BacnetDevice>.Instance);

        var map = new MqttTopicMap(_catalog, "ComfoBox", expertMode);

        var subscriber = new MqttSubscriber(
            map,
            device,
            _errors,
            _publisher,
            NullLogger<MqttSubscriber>.Instance);

        return (subscriber, map);
    }

    [Fact]
    public async Task Ein_Schreibvorgang_Erhoeht_Den_Schreibzaehler()
    {
        var subscriber = CreateSubscriber();
        var item = _catalog.All.First(i => i.Id == "zone.set_point_normal");

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "21.5");

        Assert.Equal(1, _counter.Count);
        Assert.Contains((uint)item.BacnetObjectId, _client.WrittenObjects);
        Assert.Equal(21.5f, _client.Values[(uint)item.BacnetObjectId]);
    }

    [Fact]
    public async Task Ein_Schreibvorgang_Wird_Auf_Dem_Topic_Veroeffentlicht()
    {
        var subscriber = CreateSubscriber();
        var item = _catalog.All.First(i => i.Id == "zone.set_point_normal");

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "21.5");

        Assert.Equal("21.5", _publisher.LastPayloadFor($"ComfoBox/{item.TopicPath}"));
    }

    [Fact]
    public async Task Ein_Enum_Wird_Ueber_Den_Namen_Geschrieben()
    {
        var subscriber = CreateSubscriber();
        var item = _catalog.All.First(i => i.Kind == ItemValueKind.Enum && !i.IsReadOnly);

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "Normal");

        Assert.Equal(6f, _client.Values[(uint)item.BacnetObjectId]);
        Assert.Equal(1, _counter.Count);
    }

    [Fact]
    public async Task Ein_Enum_Wird_Ueber_Die_Zahl_Geschrieben()
    {
        var subscriber = CreateSubscriber();
        var item = _catalog.All.First(i => i.Kind == ItemValueKind.Enum && !i.IsReadOnly);

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/AsNumber/Set", "6");

        Assert.Equal(6f, _client.Values[(uint)item.BacnetObjectId]);
    }

    [Fact]
    public async Task Eine_Ungueltige_Enum_Payload_Schreibt_Keinen_Null_Wert()
    {
        // Befund A6: der Legacy hat hier den Wert 0 geschrieben.
        var subscriber = CreateSubscriber();
        var item = _catalog.All.First(i => i.Kind == ItemValueKind.Enum && !i.IsReadOnly);

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "abc");

        Assert.Empty(_client.WrittenObjects);
        Assert.Equal(0, _counter.Count);

        var fehler = Assert.Single(_errors.Snapshot());
        Assert.Equal("invalid_value", fehler.Code);
    }

    [Fact]
    public async Task Ein_Dezimalkomma_Wird_Abgelehnt()
    {
        var subscriber = CreateSubscriber();
        var item = _catalog.All.First(i => i.Id == "zone.set_point_normal");

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "21,5");

        Assert.Empty(_client.WrittenObjects);
        Assert.Equal(0, _counter.Count);
        Assert.Single(_errors.Snapshot());
    }

    [Fact]
    public async Task Ein_Wert_Ausserhalb_Des_Bereichs_Wird_Abgelehnt()
    {
        var subscriber = CreateSubscriber();
        var item = _catalog.All.First(i => i.Id == "zone.set_point_normal");

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "999");

        Assert.Empty(_client.WrittenObjects);
        Assert.Equal(0, _counter.Count);

        var fehler = Assert.Single(_errors.Snapshot());
        Assert.Equal("value_out_of_range", fehler.Code);
    }

    [Fact]
    public async Task Ein_Read_Only_Item_Hat_Kein_Set_Topic()
    {
        var subscriber = CreateSubscriber();
        var item = _catalog.All.First(i => i.IsReadOnly);

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "1");

        Assert.Empty(_client.WrittenObjects);
        Assert.Empty(_errors.Snapshot());
    }

    [Fact]
    public async Task Der_Schreibzaehler_Begrenzt_Die_Anzahl_Der_Schreibvorgaenge()
    {
        var directory = Path.Combine(_directory, "limit");
        Directory.CreateDirectory(directory);

        var counter = new WriteCounter(Path.Combine(directory, WriteCounter.FileName), limit: 1);
        var bacnet = Options.Create(new BacnetOptions { Port = "COM3", Baudrate = 38400, DeviceId = 1 });
        var gateway = Options.Create(new GatewayOptions { BaseTopic = "ComfoBox" });

        var device = new BacnetDevice(
            _client,
            _cache,
            _errors,
            counter,
            bacnet,
            gateway,
            new BacnetRuntimeSettings(bacnet.Value.DeviceId, bacnet.Value.ItemTimeoutMs),
            new GatewayRuntimeSettings(expertMode: false),
            NullLogger<BacnetDevice>.Instance);

        var subscriber = new MqttSubscriber(
            new MqttTopicMap(_catalog, "ComfoBox", expertModeEnabled: false),
            device,
            _errors,
            _publisher,
            NullLogger<MqttSubscriber>.Instance);

        var item = _catalog.All.First(i => i.Id == "zone.set_point_normal");

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "21.5");
        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "22.5");

        Assert.Single(_client.WrittenObjects);
        Assert.Equal(1, counter.Count);
        Assert.Contains(_errors.Snapshot(), e => e.Code == "write_limit_exceeded");
    }

    [Fact]
    public async Task Ein_Experten_Item_Wird_Ohne_Experten_Modus_Abgelehnt()
    {
        var subscriber = CreateSubscriber(expertMode: false);
        var item = _catalog.All.First(i => i.Id == "config.general.sommerknick");

        // Ohne Experten-Modus gibt es kein /Set-Topic.
        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "1");

        Assert.Empty(_client.WrittenObjects);
    }

    [Fact]
    public async Task Mit_Experten_Modus_Wird_Das_Set_Topic_Abonniert()
    {
        var subscriber = CreateSubscriber(expertMode: true);
        var item = _catalog.All.First(i => i.Id == "config.general.sommerknick");

        await subscriber.HandleMessageAsync($"ComfoBox/{item.TopicPath}/Set", "1");

        Assert.Contains((uint)item.BacnetObjectId, _client.WrittenObjects);
        Assert.Equal(1, _counter.Count);
    }

    /// <summary>
    /// Beim Einschalten des Experten-Modus werden die alten Topics abbestellt
    /// und die neuen abonniert (Aufgabe 10.13).
    /// </summary>
    [Fact]
    public async Task RefreshSubscriptions_Bestellt_Alte_Topics_Ab_Und_Neue_An()
    {
        var (subscriber, map) = CreateSubscriberWithMap(expertMode: false);
        var client = new NoopMqttClient();

        await subscriber.OnConnectedAsync(client, CancellationToken.None);

        var ohneExperten = map.WriteTopics().Count;
        Assert.Equal(ohneExperten, subscriber.SubscribedCount);
        Assert.Empty(client.UnsubscribedTopicFilters);

        var item = _catalog.FindByTopicPath("Config/Ventilation/SpeedStage3");
        Assert.NotNull(item);

        var setTopic = map.ItemSetTopic(item!);
        Assert.DoesNotContain(setTopic, client.ActiveSubscriptions);

        // Einschalten: die Zuordnung ändert sich, der Subscriber zieht nach.
        map.UpdateExpertMode(true);
        await subscriber.RefreshSubscriptionsAsync([client], CancellationToken.None);

        // Die vorherigen Topics sind abbestellt, die neuen abonniert.
        Assert.Equal(ohneExperten, client.UnsubscribedTopicFilters.Count);
        Assert.Equal(map.WriteTopics().Count, subscriber.SubscribedCount);

        // Alle experten-pflichtigen Items kommen hinzu (21 Config + 1 Command
        // aus V2, 34 aus dem V1-Altbestand).
        var expertenItems = _catalog.All.Count(i => i.RequiresExpertMode && i.IsWritable);
        Assert.Equal(ohneExperten + 2 * expertenItems, subscriber.SubscribedCount);
        Assert.Contains(setTopic, subscriber.SubscribedTopics);
        Assert.Contains(setTopic, client.ActiveSubscriptions);

        // Ausschalten: das Schreib-Topic verschwindet wieder.
        map.UpdateExpertMode(false);
        await subscriber.RefreshSubscriptionsAsync([client], CancellationToken.None);

        Assert.DoesNotContain(setTopic, subscriber.SubscribedTopics);
        Assert.DoesNotContain(setTopic, client.ActiveSubscriptions);
        Assert.Equal(ohneExperten, subscriber.SubscribedCount);

        // Die abbestellte Menge enthält beim zweiten Wechsel die Experten-Topics.
        Assert.Contains(setTopic, client.UnsubscribedTopicFilters);
    }

    /// <summary>
    /// Ein Moduswechsel hängt den Nachrichten-Handler nicht ein zweites Mal an.
    /// </summary>
    /// <remarks>
    /// Zwei Handler würden jede Nachricht doppelt verarbeiten - und damit
    /// zweimal schreiben. Der 24-h-Zähler liefe doppelt hoch.
    /// </remarks>
    [Fact]
    public async Task RefreshSubscriptions_Registriert_Den_Handler_Nur_Einmal()
    {
        var (subscriber, map) = CreateSubscriberWithMap(expertMode: true);
        var client = new NoopMqttClient();

        await subscriber.OnConnectedAsync(client, CancellationToken.None);
        Assert.Equal(1, client.MessageHandlerCount);

        map.UpdateExpertMode(false);
        await subscriber.RefreshSubscriptionsAsync([client], CancellationToken.None);

        map.UpdateExpertMode(true);
        await subscriber.RefreshSubscriptionsAsync([client], CancellationToken.None);

        Assert.Equal(1, client.MessageHandlerCount);
    }

    /// <summary>
    /// Nach einem Moduswechsel wird eine Nachricht **einmal** verarbeitet.
    /// </summary>
    [Fact]
    public async Task Nach_Dem_Moduswechsel_Wird_Eine_Nachricht_Nur_Einmal_Verarbeitet()
    {
        var (subscriber, map) = CreateSubscriberWithMap(expertMode: true);
        var client = new NoopMqttClient();

        await subscriber.OnConnectedAsync(client, CancellationToken.None);

        map.UpdateExpertMode(false);
        await subscriber.RefreshSubscriptionsAsync([client], CancellationToken.None);

        var item = _catalog.All.First(i => i.Id == "zone.set_point_normal");

        await client.RaiseMessageAsync($"ComfoBox/{item.TopicPath}/Set", "21.5");

        Assert.Single(_client.WrittenObjects);
        Assert.Equal(1, _counter.Count);
    }
}
