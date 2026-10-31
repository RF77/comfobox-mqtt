using ComfoBox.Domain.Items;

namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Der Plan, welche Items in welchem Zyklus gelesen werden.
/// </summary>
/// <remarks>
/// Die Prioritäten des Katalogs werden hier ausgewertet (Befund A15):
/// primäre Items in jedem Zyklus, sekundäre Items nur alle
/// <c>secondaryFactor</c> Zyklen. Einmalige Items liest der Dienst nur beim
/// Start.
/// </remarks>
public static class PollPlan
{
    /// <summary>
    /// Liefert die Items eines Zyklus in Lesereihenfolge.
    /// </summary>
    /// <param name="primary">Die primären Items.</param>
    /// <param name="secondary">Die sekundären Items.</param>
    /// <param name="withoutPriority">Die Items ohne Prioritäts-Attribut.</param>
    /// <param name="cycle">Die Nummer des Zyklus (0 = der erste).</param>
    /// <param name="secondaryFactor">Wie oft ein sekundäres Item gelesen wird.</param>
    /// <returns>Die Items in Lesereihenfolge.</returns>
    public static IReadOnlyList<ItemDefinition> ForCycle(
        IReadOnlyList<ItemDefinition> primary,
        IReadOnlyList<ItemDefinition> secondary,
        IReadOnlyList<ItemDefinition> withoutPriority,
        int cycle,
        int secondaryFactor)
    {
        ArgumentNullException.ThrowIfNull(primary);
        ArgumentNullException.ThrowIfNull(secondary);
        ArgumentNullException.ThrowIfNull(withoutPriority);
        ArgumentOutOfRangeException.ThrowIfNegative(cycle);
        ArgumentOutOfRangeException.ThrowIfLessThan(secondaryFactor, 1);

        var items = new List<ItemDefinition>(primary.Count + secondary.Count + withoutPriority.Count);

        // Die wichtigen Werte zuerst - der Nutzer sieht sie zuerst.
        items.AddRange(primary);
        items.AddRange(withoutPriority);

        if (cycle % secondaryFactor == 0)
        {
            items.AddRange(secondary);
        }

        return items;
    }
}
