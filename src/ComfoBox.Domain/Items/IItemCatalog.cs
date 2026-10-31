namespace ComfoBox.Domain.Items;

/// <summary>
/// Der Katalog aller Items der ComfoBox.
/// </summary>
public interface IItemCatalog
{
    /// <summary>
    /// Alle Items in Katalog-Reihenfolge.
    /// </summary>
    IReadOnlyList<ItemDefinition> All { get; }

    /// <summary>
    /// Findet ein Item über seine Id.
    /// </summary>
    /// <param name="id">Die Id, z. B. <c>zone.setpoint_normal</c>.</param>
    /// <returns>Das Item oder <c>null</c>, wenn es unbekannt ist.</returns>
    ItemDefinition? Find(string id);

    /// <summary>
    /// Findet ein Item über seinen Topic-Pfad (ohne <c>BaseTopic</c>).
    /// </summary>
    /// <param name="topicPath">Der Pfad, z. B. <c>Zone/SetPointNormal</c>.</param>
    /// <returns>Das Item oder <c>null</c>, wenn es unbekannt ist.</returns>
    ItemDefinition? FindByTopicPath(string topicPath);

    /// <summary>
    /// Liefert alle Items einer Gruppe.
    /// </summary>
    /// <param name="group">Die Gruppe, z. B. <c>Zone</c>.</param>
    IReadOnlyList<ItemDefinition> ByGroup(string group);
}
