using System.Text.Json;
using System.Text.Json.Serialization;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Mqtt;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für die Topic-Zuordnung - der Vertrag zum Altbestand.
/// </summary>
/// <remarks>
/// Die Fixture <c>docs/topics.json</c> stammt aus dem Legacy-Stand von 2017
/// und ist **eingefroren**. Sie darf nicht verändert werden.
/// </remarks>
public sealed class MqttTopicMapTests
{
    private const string BaseTopic = "ComfoBox";

    private static readonly ItemCatalog Catalog = new();

    private static readonly FixtureItem[] Fixture = LoadFixture();

    private static MqttTopicMap CreateMap(bool expertMode = false)
        => new(Catalog, BaseTopic, expertMode);

    [Fact]
    public void Jedes_Item_Bekommt_Sein_Topic_Aus_Gruppe_Und_Name()
    {
        var map = CreateMap();

        foreach (var item in Catalog.All)
        {
            Assert.Equal($"{BaseTopic}/{item.TopicPath}", map.ItemTopic(item));
        }
    }

    [Fact]
    public void Die_Topics_Stimmen_Mit_Der_Fixture_Ueberein()
    {
        var map = CreateMap();

        foreach (var entry in Fixture)
        {
            var topicPath = TopicPathOf(entry);
            var item = Catalog.FindByTopicPath(topicPath);

            Assert.NotNull(item);
            Assert.Equal(entry.SubscribeTopic, map.ItemTopic(item!));
        }
    }

    [Fact]
    public void Die_Set_Topics_Stimmen_Mit_Der_Fixture_Ueberein()
    {
        var map = CreateMap();

        foreach (var entry in Fixture)
        {
            var item = Catalog.FindByTopicPath(TopicPathOf(entry))!;

            var writable = item.IsWritableWithExpertMode(expertModeEnabled: false);

            Assert.Equal(entry.PublishTopic, writable ? map.ItemSetTopic(item) : null);
        }
    }

    [Fact]
    public void Die_Anzahl_Der_Set_Topics_Stimmt_Mit_Der_Fixture_Ueberein()
    {
        var map = CreateMap();

        var erwartet = Fixture.Count(e => e.PublishTopic is not null);

        Assert.Equal(erwartet, map.SetTopics().Count);
    }

    [Fact]
    public void Jedes_Enum_Item_Bekommt_Ein_AsNumber_Set_Topic()
    {
        var map = CreateMap();

        foreach (var item in Catalog.All.Where(i =>
            i.Kind == ItemValueKind.Enum && i.IsWritableWithExpertMode(expertModeEnabled: false)))
        {
            Assert.Contains(map.ItemAsNumberSetTopic(item), map.WriteTopics());
        }
    }

    [Fact]
    public void Es_Gibt_Keine_Virtual_Topics()
    {
        var map = CreateMap();

        Assert.DoesNotContain(Catalog.All, i => i.Group == "Virtual");
        Assert.DoesNotContain(map.WriteTopics(), t => t.Contains("/Virtual/", StringComparison.Ordinal));
    }

    [Fact]
    public void Ein_Topic_Laesst_Sich_Zum_Item_Aufloesen()
    {
        var map = CreateMap();
        var item = Catalog.All.First(i => i.Kind == ItemValueKind.Enum && !i.IsReadOnly);

        Assert.True(map.TryResolve(map.ItemTopic(item), out var gefunden, out var form));
        Assert.Equal(item.Id, gefunden!.Id);
        Assert.Equal(MqttTopicForm.Value, form);

        Assert.True(map.TryResolve(map.ItemAsNumberTopic(item), out var gefunden2, out var form2));
        Assert.Equal(item.Id, gefunden2!.Id);
        Assert.Equal(MqttTopicForm.AsNumber, form2);
    }

    [Fact]
    public void Ein_Unbekanntes_Topic_Wird_Nicht_Aufgeloest()
    {
        var map = CreateMap();

        Assert.False(map.TryResolve("ComfoBox/Zone/Unbekannt", out var item, out _));
        Assert.Null(item);
    }

    [Fact]
    public void Die_Sonder_Und_Status_Topics_Sind_Vertragskonform()
    {
        var map = CreateMap();

        Assert.Equal("ComfoBox/Connected", map.ConnectedTopic);
        Assert.Equal("ComfoBox/Version", map.VersionTopic);
        Assert.Equal("ComfoBox/Special/NumberOfWritesPer24h", map.NumberOfWritesTopic);
        Assert.Equal("ComfoBox/Status/Health", map.HealthTopic);
        Assert.Equal("ComfoBox/Command/Restart", map.RestartCommandTopic);
    }

