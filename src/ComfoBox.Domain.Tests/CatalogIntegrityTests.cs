using ComfoBox.Domain.Items;

namespace ComfoBox.Domain.Tests;

/// <summary>
/// Prüfungen, die den Item-Katalog gegen stille Verschlechterungen absichern.
/// </summary>
public sealed class CatalogIntegrityTests
{
    private static readonly ItemCatalog Catalog = new();

    /// <summary>
    /// Objekte, die von einem Test- **und** einem Zustands-Topic gelesen werden.
    /// </summary>
    /// <remarks>
    /// Es ist dasselbe Geräte-Objekt, einmal zum Setzen (Inbetriebnahme) und
    /// einmal als Istwert. Nach R6 zeigen beide Topics auf dieselbe Nummer.
    /// </remarks>
    private static readonly int[] SharedObjects = [2782, 2789, 2790];

    /// <summary>
    /// Jede Id ist eindeutig.
    /// </summary>
    [Fact]
    public void Jede_Item_Id_Ist_Eindeutig()
    {
        var ids = Catalog.All.Select(item => item.Id).ToList();

        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>
    /// Nur die **bekannten** BACnet-Objekte werden doppelt gelesen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Seit der Korrektur von <c>RestartDelay</c> auf Objekt 846 (R3) ist 864
    /// eindeutig. Dafür gibt es drei **bewusste** Doppelbelegungen: das Gerät
    /// führt Ausgänge als Test- **und** als Zustands-Topic.
    /// </para>
    /// <para>
    /// Der Test ist eine <b>Wache</b>: kommt eine weitere Doppelbelegung dazu,
    /// fällt sie hier auf und nicht erst beim Nutzer als Warnung im Protokoll.
    /// </para>
    /// </remarks>
    [Fact]
    public void Nur_Die_Bekannten_Objekte_Werden_Doppelt_Gelesen()
    {
        var shared = Catalog.All
            .GroupBy(item => item.BacnetObjectId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToList();

        Assert.Equal(SharedObjects, shared);
    }

    /// <summary>
    /// Jede Doppelbelegung besteht genau aus einem Test- und einem Zustands-Topic.
    /// </summary>
    [Fact]
    public void Jedes_Geteilte_Objekt_Hat_Ein_Test_Und_Ein_Zustands_Topic()
    {
        foreach (var objectId in SharedObjects)
        {
            var items = Catalog.All.Where(item => item.BacnetObjectId == objectId).ToList();

            Assert.Equal(2, items.Count);
            Assert.Single(items, item => item.Group == "Tests");
            Assert.Single(items, item => item.Group == "States/Outputs");
        }
    }

    /// <summary>
    /// Objekt 864 gehört nach der Korrektur nur noch dem Kühl-Offset.
    /// </summary>
    [Fact]
    public void Objekt_864_Ist_Nur_KuehlraumsollAbstStandby()
    {
        var item = Assert.Single(Catalog.All, item => item.BacnetObjectId == 864);

        Assert.Equal("KühlraumsollAbstStandby", item.Name);
    }
}
