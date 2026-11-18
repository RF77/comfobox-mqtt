using ComfoBox.Domain.Derived;
using ComfoBox.Domain.Items;

namespace ComfoBox.Domain.Tests;

/// <summary>
/// Tests der abgeleiteten Werte: reine Formeln, ohne Cache, ohne Uhr, ohne MQTT.
/// </summary>
/// <remarks>
/// Die Tests fahren die Rechenfunktionen der Definitionen direkt an. Genau
/// dadurch ist prüfbar, dass eine fehlende Eingabe <c>null</c> ergibt - und
/// nicht die <c>0</c>, die eine falsche Aussage wäre.
/// </remarks>
public sealed class DerivedValueTests
{
    private static readonly ItemCatalog Catalog = new();

    [Fact]
    public void Ueberhitzung_Ist_Die_Differenz()
        => Assert.Equal(7f, Compute("derived.superheat", (641, 12f), (660, 5f)));

    [Fact]
    public void Heizkreis_Spreizung_Ist_Die_Differenz()
        => Assert.Equal(7f, Compute("derived.heating_circuit_delta", (600, 45f), (3468, 38f)));

    [Fact]
    public void Puffer_Delta_Ist_Die_Differenz()
        => Assert.Equal(8f, Compute("derived.buffer_delta", (24, 50f), (25, 42f)));

    [Fact]
    public void Laufzeit_Verhaeltnis_Ist_Prozent()
        => Assert.Equal(75f, Compute("derived.runtime_ratio", (30, 1000f), (31, 500f), (3060, 2000f)));

    [Fact]
    public void Laufzeit_Verhaeltnis_Ohne_Heizstunden_Ist_Unbekannt()
        => Assert.Null(Compute("derived.runtime_ratio", (30, 1000f), (31, 500f), (3060, 0f)));

    [Fact]
    public void Wartung_Ist_Bei_Gleichstand_Faellig()
        => Assert.Equal(1f, Compute("derived.service_due", (3366, 4000f), (3365, 4000f)));

    [Fact]
    public void Wartung_Ist_Vor_Erreichen_Des_Intervalls_Nicht_Faellig()
        => Assert.Equal(0f, Compute("derived.service_due", (3366, 3999f), (3365, 4000f)));

    [Theory]
    [InlineData(9f)]
    [InlineData(30f)]
    [InlineData(31f)]
    public void Kuehlung_Ist_Bei_Diesen_Status_Werten_Aktiv(float status)
        => Assert.Equal(1f, Compute("derived.cooling_active", (662, status), (3098, 0f)));

    [Fact]
    public void Kuehlung_Ist_Ohne_Kuehlvorlauf_Inaktiv()
        => Assert.Equal(0f, Compute("derived.cooling_active", (662, 5f), (3098, 0f)));

    [Fact]
    public void Kuehlung_Ist_Bei_Warmem_Kuehlvorlauf_Aktiv()
        => Assert.Equal(1f, Compute("derived.cooling_active", (662, 5f), (3098, 12.5f)));

    /// <summary>
    /// Fehlt ein Eingang, ist der Wert unbekannt - in **jeder** Definition und
    /// für **jeden** Eingang.
    /// </summary>
    [Fact]
    public void Fehlender_Eingang_Ergibt_Unbekannt()
    {
        foreach (var definition in DerivedValues.All)
        {
            var compute = definition.Compute;

            foreach (var objectId in definition.InputObjectIds)
            {
                var inputs = ValidInputs(definition);
                inputs.Remove(objectId);

                Assert.Null(compute(inputs));
            }
        }
    }

    /// <summary>
    /// Es gibt genau sechs abgeleitete Werte - und jeder ist vollständig beschrieben.
    /// </summary>
    [Fact]
    public void Es_Gibt_Sechs_Vollstaendige_Definitionen()
    {
        Assert.Equal(6, DerivedValues.All.Count);

        var ids = DerivedValues.All.Select(definition => definition.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct(StringComparer.Ordinal).Count());

        foreach (var definition in DerivedValues.All)
        {
            Assert.False(string.IsNullOrWhiteSpace(definition.Id));
            Assert.False(string.IsNullOrWhiteSpace(definition.Formula));
            Assert.NotEmpty(definition.InputObjectIds);
            Assert.Equal(
                definition.InputObjectIds.Count,
                definition.InputObjectIds.Distinct().Count());
        }
    }

