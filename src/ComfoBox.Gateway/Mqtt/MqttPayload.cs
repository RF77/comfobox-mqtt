using System.Globalization;
using ComfoBox.Domain.Values;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Die Formatierung und das Parsen der MQTT-Payloads.
/// </summary>
/// <remarks>
/// <para>
/// Zahlen sind **immer** kultur-invariant: <c>21.5</c>, niemals <c>21,5</c>.
/// Auf deutschen Systemen hat der Legacy genau daran gescheitert (Befund A5):
/// der Schreibvorgang wurde verworfen oder - schlimmer - als <c>235</c>
/// interpretiert.
/// </para>
/// <para>
/// Enum-Werte werden als **Name** veröffentlicht und als Name **oder** Zahl
/// angenommen. Ein ungültiger Wert wird abgelehnt - es wird **nie** der Wert
/// <c>0</c> geschrieben (Befund A6).
/// </para>
/// </remarks>
public static class MqttPayload
{
    /// <summary>
    /// Formatiert einen Zahlenwert kultur-invariant (z. B. <c>21.5</c>).
    /// </summary>
    /// <param name="value">Der zu formatierende Wert.</param>
    /// <returns>Der formatierte Wert.</returns>
    public static string Format(float value)
        => value.ToString("0.###", CultureInfo.InvariantCulture);

    /// <summary>
    /// Formatiert einen Zahlenwert kultur-invariant (z. B. <c>21.5</c>).
    /// </summary>
    /// <param name="value">Der zu formatierende Wert.</param>
    /// <returns>Der formatierte Wert oder ein leerer String.</returns>
    public static string Format(float? value)
        => value is null ? string.Empty : Format(value.Value);

    /// <summary>
    /// Liest einen Zahlenwert - unabhängig von der aktuellen Kultur.
    /// </summary>
    /// <param name="payload">Die Payload, z. B. <c>21.5</c>.</param>
    /// <param name="value">Der gelesene Wert.</param>
    /// <returns>
    /// <c>true</c>, wenn die Payload eine gültige Zahl ist. Ein Dezimalkomma
    /// (<c>21,5</c>) ist **ungültig** - es wird nicht als <c>215</c> gelesen.
    /// </returns>
    public static bool TryParse(string? payload, out float value)
        => AnalogValue.TryParse(payload?.Trim(), out value);

    /// <summary>
    /// Liest einen Enum-Wert: als Name (case-insensitive) oder als Zahl.
    /// </summary>
    /// <param name="payload">Die Payload, z. B. <c>Normal</c> oder <c>6</c>.</param>
    /// <param name="values">Die bekannten Enum-Werte (Wert → Name).</param>
    /// <param name="value">
    /// Der gelesene Wert oder <c>null</c>, wenn die Payload ungültig ist.
    /// </param>
    /// <returns>
    /// <c>true</c>, wenn die Payload einen bekannten Enum-Wert bezeichnet.
    /// </returns>
    /// <remarks>
    /// Bei <c>false</c> ist <paramref name="value"/> <c>null</c> - so kann ein
    /// Aufrufer **nicht** versehentlich den Wert <c>0</c> schreiben (Befund A6).
    /// </remarks>
    public static bool TryParseEnum(
        string? payload,
        IReadOnlyDictionary<int, string> values,
        out int? value)
    {
        ArgumentNullException.ThrowIfNull(values);

        value = null;

        var text = payload?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // 1) Der Name - der Vertrag des Legacy ("Normal", "HeatingWithHeatPump").
        foreach (var entry in values)
        {
            if (string.Equals(entry.Value, text, StringComparison.OrdinalIgnoreCase))
            {
                value = entry.Key;
                return true;
            }
        }

        // 2) Die Zahl - auf /AsNumber/Set der Normalfall.
        if (int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            && values.ContainsKey(number))
        {
            value = number;
            return true;
        }

        return false;
    }
}
