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
    /// <remarks>
    /// Enthält <b>nicht</b> den vollen Beschreibungstext: Lautet die
    /// Beschreibung „Titel: Erklärung", ist der Name nur der Titel
    /// (z. B. <c>Kühlfunktion</c>). Home Assistant bildet daraus die
    /// Entitäts-Id.
    /// </remarks>
    public required string DisplayName { get; init; }

    /// <summary>
    /// Die vollständige Beschreibung aus dem Item-Katalog (oder <c>null</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="DisplayName"/> ist der Titel der ersten Zeile; die
    /// Beschreibung trägt den vollständigen Text für Tooltips.
    /// </remarks>
    public string? Description { get; init; }

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
    /// Der Grund, warum das Item keinen Wert liefert (oder <c>null</c>).
    /// </summary>
    /// <remarks>
    /// <c>not_installed</c> = die Baugruppe fehlt am Regler (Typ-Parameter),
    /// <c>no_sensor</c> = der Regler meldet den Sentinel <c>32767</c>,
    /// <c>unknown</c> = der Typ-Parameter wurde noch nicht gelesen.
    /// </remarks>
    public string? UnavailableReason { get; init; }

    /// <summary>
    /// Die Enum-Werte (Wert → Name) - nur bei Enum-Items.
    /// </summary>
    public IReadOnlyDictionary<int, string> EnumValues { get; init; } =
        new Dictionary<int, string>();

    /// <summary>
    /// Die Bedeutung des Werts: <c>measurement</c>, <c>setpoint</c>, <c>flag</c>,
    /// <c>code</c>, <c>choice</c>, <c>counter</c>, <c>info</c>, <c>command</c>
    /// oder <c>date</c>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="Kind"/> sagt nur, **wie** der Wert kodiert ist - <c>role</c>
    /// sagt, **was** er bedeutet. Die Home-Assistant-Integration wählt daraus
    /// die Plattform (Sensor, Schalter, Auswahl, Zahl oder Taster) und
    /// entscheidet, ob der Wert eine Langzeitstatistik erzeugen darf.
    /// </para>
    /// <para>
    /// Abgeleitet aus dem Katalog über <see cref="ItemSemanticsRules.Of"/>.
    /// </para>
    /// </remarks>
    public required string Role { get; init; }

    /// <summary>
    /// Die Lese-Priorität: <c>primary</c> (jeder Zyklus), <c>secondary</c>,
    /// <c>slow</c> (stündlich), <c>once</c> oder <c>none</c>.
    /// </summary>
    /// <remarks>
    /// Sie bestimmt, wie alt ein Wert im schlechtesten Fall ist. Die
    /// Integration nutzt sie, um selten gelesene Werte standardmäßig
    /// ausgeschaltet anzulegen.
    /// </remarks>
    public required string Priority { get; init; }

    /// <summary>
    /// Die erlaubten Werte eines Auswahl-Parameters (oder <c>null</c>).
    /// </summary>
    /// <remarks>
    /// <see cref="Min"/> und <see cref="Max"/> sind ein **Bereich**, keine
    /// Werteliste: <c>Cooling</c> erlaubt 0…3, das Gerät kennt aber nur 0, 1
    /// und 3. Ein Schreibvorgang mit 2 wird vom Gateway abgelehnt.
    /// </remarks>
    public IReadOnlyList<float>? AllowedValues { get; init; }

    /// <summary>
    /// Die Beschriftungen der erlaubten Werte (Zahl → Text).
    /// </summary>
    /// <remarks>
    /// Reiner Anzeigetext aus der Katalog-Beschreibung („Leichte Bauweise" für
    /// 1) - <b>kein</b> Vertrag und nur bei <c>role = code</c> gefüllt. Fehlt
    /// eine Beschriftung, bleibt es bei der Zahl.
    /// </remarks>
    public IReadOnlyDictionary<int, string> ValueLabels { get; init; } =
        new Dictionary<int, string>();

    /// <summary>
    /// Erzeugt die Beschreibung eines Items.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="cached">Der gecachte Wert (oder <c>null</c>).</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="expertModeEnabled">Ist der Experten-Modus aktiv?</param>
    /// <param name="unavailableReason">Der Grund für einen fehlenden Wert (oder <c>null</c>).</param>
    /// <returns>Die Beschreibung.</returns>
    public static ItemDto From(
        ItemDefinition item,
        CachedValue? cached,
        MqttTopicMap topics,
        bool expertModeEnabled,
        string? unavailableReason = null)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(topics);

        var writable = item.IsWritableWithExpertMode(expertModeEnabled);
        var role = ItemSemanticsRules.Of(item);

        return new ItemDto
        {
            Id = item.Id,
            Group = item.Group,
            Name = item.Name,
            DisplayName = MqttValueFormatter.DisplayName(item),
            Description = item.Description,
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
            UnavailableReason = unavailableReason,
            EnumValues = item.EnumValues,
            Role = RoleOf(role),
            Priority = PriorityOf(item.Priority),
            AllowedValues = item.AllowedValues,
            ValueLabels = LabelsOf(item, role),
        };
    }

    /// <summary>
    /// Der Name der Rolle für den REST-Vertrag.
    /// </summary>
    private static string RoleOf(ItemSemantics semantics) => semantics switch
    {
        ItemSemantics.Measurement => "measurement",
        ItemSemantics.Setpoint => "setpoint",
        ItemSemantics.Flag => "flag",
        ItemSemantics.Code => "code",
        ItemSemantics.Choice => "choice",
        ItemSemantics.Counter => "counter",
        ItemSemantics.Command => "command",
        ItemSemantics.Date => "date",
        _ => "info",
    };

    /// <summary>
    /// Der Name der Lese-Priorität für den REST-Vertrag.
    /// </summary>
    private static string PriorityOf(RefreshPriority priority) => priority switch
    {
        RefreshPriority.Primary => "primary",
        RefreshPriority.Slow => "slow",
        RefreshPriority.Once => "once",
        RefreshPriority.None => "none",
        _ => "secondary",
    };

    /// <summary>
    /// Die Beschriftungen der erlaubten Werte - nur für Kodewerte.
    /// </summary>
    /// <remarks>
    /// Bei einem Enum ist der Name im <c>enumValues</c>-Objekt bereits der
    /// Vertrag; bei allen anderen Rollen gibt es nichts zu beschriften.
    /// Treffer außerhalb der erlaubten Werte werden verworfen (Schreibschutz
    /// bleibt die Wahrheit).
    /// </remarks>
    private static IReadOnlyDictionary<int, string> LabelsOf(
        ItemDefinition item,
        ItemSemantics role)
    {
        if (role != ItemSemantics.Code)
        {
            return new Dictionary<int, string>();
        }

        var labels = ItemValueLabels.FromDescription(item.Description);
        if (labels.Count == 0)
        {
            return labels;
        }

        var allowed = AllowedNumbers(item);
        if (allowed is null)
        {
            return labels;
        }

        return labels
            .Where(entry => allowed.Contains(entry.Key))
            .ToDictionary(entry => entry.Key, entry => entry.Value);
    }

    /// <summary>
    /// Die Zahlenwerte, die das Item überhaupt annehmen darf (oder <c>null</c>).
    /// </summary>
    private static HashSet<int>? AllowedNumbers(ItemDefinition item)
    {
        if (item.AllowedValues is { Count: > 0 } values)
        {
            return [.. values.Select(value => (int)value)];
        }

        if (item.Min is null || item.Max is null || !ItemSemanticsRules.IsCodeRange(item))
        {
            return null;
        }

        var numbers = new HashSet<int>();
        for (var number = (int)item.Min.Value; number <= (int)item.Max.Value; number++)
        {
            numbers.Add(number);
        }

        return numbers;
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
