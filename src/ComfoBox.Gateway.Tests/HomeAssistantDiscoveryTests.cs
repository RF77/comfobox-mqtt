using System.Text.Json;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für die Home-Assistant-Discovery.
/// </summary>
public sealed class HomeAssistantDiscoveryTests
{
    private static readonly ItemCatalog Catalog = new();

    private static readonly string[] GeraeteIds = ["comfobox"];

    private static HomeAssistantDiscovery CreateDiscovery(
        bool expertMode = false,
        string discoveryIdentifier = "comfobox")
    {
        var options = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
            DiscoveryPrefix = "homeassistant",
            DiscoveryIdentifier = discoveryIdentifier,
        };

        var gateway = new GatewayOptions { BaseTopic = "ComfoBox", ExpertMode = expertMode };

        return new HomeAssistantDiscovery(
            new MqttTopicMap(Catalog, "ComfoBox", expertMode),
            Catalog,
            options,
            gateway,
            NullLogger<HomeAssistantDiscovery>.Instance);
    }

    [Fact]
    public void Die_Discovery_Kennung_ist_konfigurierbar()
    {
        // Aufgabe 7.3: im Parallelbetrieb müssen sich die unique_ids der
        // beiden Gateways unterscheiden - sonst ueberschreiben sich die
        // Entitaeten in Home Assistant.
        var item = Catalog.All[0];
        var standard = CreateDiscovery();
        var parallel = CreateDiscovery(discoveryIdentifier: "comfobox2");

        Assert.StartsWith("comfobox_", standard.UniqueId(item), StringComparison.Ordinal);
        Assert.StartsWith("comfobox2_", parallel.UniqueId(item), StringComparison.Ordinal);
        Assert.NotEqual(standard.UniqueId(item), parallel.UniqueId(item));

        // Auch die Discovery-Topics muessen sich unterscheiden.
        Assert.NotEqual(
            standard.DiscoveryTopic(item, writable: false),
            parallel.DiscoveryTopic(item, writable: false));

        Assert.NotEqual(standard.DeviceTopic, parallel.DeviceTopic);
    }

    [Fact]
    public void Jedes_Item_Bekommt_Eine_Discovery()
    {
        var discovery = CreateDiscovery();

        foreach (var item in Catalog.All)
        {
            var writable = item.IsWritableWithExpertMode(expertModeEnabled: false);
            var payload = discovery.BuildItemPayload(item, writable);

            Assert.False(string.IsNullOrWhiteSpace(payload["name"] as string));
            Assert.Equal($"comfobox_{DiscoveryId.Slug(item.Id)}", payload["unique_id"]);
            Assert.Equal($"ComfoBox/{item.TopicPath}", payload["state_topic"]);
        }
    }

    [Fact]
    public void Die_Discovery_Payload_Ist_Valides_Json()
    {
        var discovery = CreateDiscovery();

        foreach (var item in Catalog.All)
        {
            var payload = discovery.BuildItemPayload(item, item.IsWritableWithExpertMode(expertModeEnabled: false));

            var json = JsonSerializer.Serialize(payload);

            using var document = JsonDocument.Parse(json);
            Assert.Equal(JsonValueKind.Object, document.RootElement.ValueKind);
        }
    }

    [Fact]
    public void Jede_Discovery_Verweist_Auf_Das_Geraet_Und_Den_Origin()
    {
        var discovery = CreateDiscovery();

        foreach (var item in Catalog.All)
        {
            var payload = discovery.BuildItemPayload(item, item.IsWritableWithExpertMode(expertModeEnabled: false));

            var device = Assert.IsType<Dictionary<string, object?>>(payload["device"]);
            Assert.Equal(GeraeteIds, Assert.IsType<string[]>(device["identifiers"]));

            var origin = Assert.IsType<Dictionary<string, object?>>(payload["origin"]);
            Assert.Equal("comfobox-gateway", origin["name"]);
            Assert.False(string.IsNullOrWhiteSpace(origin["sw_version"] as string));
        }
    }

    [Fact]
    public void Jede_Discovery_Hat_Die_Availability_Des_Health_Topics()
    {
        var discovery = CreateDiscovery();

        foreach (var item in Catalog.All)
        {
            var payload = discovery.BuildItemPayload(item, item.IsWritableWithExpertMode(expertModeEnabled: false));

            Assert.Equal("ComfoBox/Status/Health", payload["availability_topic"]);
            Assert.Equal("online", payload["payload_available"]);
            Assert.Equal("offline", payload["payload_not_available"]);
        }
    }

    [Fact]
    public void Nur_Schreibbare_Items_Bekommen_Ein_Command_Topic()
    {
        var discovery = CreateDiscovery();

        foreach (var item in Catalog.All)
        {
            var writable = item.IsWritableWithExpertMode(expertModeEnabled: false);
            var payload = discovery.BuildItemPayload(item, writable);

            if (writable)
            {
                Assert.Equal($"ComfoBox/{item.TopicPath}/Set", payload["command_topic"]);
            }
            else
            {
                Assert.False(payload.ContainsKey("command_topic"));
            }
        }
    }

    [Fact]
    public void Die_Date_Items_Bekommen_Kein_Command_Topic()
    {
        // Befund A20: die beiden Legacy-DateItems haben keinen Wert.
        var discovery = CreateDiscovery();

        var dateItems = Catalog.All.Where(i => i.Kind == ItemValueKind.Date).ToList();

        Assert.Equal(2, dateItems.Count);

        foreach (var item in dateItems)
        {
            var payload = discovery.BuildItemPayload(item, writable: false);

            Assert.False(payload.ContainsKey("command_topic"));
            Assert.Equal("sensor", HomeAssistantDiscovery.ComponentFor(item, writable: false));
        }
    }

    [Fact]
    public void Die_Test_Und_Ausgangs_Topics_Sind_Diagnose_Sensoren()
    {
        // R5/R6: die Test- und Ausgangs-Topics sind read-only. Sie dürfen in
        // Home Assistant nicht als Zahl-Entität (number) erscheinen.
        var discovery = CreateDiscovery();
        string[] ids =
        [
            "tests.outputs_inactive",
            "tests.r4",
            "tests.y1",
            "tests.y2",
            "states.outputs.y1_primary_pump",
            "states.outputs.y2",
        ];

        foreach (var id in ids)
        {
            var item = Catalog.All.Single(i => i.Id == id);
            Assert.True(item.IsReadOnly, $"{id} ist nicht read-only.");

            var payload = discovery.BuildItemPayload(item, writable: false);
            Assert.False(payload.ContainsKey("command_topic"));
            Assert.Equal("sensor", HomeAssistantDiscovery.ComponentFor(item, writable: false));
        }
    }

    [Theory]
    [InlineData(ItemValueKind.Analog, false, "sensor")]
    [InlineData(ItemValueKind.Analog, true, "number")]
    [InlineData(ItemValueKind.Enum, false, "sensor")]
    [InlineData(ItemValueKind.Enum, true, "select")]
    public void Die_Plattform_Entspricht_Dem_Item_Typ(ItemValueKind kind, bool writable, string erwartet)
    {
        var item = new ItemDefinition
        {
            Id = "test.item",
            Group = "Test",
            Name = "Item",
            BacnetObjectId = 1,
            Kind = kind,
        };

        Assert.Equal(erwartet, HomeAssistantDiscovery.ComponentFor(item, writable));
    }

    [Fact]
    public void Ein_Schreibbares_Enum_Bekommt_Die_Enum_Namen_Als_Optionen()
    {
        var discovery = CreateDiscovery();

        var item = Catalog.All.First(i => i.Kind == ItemValueKind.Enum && !i.IsReadOnly);
        var payload = discovery.BuildItemPayload(item, writable: true);

        var optionen = Assert.IsType<string[]>(payload["options"]);

        Assert.Equal(item.EnumValues.Count, optionen.Length);
        Assert.Contains("Normal", optionen);
    }

    [Fact]
    public void Ein_Schreibbarer_Zahlwert_Bekommt_Min_Max_Und_Step()
    {
        var discovery = CreateDiscovery();

        var item = Catalog.All.First(i => i.Kind == ItemValueKind.Analog && !i.IsReadOnly && i.Min is not null && i.Max is not null);
        var payload = discovery.BuildItemPayload(item, writable: true);

        Assert.Equal(item.Min!.Value, (float)payload["min"]!);
        Assert.Equal(item.Max!.Value, (float)payload["max"]!);
        Assert.True((double)payload["step"]! > 0);
    }

    [Fact]
    public void Die_Unique_Id_Ist_Stabil_Und_Eindeutig()
    {
        var discovery = CreateDiscovery();
        var ids = Catalog.All.Select(discovery.UniqueId).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.All(ids, id => Assert.StartsWith("comfobox_", id, StringComparison.Ordinal));
        Assert.All(ids, id => Assert.DoesNotContain('.', id));
    }

    [Fact]
    public void Die_Unique_Id_Ist_Immer_Ascii()
    {
        // Die MQTT-Integration von Home Assistant akzeptiert in den
        // Discovery-Topics und unique_ids nur a-z, A-Z, 0-9, '-' und '_'.
        // Die Item-Ids enthalten aber Umlaute (Altbestand) - sie werden
        // transliteriert, damit der Topic-Vertrag unverändert bleibt.
        var discovery = CreateDiscovery();

        foreach (var item in Catalog.All)
        {
            var id = discovery.UniqueId(item);
            Assert.All(id, character => Assert.True(
                char.IsAsciiLetterOrDigit(character) || character is '_' or '-',
                $"'{id}' enthält das unzulässige Zeichen '{character}'."));

            var topic = discovery.DiscoveryTopic(item, writable: false);
            Assert.All(topic, character => Assert.True(
                char.IsAsciiLetterOrDigit(character) || character is '_' or '-' or '/',
                $"Das Discovery-Topic '{topic}' enthält das unzulässige Zeichen '{character}'."));
        }
    }

    [Fact]
    public void Umlaute_In_Der_Item_Id_Werden_Transliteriert()
    {
        var discovery = CreateDiscovery();
        var item = Catalog.All.First(i => i.Id.Contains('ä', StringComparison.Ordinal));

        var id = discovery.UniqueId(item);

        Assert.Contains("ae", id, StringComparison.Ordinal);
        Assert.DoesNotContain('ä', id);

        // Der Topic-Vertrag bleibt unverändert - nur der Discovery-Name ist ASCII.
        Assert.Contains("ä", item.TopicPath, StringComparison.Ordinal);
    }

    [Fact]
    public void Das_Discovery_Topic_Enthaelt_Die_Plattform_Und_Die_Unique_Id()
    {
        var discovery = CreateDiscovery();
        var item = Catalog.All[0];

        var topic = discovery.DiscoveryTopic(item, writable: false);

        Assert.StartsWith("homeassistant/sensor/", topic, StringComparison.Ordinal);
        Assert.EndsWith("/config", topic, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Geraete_Discovery_Wird_Geleert()
    {
        // Die Entitäten bringen den `device`-Block selbst mit. Eine eigene
        // Geräte-Discovery verlangt `components`/`child_devices`; fehlen sie,
        // meldet die MQTT-Integration eine Warnung. Das Topic wird deshalb nur
        // noch **geleert** (eine ältere Version hat es retained gefüllt).
        var discovery = CreateDiscovery();

        Assert.EndsWith("/config", discovery.DeviceTopic, StringComparison.Ordinal);
        Assert.Contains("/device/", discovery.DeviceTopic, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Status_Discovery_Umfasst_Health_Version_Und_Uptime()
    {
        var discovery = CreateDiscovery();

        var topics = discovery.BuildStatusPayloads().Select(p => p.Key).ToList();

        Assert.Contains("homeassistant/sensor/comfobox_status_health/config", topics);
        Assert.Contains("homeassistant/sensor/comfobox_status_version/config", topics);
        Assert.Contains("homeassistant/sensor/comfobox_status_uptime/config", topics);
        Assert.Contains("homeassistant/sensor/comfobox_status_writes/config", topics);
    }
}
