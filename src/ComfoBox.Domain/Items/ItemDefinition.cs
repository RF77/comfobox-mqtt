namespace ComfoBox.Domain.Items;

/// <summary>
/// Die Definition eines Items der ComfoBox.
/// </summary>
/// <remarks>
/// Ein Item beschreibt genau eine BACnet-Objekt-ID mit Typ, Grenzen, Einheit
/// und Schreibbarkeit. Aus ihm werden Topic, REST-Route und HA-Discovery
/// abgeleitet.
/// </remarks>
public sealed record ItemDefinition
{
    private static readonly IReadOnlyDictionary<int, string> NoEnumValues = new Dictionary<int, string>();

    /// <summary>
    /// Die stabile Id des Items, z. B. <c>zone.setpoint_normal</c>.
    /// </summary>
    /// <remarks>
    /// Die Id ist unabhängig vom konfigurierbaren <c>BaseTopic</c> und dient
    /// als Schlüssel in REST-Routen.
    /// </remarks>
    public required string Id { get; init; }

    /// <summary>
    /// Die Topic-Gruppe, z. B. <c>Zone</c> oder <c>States/Controller</c>.
    /// </summary>
    public required string Group { get; init; }

    /// <summary>
    /// Der Topic-Name, z. B. <c>SetPointNormal</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Die Instanz-Nummer des BACnet-Objekts (alle Items sind Analog-Values).
    /// </summary>
    public required int BacnetObjectId { get; init; }

    /// <summary>
    /// Der Werte-Kind des Items.
    /// </summary>
    public required ItemValueKind Kind { get; init; }

    /// <summary>
    /// Die untere Grenze für Schreibvorgänge (oder <c>null</c>).
    /// </summary>
    public float? Min { get; init; }

    /// <summary>
    /// Die obere Grenze für Schreibvorgänge (oder <c>null</c>).
    /// </summary>
    public float? Max { get; init; }

    /// <summary>
    /// Die Einheit, z. B. <c>°C</c> (oder <c>null</c>).
    /// </summary>
    public string? Unit { get; init; }

    /// <summary>
    /// Das Item kann nicht geschrieben werden.
    /// </summary>
    public bool IsReadOnly { get; init; }

    /// <summary>
    /// Schreibvorgänge erfordern den Expert-Modus.
    /// </summary>
    /// <remarks>
    /// Der Legacy-Code hat das über <c>IsReadOnly = !ExpertMode</c> abgebildet.
    /// Hier sind die beiden Fälle getrennt, damit der effektive Zustand
    /// berechenbar bleibt.
    /// </remarks>
    public bool RequiresExpertMode { get; init; }

    /// <summary>
    /// Die Lese-Priorität des Items.
    /// </summary>
    public RefreshPriority Priority { get; init; } = RefreshPriority.Secondary;

    /// <summary>
    /// Die Beschreibung des Items (aus dem Legacy-Katalog).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Die Namen der Enum-Werte (Wert → Name). Nur für <see cref="ItemValueKind.Enum"/>.
    /// </summary>
    /// <remarks>
    /// Die Namen sind Teil des MQTT-Vertrags und dürfen nicht geändert werden.
    /// </remarks>
    public IReadOnlyDictionary<int, string> EnumValues { get; init; } = NoEnumValues;

    /// <summary>
    /// Der Topic-Pfad ohne <c>BaseTopic</c>, z. B. <c>Zone/SetPointNormal</c>.
    /// </summary>
    public string TopicPath => $"{Group}/{Name}";

    /// <summary>
    /// Das Item ist grundsätzlich schreibbar (der Expert-Modus bleibt unberücksichtigt).
    /// </summary>
    public bool IsWritable => !IsReadOnly;

    /// <summary>
    /// Das Item ist mit dem angegebenen Expert-Modus-Zustand schreibbar.
    /// </summary>
    /// <param name="expertModeEnabled">Ist der Expert-Modus aktiv?</param>
    public bool IsWritableWithExpertMode(bool expertModeEnabled)
        => !IsReadOnly && (!RequiresExpertMode || expertModeEnabled);

    /// <summary>
    /// Prüft, ob ein Zahlenwert innerhalb der Grenzen liegt.
    /// </summary>
    /// <param name="value">Der zu prüfende Wert.</param>
    public bool IsInRange(float value)
        => (Min is null || value >= Min.Value) && (Max is null || value <= Max.Value);

    /// <summary>
    /// Löst einen Enum-Namen zu seinem Zahlenwert auf.
    /// </summary>
    /// <param name="name">Der Enum-Name, z. B. <c>Normal</c>.</param>
    /// <param name="value">Der gefundene Zahlenwert.</param>
    /// <returns><c>true</c>, wenn der Name bekannt ist.</returns>
    public bool TryResolveEnumValue(string name, out int value)
    {
        foreach (var entry in EnumValues)
        {
            if (string.Equals(entry.Value, name, StringComparison.Ordinal))
            {
                value = entry.Key;
                return true;
            }
        }

        value = 0;
        return false;
    }
}
