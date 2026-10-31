using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für die Reaktion auf die „Geburt" von Home Assistant.
/// </summary>
/// <remarks>
/// Home Assistant publiziert auf <c>homeassistant/status</c> den Payload
/// <c>online</c>, wenn seine MQTT-Integration startet. Der Gateway muss
/// dann die Discovery und die Zustände wiederholen.
/// </remarks>
public sealed class HomeAssistantBirthHandlerTests
{
    private static readonly ItemCatalog Catalog = new();

    [Fact]
    public async Task Die_Geburt_Wiederholt_Die_Discovery_Und_Die_Zustaende()
    {
        var options = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
            DiscoveryPrefix = "homeassistant",
        };

        var cache = new ValueCache();
        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));

        var publisher = new RecordingMqttPublisher();
        var topics = new MqttTopicMap(Catalog, "ComfoBox", expertModeEnabled: false);

        var discovery = new HomeAssistantDiscovery(
            topics,
            Catalog,
            options,
            new GatewayOptions { BaseTopic = "ComfoBox" },
            NullLogger<HomeAssistantDiscovery>.Instance);

        var statePublisher = new MqttStatePublisher(
            cache,
            Catalog,
            topics,
            publisher,
            options,
            NullLogger<MqttStatePublisher>.Instance);

        var handler = new HomeAssistantBirthHandler(
            options,
            discovery,
            statePublisher,
            NullLogger<HomeAssistantBirthHandler>.Instance);

        var client = new NoopMqttClient();
        await handler.OnConnectedAsync(client, CancellationToken.None);

        Assert.Equal("homeassistant/status", handler.StatusTopic);

        // Die Geburt melden.
        await client.RaiseMessageAsync("homeassistant/status", "online");

        // Die Discovery wurde wiederholt (127 Items + Gerät + Status).
        var discoveryAnzahl = client.Published.Count(m => m.Topic.StartsWith("homeassistant/", StringComparison.Ordinal));
        Assert.True(discoveryAnzahl >= 127, $"Es wurden nur {discoveryAnzahl} Discovery-Payloads publiziert.");

        // Der Item-Wert wurde ebenfalls wiederholt.
        Assert.Equal("21.5", client.LastPayloadFor("ComfoBox/Zone/SetPointNormal"));
    }

    [Fact]
    public async Task Ein_Anderer_Payload_Loest_Nichts_Aus()
    {
        var options = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
            DiscoveryPrefix = "homeassistant",
        };

        var cache = new ValueCache();
        var publisher = new RecordingMqttPublisher();
        var topics = new MqttTopicMap(Catalog, "ComfoBox", expertModeEnabled: false);

        var discovery = new HomeAssistantDiscovery(
            topics,
            Catalog,
            options,
            new GatewayOptions { BaseTopic = "ComfoBox" },
            NullLogger<HomeAssistantDiscovery>.Instance);

        var statePublisher = new MqttStatePublisher(
            cache,
            Catalog,
            topics,
            publisher,
            options,
            NullLogger<MqttStatePublisher>.Instance);

        var handler = new HomeAssistantBirthHandler(
            options,
            discovery,
            statePublisher,
            NullLogger<HomeAssistantBirthHandler>.Instance);

        var client = new NoopMqttClient();
        await handler.OnConnectedAsync(client, CancellationToken.None);

        await client.RaiseMessageAsync("homeassistant/status", "offline");

        Assert.Empty(client.Published);
    }
}
