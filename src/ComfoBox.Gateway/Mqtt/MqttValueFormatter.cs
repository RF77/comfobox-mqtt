using System.Globalization;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Formatiert die Werte für die MQTT-Topics.
/// </summary>
/// <remarks>
/// <para>
/// Zahlen sind **immer** kultur-invariant (<c>21.5</c>). Enums werden als
/// **Name** veröffentlicht - der Name ist Teil des MQTT-Vertrags und darf
/// nicht übersetzt werden.
/// </para>
/// </remarks>
public static class MqttValueFormatter
{
    /// <summary>
    /// Die Anzeigenamen der Topic-Gruppen (Präfix → Bezeichnung).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Nur Gruppen mit einem Eintrag bekommen eine Vorsilbe - die V1-Gruppen
    /// bleiben unverändert. Bei mehreren Treffern gewinnt der **längste**
    /// Präfix; ein Präfix gilt nur an einer Segmentgrenze
    /// (<c>Status/Zone</c> trifft <c>Status/Zone/…</c>, aber nicht
    /// <c>Status/Zoner</c>).
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> GroupLabels =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["Status/Zone"] = "Diagnose Zone",
            ["Status/WarmWater"] = "Diagnose Warmwasser",
            ["Status/HeatPump"] = "Kältekreis",
            ["Status/Buffer"] = "Pufferspeicher",
            ["Status/Energy"] = "Energie",
            ["Status/Counters"] = "Zähler",
            ["Status/Solar"] = "Solar",
            ["Config/Warmwater"] = "Warmwasser",
            ["Config/Cooling"] = "Kühlung",
            ["Config/Ventilation"] = "Lüftung",
            ["Config/Service"] = "Service",
            ["Command"] = "Befehl",
        };

    /// <summary>
    /// Die Payload des Wert-Topics.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="value">Der Wert (oder <c>null</c>).</param>
    /// <returns>Die Payload (z. B. <c>21.5</c> oder <c>Normal</c>).</returns>
    public static string StatePayload(ItemDefinition item, ItemValue? value)
    {
        ArgumentNullException.ThrowIfNull(item);

        return value switch
        {
            AnalogValue analog => MqttPayload.Format(analog.Value),
            EnumValue enumValue => enumValue.ToInvariantString(),
            _ => string.Empty,
        };
    }

    /// <summary>
    /// Die Payload des Zahl-Topics (<c>AsNumber</c>).
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="value">Der Wert (oder <c>null</c>).</param>
    /// <returns>Die Payload (z. B. <c>6</c>) oder <c>null</c>, wenn das Item kein Enum ist.</returns>
    public static string? NumberPayload(ItemDefinition item, ItemValue? value)
    {
        ArgumentNullException.ThrowIfNull(item);

        return value is EnumValue enumValue && enumValue.Value is int number
            ? number.ToString(CultureInfo.InvariantCulture)
            : null;
    }

    /// <summary>
    /// Der Anzeigename eines Items für die Discovery.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns>
    /// Die erste Zeile der Beschreibung, wenn sie kurz genug ist - sonst der
    /// Topic-Name. Lange Fließtexte sind als Entitätsname unbrauchbar.
    /// </returns>
    public static string DisplayName(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var name = BaseDisplayName(item);
        var label = GroupLabelFor(item.Group);

        return label is null ? name : $"{label}: {name}";
    }

    /// <summary>
    /// Der Anzeigename ohne Gruppen-Vorsilbe.
    /// </summary>
    private static string BaseDisplayName(ItemDefinition item)
    {
        var description = item.Description?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            return item.Name;
        }

        var line = description.Split('\n')[0].Trim();
        return line.Length <= 48 ? line : item.Name;
    }

    /// <summary>
    /// Die Bezeichnung der Gruppe (oder <c>null</c>).
    /// </summary>
    /// <remarks>
    /// Bei mehreren Präfix-Treffern gewinnt der längste - so kann später eine
    /// Untergruppe (<c>Status/Energy/…</c>) einen eigenen Namen bekommen,
    /// ohne die übergeordnete Gruppe zu ändern.
    /// </remarks>
    private static string? GroupLabelFor(string group)
    {
        string? label = null;
        var longest = -1;

        foreach (var (prefix, candidate) in GroupLabels)
        {
            if (prefix.Length <= longest || group.Length < prefix.Length)
            {
                continue;
            }

            if (!group.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (group.Length > prefix.Length && group[prefix.Length] != '/')
            {
                continue;
            }

            label = candidate;
            longest = prefix.Length;
        }

        return label;
    }
}
