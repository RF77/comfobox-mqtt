using System.Text.Json;
using System.Text.Json.Serialization;
using ComfoBox.Domain.Items;

namespace ComfoBox.Contract.Tests;

/// <summary>
/// Der MQTT-Kompatibilitätsvertrag für V2: der Katalog muss deckungsgleich zur
/// Topic-Fixture <c>docs/topics-v2.json</c> sein.
/// </summary>
/// <remarks>
/// <para>
/// V2 ist rein additiv. Die Fixture wird wie die V1-Fixture vom Generator
/// (<c>tools/generate-topics/generate-topics.mjs --out-v2</c>) erzeugt und
/// enthält nur Items mit <c>IsV2 == true</c>.
/// </para>
/// <para>
/// Die Anzahl wird **nicht** fest verdrahtet: sie ergibt sich aus der Fixture.
/// Stand 2026-11-18 sind es 67 Items - 42 Status-Topics, ein Command-Topic
/// und 24 Konfigurations-Topics (davon vier rein lesende Typ-Parameter).
/// </para>
/// </remarks>
public sealed class V2TopicContractTests
{
    private const string BaseTopic = "ComfoBox";

    private static readonly FixtureItem[] Fixture = LoadFixture();

    private static readonly IReadOnlyList<ItemDefinition> V2Items = ComfoBox.Domain.Items.Catalog.V2;

    [Fact]
    public void Die_Aktuellen_Wellen_Umfassen_67_V2_Items()
    {
        // Momentaufnahme der Wellen 2a/2b/2c ohne Energiemessung: sie fällt auf,
        // wenn Fixture oder Katalog versehentlich wachsen. Der eigentliche
        // Vertrag bleibt der Abgleich Fixture ↔ Katalog weiter unten.
        Assert.Equal(67, Fixture.Length);
        Assert.Equal(67, V2Items.Count);
    }

    [Fact]
    public void V2_Fixture_Und_Katalog_Haben_Die_Gleiche_Anzahl_Items()
    {
        Assert.Equal(Fixture.Length, V2Items.Count);
    }

    [Fact]
    public void Jedes_V2_Item_Der_Fixture_Ist_Im_Katalog_Vorhanden()
    {
        var katalog = ByTopicPath();

        foreach (var item in Fixture)
        {
            var topicPath = TopicPathOf(item);
            Assert.True(katalog.ContainsKey(topicPath),
                $"Die V2-Fixture kennt {item.SubscribeTopic}, der Katalog nicht.");
        }
    }

    [Fact]
    public void Jedes_V2_Item_Des_Katalogs_Ist_In_Der_Fixture_Vorhanden()
    {
        var themen = Fixture.Select(i => i.SubscribeTopic).ToHashSet(StringComparer.Ordinal);

        foreach (var item in V2Items)
        {
            var topic = $"{BaseTopic}/{item.TopicPath}";
            Assert.True(themen.Contains(topic), $"Der Katalog kennt {topic}, die V2-Fixture nicht.");
        }
    }

    [Fact]
    public void Die_Beschreibungen_Sind_Identisch()
    {
        var katalog = ByTopicPath();

        foreach (var item in Fixture)
        {
            var catalogItem = katalog[TopicPathOf(item)];

            Assert.Equal(item.Description, catalogItem.Description);
        }
    }

    [Fact]
    public void Die_Einheiten_Sind_Identisch()
    {
        var katalog = ByTopicPath();

        foreach (var item in Fixture)
        {
            var catalogItem = katalog[TopicPathOf(item)];

            Assert.Equal(item.Unit, catalogItem.Unit);
        }
    }

    [Fact]
    public void Die_Grenzen_Sind_Identisch()
    {
        var katalog = ByTopicPath();

        foreach (var item in Fixture)
        {
            var catalogItem = katalog[TopicPathOf(item)];

            Assert.Equal(item.Min, catalogItem.Min);
            Assert.Equal(item.Max, catalogItem.Max);
        }
    }

    [Fact]
    public void Die_Schreibbarkeit_Und_Das_Set_Topic_Sind_Identisch()
    {
        var katalog = ByTopicPath();

        foreach (var item in Fixture)
        {
            var catalogItem = katalog[TopicPathOf(item)];

            // Ohne Experten-Modus gibt es kein `/Set`-Topic - für neue
            // schreibbare V2-Items gilt genau das (ADR-014).
            var writable = catalogItem.IsWritableWithExpertMode(expertModeEnabled: false);

            Assert.Equal(item.IsReadOnly, !writable);
            Assert.Equal(item.PublishTopic is not null, writable);
            Assert.Equal(item.PublishTopic, writable ? $"{BaseTopic}/{catalogItem.TopicPath}/Set" : null);
        }
    }

    [Fact]
    public void Die_Werte_Art_Stimmt_Mit_Der_Fixture_Ueberein()
    {
        var katalog = ByTopicPath();

        foreach (var item in Fixture)
        {
            var catalogItem = katalog[TopicPathOf(item)];

            Assert.Equal(KindOf(item.ValueType), catalogItem.Kind);
        }
    }

    [Fact]
    public void Enum_Items_Haben_Die_Enum_Werte_aus_Der_Fixture_Art()
    {
        var katalog = ByTopicPath();

        foreach (var item in Fixture.Where(i => i.ValueType.StartsWith("ComfoBoxLib.Items.Enums.", StringComparison.Ordinal)))
        {
            var catalogItem = katalog[TopicPathOf(item)];

            Assert.Equal(ItemValueKind.Enum, catalogItem.Kind);
            Assert.NotEmpty(catalogItem.EnumValues);
        }
    }

    private static Dictionary<string, ItemDefinition> ByTopicPath()
        => V2Items.ToDictionary(item => item.TopicPath, StringComparer.Ordinal);

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
        var path = Path.Combine(AppContext.BaseDirectory, "topics-v2.json");
        Assert.True(File.Exists(path), $"Die V2-Topic-Fixture fehlt: {path}");

        // Ein UTF-8-BOM ist optional - wie bei der V1-Fixture.
        var json = File.ReadAllText(path).TrimStart('\uFEFF');
        return JsonSerializer.Deserialize<FixtureItem[]>(json, JsonOptions)
            ?? throw new InvalidOperationException("Die V2-Topic-Fixture ist leer.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    /// <summary>
    /// Ein Eintrag der V2-Topic-Fixture.
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