    [Fact]
    public void Neue_Topics_Liegen_Ausschliesslich_Unter_Status_Und_Command()
    {
        var map = CreateMap();

        var neueTopics = new[]
        {
            map.HealthTopic,
            map.StatusVersionTopic,
            map.UptimeTopic,
            map.StartedAtTopic,
            map.WritesTopic,
            map.WritesMaxTopic,
            map.BacnetTopic,
            map.MqttTopic,
            map.LastErrorTopic,
            map.ErrorsTopic,
            map.RestartTopic,
            map.RestartCommandTopic,
        };

        foreach (var topic in neueTopics)
        {
            var istStatus = topic.StartsWith($"{BaseTopic}/Status/", StringComparison.Ordinal);
            var istCommand = topic.StartsWith($"{BaseTopic}/Command/", StringComparison.Ordinal);

            Assert.True(istStatus || istCommand, $"{topic} liegt weder unter Status/ noch unter Command/.");
        }
    }

    /// <summary>
    /// Das V2-Command-Topic existiert nur mit Experten-Modus als `/Set`.
    /// </summary>
    /// <remarks>
    /// <c>Command/ResetHeatingCurveAdaption</c> schreibt DP 678 und verändert
    /// die Regelung. ADR-014: ohne Experten-Modus gibt es kein Schreib-Topic,
    /// dann bleibt nur der Lesewert.
    /// </remarks>
    [Fact]
    public void Das_Command_Topic_Ist_Nur_Mit_Experten_Modus_Schreibbar()
    {
        var item = Catalog.FindByTopicPath("Command/ResetHeatingCurveAdaption");
        Assert.NotNull(item);

        var ohneExperten = CreateMap(expertMode: false);
        var mitExperten = CreateMap(expertMode: true);

        var setTopic = mitExperten.ItemSetTopic(item!);

        Assert.Equal("ComfoBox/Command/ResetHeatingCurveAdaption", ohneExperten.ItemTopic(item!));
        Assert.Equal("ComfoBox/Command/ResetHeatingCurveAdaption/Set", setTopic);

        // Ohne Experten-Modus gibt es kein Schreib-Topic.
        Assert.False(ohneExperten.TryResolve(setTopic, out _, out _));
        Assert.DoesNotContain(ohneExperten.WriteTopics(),
            topic => topic.StartsWith("ComfoBox/Command/", StringComparison.Ordinal));

        // Mit Experten-Modus ist es vorhanden und schreibbar.
        Assert.Contains(mitExperten.WriteTopics(), topic => topic == setTopic);
        Assert.True(mitExperten.TryResolve(setTopic, out var gefunden, out var form));
        Assert.Equal(item!.Id, gefunden!.Id);
        Assert.Equal(MqttTopicForm.Value, form);
    }

