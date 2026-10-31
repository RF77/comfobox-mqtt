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
