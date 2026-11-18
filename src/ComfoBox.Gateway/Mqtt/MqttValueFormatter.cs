using System.Globalization;
using System.Text.RegularExpressions;
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
public static partial class MqttValueFormatter
{
    /// <summary>
    /// Die größte Länge eines Anzeigenamens (in Zeichen).
    /// </summary>
    /// <remarks>
    /// Längere Texte sind als Entitätsname unbrauchbar - Home Assistant baut
    /// daraus die Entitäts-Id.
    /// </remarks>
    private const int MaxNameLength = 48;

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
    /// Der Titel aus der Beschreibung (siehe <see cref="BaseDisplayName"/>),
    /// bei V2-Gruppen mit der Gruppen-Bezeichnung davor.
    /// </returns>
    /// <remarks>
    /// Die Gruppen-Vorsilbe entfällt, wenn der Name dadurch zu lang würde -
    /// Home Assistant bildet aus dem Namen die Entitäts-Id.
    /// </remarks>
    public static string DisplayName(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        var name = BaseDisplayName(item);
        var label = GroupLabelFor(item.Group);

        if (label is null)
        {
            return name;
        }

        var combined = $"{label}: {name}";
        return combined.Length <= MaxNameLength ? combined : name;
    }

    /// <summary>
    /// Der Anzeigename ohne Gruppen-Vorsilbe.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Viele Beschreibungen im Katalog haben die Form
    /// <c>Titel: Erklärung</c> - z. B.
    /// <c>Kühlfunktion: 0 = gesperrt, 1 = freigegeben</c>. Als Name zählt nur
    /// der <b>Titel</b>, denn der Rest ist eine Erklärung und gehört in den
    /// Tooltip: Aus einem Namen wird in Home Assistant die Entitäts-Id, und
    /// „… Kühlfunktion 0 gesperrt 1 freigegeben" ist als Id unbrauchbar.
    /// </para>
    /// <para>
    /// Ist der Titel zu lang, dient der Topic-Name als Ausweg.
    /// </para>
    /// </remarks>
    private static string BaseDisplayName(ItemDefinition item)
    {
        var description = item.Description?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            return item.Name;
        }

        var line = description.Split('\n')[0].Trim();
        var title = TitleOf(line);

        return title.Length <= MaxNameLength ? title : item.Name;
    }

    /// <summary>
    /// Der Titel einer Beschreibungszeile („Titel: Erklärung" → „Titel").
    /// </summary>
    /// <param name="line">Die erste Zeile der Beschreibung.</param>
    /// <returns>Der Titel - oder die unveränderte Zeile, wenn sie keinen hat.</returns>
    /// <remarks>
    /// Eine Werteliste in Klammern ist eine Erklärung, kein Namensteil:
    /// <c>Freigabe der Kühlung (0 bis 6)</c> → <c>Freigabe der Kühlung</c>.
    /// Ein Parenthese-Wert wie <c>(20 °C)</c> bleibt stehen - er
    /// unterscheidet zwei Items voneinander.
    /// </remarks>
    private static string TitleOf(string line)
    {
        var colon = line.IndexOf(':', StringComparison.Ordinal);
        var title = colon > 0 ? line[..colon].Trim() : line;

        if (title.Length == 0)
        {
            return line;
        }

        var withoutValues = ValueListSuffix().Replace(title, string.Empty).Trim();

        return withoutValues.Length == 0 ? line : withoutValues;
    }

    /// <summary>
    /// Eine Werteliste in Klammern am Ende eines Namens („(0, 1 oder 2)").
    /// </summary>
    /// <remarks>
    /// Absichtlich streng: nur Zahlen, getrennt durch Komma oder Schrägstrich,
    /// optional mit <c>oder</c>/<c>bis</c>/<c>und</c>. <c>(20 °C)</c> oder
    /// <c>(dT)</c> passen nicht und bleiben erhalten.
    /// </remarks>
    [GeneratedRegex(@"\s*\(\s*\d+(\s*[,/]\s*\d+)*(\s*(?:oder|bis|und)\s*\d+)?\s*\)$", RegexOptions.CultureInvariant)]
    private static partial Regex ValueListSuffix();

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
