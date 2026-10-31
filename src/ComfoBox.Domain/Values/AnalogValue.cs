using System.Globalization;

namespace ComfoBox.Domain.Values;

/// <summary>
/// Ein Zahlenwert mit Einheit und Grenzen.
/// </summary>
/// <remarks>
/// Jede Umwandlung nutzt <see cref="CultureInfo.InvariantCulture"/> - die
/// aktuelle Kultur des Systems darf das Ergebnis nie beeinflussen
/// (Befund A5: auf deutschen Systemen wurden Schreibvorgänge dadurch kaputt).
/// </remarks>
public sealed record AnalogValue(float? Value, float? Min, float? Max, string? Unit) : ItemValue
{
    /// <inheritdoc />
    public override object? Raw => Value;

    /// <summary>
    /// Formatiert den Wert kultur-invariant (z. B. <c>21.5</c>).
    /// </summary>
    /// <returns>
    /// Der formatierte Wert oder ein leerer String, wenn kein Wert vorhanden ist.
    /// </returns>
    public string ToInvariantString()
        => Value?.ToString("0.###", CultureInfo.InvariantCulture) ?? string.Empty;

    /// <summary>
    /// Liest einen Zahlenwert - unabhängig von der aktuellen Kultur.
    /// </summary>
    /// <param name="text">Der zu lesende Text, z. B. <c>21.5</c>.</param>
    /// <param name="value">Der gelesene Wert.</param>
    /// <returns>
    /// <c>true</c>, wenn der Text gültig ist. Ein Text mit Dezimalkomma
    /// (<c>23,5</c>) ist **ungültig** - er wird nicht als <c>235</c> gelesen.
    /// </returns>
    public static bool TryParse(string? text, out float value)
        => float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);

    /// <summary>
    /// Liest einen Zahlenwert - unabhängig von der aktuellen Kultur.
    /// </summary>
    /// <param name="text">Der zu lesende Text.</param>
    /// <returns>Der gelesene Wert oder <c>null</c>, wenn der Text ungültig ist.</returns>
    public static float? Parse(string? text)
        => TryParse(text, out var value) ? value : null;
}
