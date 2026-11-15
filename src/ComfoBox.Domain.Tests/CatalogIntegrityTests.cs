using ComfoBox.Domain.Items;

namespace ComfoBox.Domain.Tests;

/// <summary>
/// Prüfungen, die den Item-Katalog gegen stille Verschlechterungen absichern.
/// </summary>
public sealed class CatalogIntegrityTests
{
    private static readonly ItemCatalog Catalog = new();

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
    /// Nur **ein** BACnet-Objekt wird doppelt gelesen (Altbestand).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Objekt <b>864</b> gehört sowohl <c>KühlraumsollAbstStandby</c> (0 … 10 K)
    /// als auch <c>RestartDelay</c> (20 … 60 min). Beide Topics zeigen deshalb
    /// denselben Rohwert - der kann nur zu einer der beiden Grenzen passen.
    /// Genau das führt in Home Assistant zu „value is outside of the range".
    /// </para>
    /// <para>
    /// Der Test ist eine <b>Wache</b>: kommt eine weitere Doppelbelegung dazu,
    /// fällt sie hier auf und nicht erst beim Nutzer als Warnung im Protokoll.
    /// </para>
    /// </remarks>
    [Fact]
    public void Nur_Das_Bekannte_Objekt_Wird_Doppelt_Gelesen()
    {
        var shared = Catalog.All
            .GroupBy(item => item.BacnetObjectId)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .Order()
            .ToList();

        Assert.Equal([864], shared);
    }

    /// <summary>
    /// Die Doppelbelegung betrifft genau die beiden bekannten Items.
    /// </summary>
    [Fact]
    public void Objekt_864_Gehoert_Zu_Standby_Und_RestartDelay()
    {
        var ids = Catalog.All
            .Where(item => item.BacnetObjectId == 864)
            .Select(item => item.Name)
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["KühlraumsollAbstStandby", "RestartDelay"], ids);
    }
}
