namespace ComfoBox.Domain.Items;

/// <summary>
/// Die V1/V2-Aufteilung des Item-Katalogs.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Catalog.All"/> bleibt die einzige Quelle. Diese Datei leitet
/// daraus ausschließlich die gefilterten Sichten ab – sie ist handgeschrieben,
/// weil <c>Catalog.cs</c> generiert wird.
/// </para>
/// <para>
/// V1 ist der eingefrorene Vertrag (<c>docs/topics.json</c>, 127 Items), V2
/// sind die additiven Topics (<c>docs/topics-v2.json</c>).
/// </para>
/// </remarks>
public static partial class Catalog
{
    // Die Filter liegen in Lazy-Werten: ein direkter Feld-Initialisierer
    // würde von der Reihenfolge der partiellen Deklarationen abhängen und
    // könnte vor Catalog.All laufen.
    private static readonly Lazy<IReadOnlyList<ItemDefinition>> V1Items =
        new(() => [.. All.Where(static item => !item.IsV2)]);

    private static readonly Lazy<IReadOnlyList<ItemDefinition>> V2Items =
        new(() => [.. All.Where(static item => item.IsV2)]);

    /// <summary>
    /// Die Items des eingefrorenen V1-Topic-Vertrags.
    /// </summary>
    public static IReadOnlyList<ItemDefinition> V1 => V1Items.Value;

    /// <summary>
    /// Die Items des additiven V2-Topic-Vertrags.
    /// </summary>
    public static IReadOnlyList<ItemDefinition> V2 => V2Items.Value;
}