    /// <summary>
    /// Die 21 Konfigurations-Topics aus Welle 2c existieren nur mit Experten-Modus als `/Set`.
    /// </summary>
    [Fact]
    public void Die_Config_Topics_Existieren_Nur_Mit_Experten_Modus_Als_Set()
    {
        var configItems = Catalog.V2
            .Where(item => item.IsWritable)
            .Where(item => item.TopicPath.StartsWith("Config/", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(20, configItems.Count);

        var ohneExperten = CreateMap(expertMode: false);
        var mitExperten = CreateMap(expertMode: true);

        var ohneSet = ohneExperten.SetTopics().ToHashSet(StringComparer.Ordinal);
        var mitSet = mitExperten.SetTopics().ToHashSet(StringComparer.Ordinal);

        foreach (var item in configItems)
        {
            var setTopic = $"{BaseTopic}/{item.TopicPath}/Set";

            Assert.DoesNotContain(setTopic, ohneSet);
            Assert.Contains(setTopic, mitSet);
        }
    }

    /// <summary>
    /// Die Availability-Topics der Baugruppen sind Gateway-Topics.
    /// </summary>
    /// <remarks>
    /// <c>Status/Hardware/…</c> hat kein BACnet-Objekt - es wird aus den
    /// Typ-Parametern gebildet (siehe <c>HardwareAvailabilityGate</c>).
    /// </remarks>
    [Fact]
    public void HardwareTopics_Liegen_Unter_Status_Hardware()
    {
        var map = CreateMap();

        Assert.Equal(
            "ComfoBox/Status/Hardware/HeatPumpSensors",
            map.HardwareTopic(HardwareRequirements.HeatPumpSensors));
        Assert.Equal(
            "ComfoBox/Status/Hardware/BufferBottom",
            map.HardwareTopic(HardwareRequirements.BufferBottom));

        // Kein Katalog-Item trägt einen dieser Pfade - sie gehören dem Gateway.
        foreach (var requirement in HardwareRequirements.All)
        {
            var path = $"Status/{requirement.TopicPath}";
            Assert.DoesNotContain(Catalog.All, item => item.TopicPath == path);
            Assert.Null(Catalog.FindByTopicPath(path));
        }
    }

    /// <summary>
    /// Der Experten-Modus lässt sich zur Laufzeit einschalten (Aufgabe 10.13).
    /// </summary>
    /// <remarks>
    /// Die Instanz wird an mehrere Singletons injiziert - sie darf nicht
    /// ersetzt werden, sondern muss ihren Inhalt tauschen.
    /// </remarks>
    [Fact]
    public void ExpertMode_An_Erzeugt_Set_Topics()
    {
        var map = CreateMap(expertMode: false);

        var item = Catalog.FindByTopicPath("Config/Ventilation/SpeedStage3");
        Assert.NotNull(item);

        var vorher = map.SetTopics().Count;
        var werteTopic = map.ItemTopic(item!);

        Assert.False(map.ExpertModeEnabled);
        Assert.False(map.TryResolve(map.ItemSetTopic(item!), out _, out _));

        map.UpdateExpertMode(true);

        Assert.True(map.ExpertModeEnabled);

        // Die 20 Config-Topics + das Command-Topic sind jetzt schreibbar.
        var v2SetTopics = Catalog.V2
            .Where(i => i.IsWritableWithExpertMode(expertModeEnabled: true))
            .Select(i => map.ItemSetTopic(i))
            .ToList();

        Assert.Equal(21, v2SetTopics.Count);

        // Dazu kommen die 34 V1-Items, die schon im Legacy experten-pflichtig
        // waren - zusammen 55 Items mit je zwei Schreib-Topics.
        var expertenItems = Catalog.All.Count(i => i.RequiresExpertMode && i.IsWritable);
        Assert.Equal(55, expertenItems);
        Assert.Equal(vorher + expertenItems, map.SetTopics().Count);
        Assert.Equal((vorher + expertenItems) * 2, map.WriteTopics().Count);

        foreach (var topic in v2SetTopics)
        {
            Assert.Contains(topic, map.SetTopics());
            Assert.True(map.TryResolve(topic, out _, out var form));
            Assert.Equal(MqttTopicForm.Value, form);
        }

        Assert.Contains(map.ItemAsNumberSetTopic(item!), map.WriteTopics());

        // Das Lese-Topic bleibt unverändert.
        Assert.Equal(werteTopic, map.ItemTopic(item!));
        Assert.True(map.TryResolve(werteTopic, out var gefunden, out _));
        Assert.Equal(item!.Id, gefunden!.Id);
    }

    /// <summary>
    /// Der Experten-Modus lässt sich zur Laufzeit wieder ausschalten.
    /// </summary>
    [Fact]
    public void ExpertMode_Aus_Entfernt_Set_Topics()
    {
        var map = CreateMap(expertMode: true);

        var item = Catalog.FindByTopicPath("Config/Ventilation/SpeedStage3");
        Assert.NotNull(item);

        var setTopic = map.ItemSetTopic(item!);
        var asNumberSetTopic = map.ItemAsNumberSetTopic(item!);
        var werteTopic = map.ItemTopic(item!);

        Assert.True(map.ExpertModeEnabled);
        Assert.Contains(setTopic, map.WriteTopics());

        map.UpdateExpertMode(false);

        Assert.False(map.ExpertModeEnabled);
        Assert.DoesNotContain(setTopic, map.WriteTopics());
        Assert.DoesNotContain(setTopic, map.SetTopics());
        Assert.DoesNotContain(asNumberSetTopic, map.WriteTopics());
        Assert.False(map.TryResolve(setTopic, out _, out _));

        // Die Werte-Topics bleiben - Lesen ist immer erlaubt.
        Assert.Equal(werteTopic, map.ItemTopic(item!));
        Assert.True(map.TryResolve(werteTopic, out _, out _));
        Assert.True(map.TryResolve(map.ItemAsNumberTopic(item!), out _, out var form));
        Assert.Equal(MqttTopicForm.AsNumber, form);
    }

    /// <summary>
    /// Ein Aufruf mit dem bereits gültigen Modus baut nichts neu.
    /// </summary>
    [Fact]
    public void UpdateExpertMode_Mit_Dem_Selben_Wert_Aendert_Nichts()
    {
        var map = CreateMap(expertMode: false);

        var topics = map.WriteTopics();
        var bindingsVorher = topics.Count;

        map.UpdateExpertMode(false);

        Assert.Equal(bindingsVorher, map.WriteTopics().Count);
        Assert.Same(topics, map.WriteTopics());
    }

    private static string TopicPathOf(FixtureItem item)
        => item.SubscribeTopic.StartsWith(BaseTopic + "/", StringComparison.Ordinal)
            ? item.SubscribeTopic[(BaseTopic.Length + 1)..]
            : item.SubscribeTopic;

    private static FixtureItem[] LoadFixture()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "topics.json");
        Assert.True(File.Exists(path), $"Die Topic-Fixture fehlt: {path}");

        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<FixtureItem[]>(json, JsonOptions)
            ?? throw new InvalidOperationException("Die Topic-Fixture ist leer.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Ein Eintrag der Topic-Fixture.
    /// </summary>
    private sealed record FixtureItem
    {
        [JsonPropertyName("SubscribeTopic")]
        public string SubscribeTopic { get; init; } = string.Empty;

        [JsonPropertyName("PublishTopic")]
        public string? PublishTopic { get; init; }
    }
}
