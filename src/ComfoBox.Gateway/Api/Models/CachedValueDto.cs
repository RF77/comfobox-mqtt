using ComfoBox.Domain.Values;
using ComfoBox.Gateway;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Ein gecachter Item-Wert (für den SSE-Stream).
/// </summary>
/// <remarks>
/// Der Wert wird **flach** dargestellt: <c>ItemValue</c> ist eine abstrakte
/// Basisklasse, und eine polymorphe Serialisierung würde der Oberfläche nur
/// den Basistyp liefern.
/// </remarks>
public sealed record CachedValueDto
{
    /// <summary>
    /// Die Id des Items.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Der Zahlenwert (oder <c>null</c>).
    /// </summary>
    public double? Value { get; init; }

    /// <summary>
    /// Der Enum-Name (oder <c>null</c>).
    /// </summary>
    public string? ValueName { get; init; }

    /// <summary>
    /// Die Einheit (oder <c>null</c>).
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Der Zeitpunkt des letzten erfolgreichen Lesens.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Ist der Wert veraltet?
    /// </summary>
    public required bool IsStale { get; init; }

    /// <summary>
    /// Erzeugt die Beschreibung eines gecachten Werts.
    /// </summary>
    /// <param name="cached">Der gecachte Wert.</param>
    /// <returns>Die Beschreibung.</returns>
    public static CachedValueDto From(CachedValue cached)
    {
        ArgumentNullException.ThrowIfNull(cached);

        return new CachedValueDto
        {
            Id = cached.Id,
            Value = cached.Value switch
            {
                AnalogValue analog => analog.Value,
                EnumValue enumValue => enumValue.Value,
                _ => null,
            },
            ValueName = (cached.Value as EnumValue)?.Name,
            Unit = (cached.Value as AnalogValue)?.Unit,
            Timestamp = cached.Timestamp,
            IsStale = cached.IsStale,
        };
    }
}
