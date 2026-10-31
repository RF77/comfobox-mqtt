using System.Globalization;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Mqtt;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Die Beschreibung eines Items für die Oberfläche und die
/// Home-Assistant-Integration.
/// </summary>
/// <remarks>
/// <para>
/// Die Feldnamen sind Teil des öffentlichen Vertrags - die HA-Integration liest
/// sie. Sie dürfen deshalb nicht umbenannt werden.
/// </para>
/// <para>
/// <c>value</c> ist immer eine JSON-Zahl (kultur-invariant), <c>valueText</c> die
/// lokalisierte Darstellung **nur für die Anzeige**.
/// </para>
/// </remarks>
public sealed record ItemDto
{
    /// <summary>
    /// Die stabile Id des Items, z. B. <c>zone.set_point_normal</c>.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Die Topic-Gruppe, z. B. <c>Zone</c>.
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// Der Topic-Name, z. B. <c>SetPointNormal</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Der Anzeigename (aus der Beschreibung abgeleitet).
    /// </summary>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Die Art des Wertes: <c>analog</c>, <c>enum</c> oder <c>date</c>.
    /// </summary>
    public required string Kind { get; init; }

    /// <summary>
    /// Der aktuelle Wert (Zahl) oder <c>null</c>.
    /// </summary>
    public double? Value { get; init; }

    /// <summary>
    /// Der aktuelle Wert als Text (Anzeige), z. B. <c>21,5 °C</c> oder <c>Normal</c>.
    /// </summary>
    public string? ValueText { get; init; }

    /// <summary>
    /// Die Einheit, z. B. <c>°C</c>.
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Die untere Grenze für Schreibvorgänge.
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Die obere Grenze für Schreibvorgänge.
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// Ist das Item aktuell schreibbar (Expert-Modus berücksichtigt)?
    /// </summary>
    public bool Writable { get; init; }

    /// <summary>
    /// Erfordert das Item den Experten-Modus?
    /// </summary>
    public bool RequiresExpertMode { get; init; }

    /// <summary>
    /// Das MQTT-Topic des Werts (z. B. <c>ComfoBox/Zone/SetPointNormal</c>).
    /// </summary>
    public string? StateTopic { get; init; }

    /// <summary>
    /// Das MQTT-Schreib-Topic (nur bei schreibbaren Items).
    /// </summary>
    public string? SetTopic { get; init; }

    /// <summary>
    /// Der Zeitpunkt des letzten erfolgreichen Lesens.
    /// </summary>
    public DateTimeOffset? LastUpdated { get; init; }

    /// <summary>
    /// Ist der Wert veraltet (der letzte Read ist fehlgeschlagen)?
    /// </summary>
    public bool Stale { get; init; }

    /// <summary>
    /// Die Enum-Werte (Wert → Name) - nur bei Enum-Items.
    /// </summary>
    public IReadOnlyDictionary<int, string> EnumValues { get; init; } =
        new Dictionary<int, string>();

    /// <summary>
    /// Erzeugt die Beschreibung eines Items.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="cached">Der gecachte Wert (oder <c>null</c>).</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="expertModeEnabled">Ist der Experten-Modus aktiv?</param>
    /// <returns>Die Beschreibung.</returns>
    public static ItemDto From(
        ItemDefinition item,
        CachedValue? cached,
        MqttTopicMap topics,
        bool expertModeEnabled)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(topics);

        var writable = item.IsWritableWithExpertMode(expertModeEnabled);

        return new ItemDto
        {
            Id = item.Id,
            Group = item.Group,
            Name = item.Name,
            DisplayName = MqttValueFormatter.DisplayName(item),
            Kind = KindOf(item.Kind),
            Value = NumericValue(cached?.Value),
            ValueText = TextValue(item, cached?.Value),
            Unit = item.Unit,
            Min = item.Min,
            Max = item.Max,
            Writable = writable,
            RequiresExpertMode = item.RequiresExpertMode,
            StateTopic = topics.ItemTopic(item),
            SetTopic = writable ? topics.ItemSetTopic(item) : null,
            LastUpdated = cached?.Timestamp,
            Stale = cached?.IsStale ?? false,
            EnumValues = item.EnumValues,
        };
    }

    /// <summary>
    /// Der Name des Werte-Kinds.
    /// </summary>
    private static string KindOf(ItemValueKind kind) => kind switch
    {
        ItemValueKind.Analog => "analog",
        ItemValueKind.Enum => "enum",
        _ => "date",
    };

    /// <summary>
    /// Der Zahlenwert des gecachten Werts.
    /// </summary>
    private static double? NumericValue(ItemValue? value) => value switch
    {
        AnalogValue analog => analog.Value,
        EnumValue enumValue => enumValue.Value,
        _ => null,
    };

    /// <summary>
    /// Die lokalisierte Darstellung des gecachten Werts.
    /// </summary>
    private static string? TextValue(ItemDefinition item, ItemValue? value) => value switch
    {
        AnalogValue analog when analog.Value is not null =>
            string.Format(CultureInfo.CurrentCulture, "{0:0.##} {1}", analog.Value, item.Unit).Trim(),
        EnumValue enumValue => enumValue.Name,
        _ => null,
    };
}
