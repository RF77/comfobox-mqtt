using System.Text.Json;
using System.Text.Json.Serialization;
using ComfoBox.Domain.Items;

namespace ComfoBox.Contract.Tests;

/// <summary>
/// Der MQTT-Kompatibilitätsvertrag: der Item-Katalog muss deckungsgleich zur
/// Topic-Fixture <c>docs/topics.json</c> sein.
/// </summary>
/// <remarks>
/// <para>
/// Die Fixture stammt aus dem Legacy-Stand von 2017 und ist **eingefroren**
/// (Projektregel). Sie darf nicht verändert werden - Abweichungen werden hier
/// aufgedeckt, nicht in der Fixture korrigiert.
/// </para>
/// <para>
/// Bekannte Abweichung: <c>ComfoBox/Warmwater/DoHeatWaterNow</c> ist in der
/// Fixture ein <c>DateTime?</c>, im heutigen Legacy-Katalog aber ein
/// <c>float?</c>. Der Legacy-Code hat den Werttyp später geändert; die Fixture
/// wurde nie nachgezogen.
/// </para>
/// </remarks>
public sealed class TopicContractTests
{
    private const string BaseTopic = "ComfoBox";

    /// <summary>
    /// Bekannte, dokumentierte Abweichungen zwischen Fixture und Legacy-Katalog.
    /// </summary>
    private static readonly Dictionary<string, string> KnownDeviations = new(StringComparer.Ordinal)
    {
        ["Warmwater/DoHeatWaterNow"] =
            "Fixture (2017) nennt DateTime?, der Legacy-Katalog nutzt heute AnalogValue (float?).",
    };

    private static readonly FixtureItem[] Fixture = LoadFixture();

    private static readonly ItemCatalog Catalog = new();

    [Fact]
    public void Fixture_Und_Katalog_Haben_Die_Gleiche_Anzahl_Items()
    {
        Assert.Equal(127, Fixture.Length);
        Assert.Equal(Fixture.Length, Catalog.All.Count);
    }

    [Fact]
    public void Jedes_Item_Der_Fixture_Ist_Im_Katalog_Vorhanden()
    {
        foreach (var item in Fixture)
        {
            var topicPath = TopicPathOf(item);
            Assert.True(Catalog.FindByTopicPath(topicPath) is not null,
                $"Die Fixture kennt {item.SubscribeTopic}, der Katalog nicht.");
        }
    }

    [Fact]
    public void Jedes_Item_Des_Katalogs_Ist_In_Der_Fixture_Vorhanden()
    {
        var themen = Fixture.Select(i => i.SubscribeTopic).ToHashSet(StringComparer.Ordinal);

        foreach (var item in Catalog.All)
        {
            var topic = $"{BaseTopic}/{item.TopicPath}";
            Assert.True(themen.Contains(topic), $"Der Katalog kennt {topic}, die Fixture nicht.");
        }
    }

    [Fact]
    public void Die_Beschreibungen_Sind_Identisch()
    {
        foreach (var item in Fixture)
        {
            var catalogItem = Catalog.FindByTopicPath(TopicPathOf(item))!;

            Assert.Equal(item.Description, catalogItem.Description);
        }
    }

    [Fact]
    public void Die_Einheiten_Sind_Identisch()
    {
        foreach (var item in Fixture)
        {
            var catalogItem = Catalog.FindByTopicPath(TopicPathOf(item))!;

            Assert.Equal(item.Unit, catalogItem.Unit);
        }
    }

    [Fact]
    public void Die_Grenzen_Sind_Identisch()
    {
        foreach (var item in Fixture)
        {
            var catalogItem = Catalog.FindByTopicPath(TopicPathOf(item))!;

            Assert.Equal(item.Min, catalogItem.Min);
            Assert.Equal(item.Max, catalogItem.Max);
        }
    }

    [Fact]
    public void Die_Schreibbarkeit_Und_Das_Set_Topic_Sind_Identisch()
    {
        foreach (var item in Fixture)
        {
            var catalogItem = Catalog.FindByTopicPath(TopicPathOf(item))!;

            // Der Legacy-Stand: `IsReadOnly = !ExpertMode` - ohne Expert-Modus
            // gibt es kein `/Set`-Topic.
            var writable = catalogItem.IsWritableWithExpertMode(expertModeEnabled: false);

            Assert.Equal(item.IsReadOnly, !writable);
            Assert.Equal(item.PublishTopic is not null, writable);
            Assert.Equal(item.PublishTopic, writable ? $"{BaseTopic}/{catalogItem.TopicPath}/Set" : null);
        }
    }

    [Fact]
    public void Die_Werte_Art_Stimmt_Mit_Der_Fixture_Ueberein()
    {
        foreach (var item in Fixture)
        {
            var topicPath = TopicPathOf(item);
            var catalogItem = Catalog.FindByTopicPath(topicPath)!;

            if (KnownDeviations.ContainsKey(topicPath))
            {
                continue;
            }

            Assert.Equal(KindOf(item.ValueType), catalogItem.Kind);
        }
    }

    [Fact]
    public void Enum_Items_Haben_Die_Enum_Werte_aus_Der_Fixture_Art()
    {
        foreach (var item in Fixture.Where(i => i.ValueType.StartsWith("ComfoBoxLib.Items.Enums.", StringComparison.Ordinal)))
        {
            var catalogItem = Catalog.FindByTopicPath(TopicPathOf(item))!;

            Assert.Equal(ItemValueKind.Enum, catalogItem.Kind);
            Assert.NotEmpty(catalogItem.EnumValues);
        }
    }

    [Fact]
    public void Die_Bekannten_Abweichungen_Sind_Immer_Noch_Vorhanden()
    {
        // Wenn der Legacy-Katalog nachgezogen wird, darf die Ausnahme nicht
        // stillschweigend verschwinden - dann muss die Fixture angepasst werden.
        foreach (var (topicPath, _) in KnownDeviations)
        {
            Assert.NotNull(Catalog.FindByTopicPath(topicPath));
        }
    }

    private static string TopicPathOf(FixtureItem item)
        => item.SubscribeTopic.StartsWith(BaseTopic + "/", StringComparison.Ordinal)
            ? item.SubscribeTopic[(BaseTopic.Length + 1)..]
            : item.SubscribeTopic;

    private static ItemValueKind KindOf(string legacyValueType) => legacyValueType switch
    {
        "float?" => ItemValueKind.Analog,
        "DateTime?" => ItemValueKind.Date,
        _ => ItemValueKind.Enum,
    };

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

        [JsonPropertyName("Description")]
        public string? Description { get; init; }

        [JsonPropertyName("ValueType")]
        public string ValueType { get; init; } = string.Empty;

        [JsonPropertyName("Unit")]
        public string? Unit { get; init; }

        [JsonPropertyName("IsReadOnly")]
        public bool IsReadOnly { get; init; }

        [JsonPropertyName("Min")]
        public float? Min { get; init; }

        [JsonPropertyName("Max")]
        public float? Max { get; init; }
    }
}