    [Fact]
    public void Alle_Topic_Pfade_Liegen_Unter_Derived()
        => Assert.All(DerivedValues.All, definition => Assert.StartsWith("Derived/", definition.TopicPath));

    /// <summary>
    /// Jeder abgeleitete Wert hat eine reine Rechenfunktion (keine Historie).
    /// </summary>
    [Fact]
    public void Jeder_Wert_Hat_Eine_Rechenfunktion()
        => Assert.All(DerivedValues.All, definition => Assert.NotNull(definition.Compute));

    /// <summary>
    /// Kein abgeleiteter Wert verdeckt ein Katalog-Item (Id oder Topic-Pfad).
    /// </summary>
    [Fact]
    public void Keine_Kollision_Mit_Dem_Item_Katalog()
    {
        foreach (var definition in DerivedValues.All)
        {
            Assert.Null(Catalog.Find(definition.Id));
            Assert.Null(Catalog.FindByTopicPath(definition.TopicPath));

            // Das veröffentlichte Topic trägt den reservierten Status-Zweig.
            Assert.Null(Catalog.FindByTopicPath($"Status/{definition.TopicPath}"));
        }
    }

    /// <summary>
    /// Jeder Eingang ist ein **eindeutiges** Katalog-Objekt - sonst wäre die
    /// Auflösung über die BACnet-Objekt-Id im Gateway mehrdeutig.
    /// </summary>
    [Fact]
    public void Alle_Eingaenge_Sind_Eindeutige_Katalog_Objekte()
    {
        foreach (var definition in DerivedValues.All)
        {
            foreach (var objectId in definition.InputObjectIds)
            {
                Assert.Single(Catalog.All, item => item.BacnetObjectId == objectId);
            }
        }
    }

    /// <summary>
    /// Rechnet eine Definition mit den angegebenen Eingängen.
    /// </summary>
    private static float? Compute(string id, params (int ObjectId, float Value)[] inputs)
    {
        var definition = Assert.Single(DerivedValues.All, candidate => candidate.Id == id);
        var compute = definition.Compute;

        Assert.NotNull(compute);

        var values = new Dictionary<int, float?>(inputs.Length);
        foreach (var (objectId, value) in inputs)
        {
            values[objectId] = value;
        }

        return compute(values);
    }

    /// <summary>
    /// Plausible Werte für alle Eingänge einer Definition.
    /// </summary>
    private static Dictionary<int, float?> ValidInputs(DerivedValueDefinition definition)
    {
        var inputs = new Dictionary<int, float?>(definition.InputObjectIds.Count);

        foreach (var objectId in definition.InputObjectIds)
        {
            inputs[objectId] = SampleValue(objectId);
        }

        return inputs;
    }

    /// <summary>
    /// Ein Beispielwert je Datenpunkt.
    /// </summary>
    private static float SampleValue(int objectId) => objectId switch
    {
        641 => 12f,      // Sauggas
        660 => 5f,       // Verdampfer
        600 => 45f,      // Vorlauf
        3468 => 38f,     // Rücklauf
        24 => 50f,       // Puffer oben
        25 => 42f,       // Puffer unten
        30 => 1000f,     // Einschaltstunden
        31 => 500f,      // Stufe 2
        3060 => 2000f,   // Heizstunden
        3366 => 5000f,   // Service-Stunden
        3365 => 4000f,   // Wartungsintervall
        3349 => 1000f,   // Wärmezähler
        3355 => 100f,    // Stromzähler
        662 => 10f,      // HeatPumpStatus (HeatingWithHeatPump)
        3098 => 0f,      // Kühlvorlauf
        _ => throw new InvalidOperationException($"Kein Beispielwert für DP {objectId}."),
    };
}
