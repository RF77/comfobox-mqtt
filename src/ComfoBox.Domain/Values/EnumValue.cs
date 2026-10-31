namespace ComfoBox.Domain.Values;

/// <summary>
/// Ein Auswahlwert aus benannten Zahlenwerten.
/// </summary>
/// <remarks>
/// Der MQTT-Vertrag veröffentlicht den **Namen** des Wertes, nicht die Zahl.
/// </remarks>
public sealed record EnumValue(int? Value, IReadOnlyDictionary<int, string> Values) : ItemValue
{
    /// <inheritdoc />
    public override object? Raw => Value;

    /// <summary>
    /// Der Name des aktuellen Wertes (oder <c>null</c>, wenn der Wert unbekannt ist).
    /// </summary>
    public string? Name
        => Value is not null && Values.TryGetValue(Value.Value, out var name) ? name : null;

    /// <summary>
    /// Formatiert den Wert als Namen (kultur-invariant).
    /// </summary>
    /// <returns>
    /// Der Name des Wertes oder ein leerer String, wenn kein Wert vorhanden ist.
    /// </returns>
    public string ToInvariantString() => Name ?? string.Empty;
}
