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

        var description = item.Description?.Trim();
        if (string.IsNullOrEmpty(description))
        {
            return item.Name;
        }

        var line = description.Split('\n')[0].Trim();
        return line.Length <= 48 ? line : item.Name;
    }
}
