using ComfoBox.Domain.Items;

namespace ComfoBox.Domain.Tests;

/// <summary>
/// Tests für den Item-Katalog.
/// </summary>
/// <remarks>
/// Der Katalog wird aus dem Legacy-Quelltext erzeugt
/// (<c>tools/generate-topics/generate-topics.mjs</c>). Die Tests sichern ab,
/// dass die Metadaten vollständig und korrekt übernommen wurden.
/// </remarks>
public sealed class CatalogTests
{
    private static readonly ItemCatalog Catalog = new();

    [Fact]
    public void Catalog_Umfasst_Exakt_127_Items()
    {
        Assert.Equal(127, Catalog.All.Count);
    }

    [Fact]
    public void Jedes_Item_Hat_Eine_Bacnet_Objekt_ID()
    {
        foreach (var item in Catalog.All)
        {
            Assert.True(item.BacnetObjectId > 0, $"{item.Id} hat keine gültige BACnet-Objekt-ID.");
        }
    }

    [Fact]
    public void Jedes_Item_Hat_Id_Gruppe_Und_Name()
    {
        foreach (var item in Catalog.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(item.Id), "Ein Item hat keine Id.");
            Assert.False(string.IsNullOrWhiteSpace(item.Group), $"{item.Id} hat keine Gruppe.");
            Assert.False(string.IsNullOrWhiteSpace(item.Name), $"{item.Id} hat keinen Namen.");
            Assert.Equal($"{item.Group}/{item.Name}", item.TopicPath);
        }
    }

    [Fact]
    public void Ids_Und_Topic_Pfade_Sind_Eindeutig()
    {
        var ids = Catalog.All.Select(i => i.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        var paths = Catalog.All.Select(i => i.TopicPath).ToList();
        Assert.Equal(paths.Count, paths.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Item_Laesst_Sich_Ueber_Id_Und_Topic_Pfad_Finden()
    {
        var item = Catalog.Find("zone.set_point_normal");

        Assert.NotNull(item);
        Assert.Equal(62, item!.BacnetObjectId);
        Assert.Equal("Zone", item.Group);
        Assert.Equal("SetPointNormal", item.Name);
        Assert.Same(item, Catalog.FindByTopicPath("Zone/SetPointNormal"));
    }

    [Fact]
    public void Unbekanntes_Item_Wird_Nicht_GEfunden()
    {
        Assert.Null(Catalog.Find("gibt.es.nicht"));
        Assert.Null(Catalog.FindByTopicPath("Zone/GibtEsNicht"));
    }

    [Fact]
    public void Die_Grad_Einheit_Wird_Korrekt_Angegeben()
    {
        // Befund A5: im Altbestand steht die Einheit als "\u00b0C".
        var item = Catalog.Find("zone.set_point_normal");

        Assert.NotNull(item);
        Assert.Equal("°C", item!.Unit);
        Assert.Equal(5f, item.Min);
        Assert.Equal(30f, item.Max);
    }

    [Fact]
    public void Prioritaeten_Werden_Ausgewertet()
    {
        // Befund A15: im Altbestand wurden die Attribute gelesen und verworfen -
        // alle Items bekamen RefreshPriority.None.
        var prios = Catalog.All.GroupBy(i => i.Priority).ToDictionary(g => g.Key, g => g.Count());

        Assert.True(prios[RefreshPriority.Primary] > 0, "Es gibt keine primären Items.");
        Assert.True(prios[RefreshPriority.Secondary] > 0, "Es gibt keine sekundären Items.");
        Assert.True(prios[RefreshPriority.Once] > 0, "Es gibt keine einmaligen Items.");
        Assert.True(prios[RefreshPriority.Primary] + prios[RefreshPriority.Secondary] < Catalog.All.Count,
            "Mindestens ein Item trägt keine Priorität - das ist der A15-Fehler.");
    }

    [Theory]
    [InlineData("zone.operation_mode", RefreshPriority.Primary)]
    [InlineData("zone.party_time", RefreshPriority.Secondary)]
    [InlineData("zone.set_point_normal", RefreshPriority.Primary)]
    [InlineData("states.controller.controller_type", RefreshPriority.Once)]
    [InlineData("states.heat_pump.cooling_flow_temperature", RefreshPriority.Primary)]
    [InlineData("states.counters.heat_pump_operating_hours", RefreshPriority.Secondary)]
    public void Die_Prioritaet_Eines_Items_Stimmt_Mit_Dem_Altbestand_Ueberein(string id, RefreshPriority expected)
    {
        var item = Catalog.Find(id);

        Assert.NotNull(item);
        Assert.Equal(expected, item!.Priority);
    }

    [Fact]
    public void Enum_Items_Haben_Die_Exakten_Enum_Namen()
    {
        var operationMode = Catalog.Find("zone.operation_mode");
        Assert.NotNull(operationMode);
        Assert.Equal(ItemValueKind.Enum, operationMode!.Kind);
        Assert.Equal(
            new Dictionary<int, string>
            {
                [1] = "Manual",
                [2] = "Standby",
                [3] = "OnlyWarmwater",
                [4] = "NormalAndFrost",
                [5] = "NormalAndReduced",
                [6] = "Normal",
                [7] = "Reduced",
            },
            operationMode.EnumValues);

        var heatingCooling = Catalog.Find("zone.heating_cooling_mode");
        Assert.NotNull(heatingCooling);
        Assert.Equal(
            new Dictionary<int, string>
            {
                [1] = "Auto",
                [2] = "OnlyCooling",
                [3] = "OnlyHeating",
                [5] = "Neutral",
            },
            heatingCooling!.EnumValues);
    }

    [Fact]
    public void Enum_Namen_Mit_Umlaut_Und_Luecken_Werden_Korrekt_UEbernommen()
    {
        var status = Catalog.Find("states.heat_pump.heat_pump_status");
        Assert.NotNull(status);
        Assert.Equal(ItemValueKind.Enum, status!.Kind);

        // Die Werte 7 und 8 gibt es nicht - die Lücke muss erhalten bleiben.
        Assert.False(status.EnumValues.ContainsKey(7));
        Assert.False(status.EnumValues.ContainsKey(8));
        Assert.Equal("Wärmequellenstoerung", status.EnumValues[14]);
        Assert.Equal("VerdampferfuehlerDefekt", status.EnumValues[62]);
    }

    [Fact]
    public void Items_Mit_Experten_Modus_Sind_Nur_Mit_Experten_Modus_Schreibbar()
    {
        var expertenItems = Catalog.All.Where(i => i.RequiresExpertMode).ToList();

        Assert.NotEmpty(expertenItems);
        foreach (var item in expertenItems)
        {
            Assert.False(item.IsReadOnly, $"{item.Id} ist Expert-Mode-pflichtig, aber fest read-only.");
            Assert.True(item.IsWritable, $"{item.Id} ist nicht grundsätzlich schreibbar.");
            Assert.False(item.IsWritableWithExpertMode(expertModeEnabled: false),
                $"{item.Id} ist ohne Expert-Modus schreibbar.");
            Assert.True(item.IsWritableWithExpertMode(expertModeEnabled: true),
                $"{item.Id} ist mit Expert-Modus nicht schreibbar.");
        }
    }

    [Fact]
    public void ReadOnly_Items_Sind_Nie_Schreibbar()
    {
        var readOnly = Catalog.All.Where(i => i.IsReadOnly).ToList();

        Assert.NotEmpty(readOnly);
        foreach (var item in readOnly)
        {
            Assert.False(item.IsWritable);
            Assert.False(item.IsWritableWithExpertMode(expertModeEnabled: true));
        }
    }

    [Fact]
    public void Grenzen_Sind_Konsistent()
    {
        foreach (var item in Catalog.All.Where(i => i.Min is not null || i.Max is not null))
        {
            Assert.True(item.Min is null || item.Max is null || item.Min <= item.Max,
                $"{item.Id} hat Min > Max.");
        }
    }

    [Fact]
    public void Wert_Ausserhalb_Der_Grenzen_Wird_Abgewiesen()
    {
        var item = Catalog.Find("zone.set_point_normal");
        Assert.NotNull(item);

        Assert.True(item!.IsInRange(21.5f));
        Assert.True(item.IsInRange(5f));
        Assert.True(item.IsInRange(30f));
        Assert.False(item.IsInRange(4.9f));
        Assert.False(item.IsInRange(30.1f));
    }

    [Fact]
    public void Enum_Name_Lasst_Sich_Zum_Zahlenwert_aufloesen()
    {
        var item = Catalog.Find("zone.operation_mode");
        Assert.NotNull(item);

        Assert.True(item!.TryResolveEnumValue("Normal", out var value));
        Assert.Equal(6, value);
        Assert.False(item.TryResolveEnumValue("GibtEsNicht", out _));
    }

    [Fact]
    public void Es_Gibt_Keine_DateValue_Und_Keine_CommandValue()
    {
        // Befund A20: die beiden Legacy-Werttypen sind tot und werden nicht
        // wiederbelebt.
        var verboten = new[] { "DateValue", "CommandValue" };

        var typen = typeof(ItemDefinition).Assembly.GetTypes()
            .Where(t => verboten.Contains(t.Name))
            .Select(t => t.FullName)
            .ToList();

        Assert.Empty(typen);
    }

    [Fact]
    public void Die_Domain_Hat_Keine_Externen_Abhaengigkeiten()
    {
        // Layerregel: die Domäne ist framework-frei.
        var erlaubt = new[] { "System", "ComfoBox.Domain" };

        var fremde = typeof(ItemDefinition).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !erlaubt.Any(e => n.StartsWith(e, StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(fremde);
    }
}
