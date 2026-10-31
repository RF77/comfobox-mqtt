namespace ComfoBox.Domain.Items;

/// <summary>
/// Der Katalog der ComfoBox-Items.
/// </summary>
/// <remarks>
/// Der Katalog ist eine reine Datenstruktur ohne Seiteneffekte - dadurch ist er
/// ohne Hardware testbar.
/// </remarks>
public sealed class ItemCatalog : IItemCatalog
{
    private readonly Dictionary<string, ItemDefinition> _byId;
    private readonly Dictionary<string, ItemDefinition> _byTopicPath;
    private readonly Dictionary<string, List<ItemDefinition>> _byGroup;

    /// <summary>
    /// Erzeugt den Katalog aus der deklarativen Item-Liste.
    /// </summary>
    /// <param name="items">
    /// Die Items. Standardmäßig <see cref="Catalog.All"/>.
    /// </param>
    public ItemCatalog(IReadOnlyList<ItemDefinition>? items = null)
    {
        All = items ?? Catalog.All;

        _byId = new Dictionary<string, ItemDefinition>(All.Count, StringComparer.Ordinal);
        _byTopicPath = new Dictionary<string, ItemDefinition>(All.Count, StringComparer.Ordinal);
        _byGroup = new Dictionary<string, List<ItemDefinition>>(StringComparer.Ordinal);

        foreach (var item in All)
        {
            _byId[item.Id] = item;
            _byTopicPath[item.TopicPath] = item;

            if (!_byGroup.TryGetValue(item.Group, out var group))
            {
                group = [];
                _byGroup[item.Group] = group;
            }

            group.Add(item);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<ItemDefinition> All { get; }

    /// <inheritdoc />
    public ItemDefinition? Find(string id)
        => id is not null && _byId.TryGetValue(id, out var item) ? item : null;

    /// <inheritdoc />
    public ItemDefinition? FindByTopicPath(string topicPath)
        => topicPath is not null && _byTopicPath.TryGetValue(topicPath, out var item) ? item : null;

    /// <inheritdoc />
    public IReadOnlyList<ItemDefinition> ByGroup(string group)
        => group is not null && _byGroup.TryGetValue(group, out var items) ? items : [];
}
