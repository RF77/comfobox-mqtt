using ComfoBox.Domain.Items;

namespace ComfoBox.Domain.Tests;

/// <summary>
/// Tests der Item-Bedeutung und der Wert-Beschriftungen.
/// </summary>
/// <remarks>
/// Die Erwartungen sind gegen den **echten** Katalog geprüft: eine Regel, die
/// nur auf erfundenen Definitionen passt, wäre wertlos.
/// </remarks>
public sealed class ItemSemanticsTests
{
    private static readonly ItemCatalog Catalog = new();

    private static ItemDefinition Item(string id) => Catalog.All.Single(item => item.Id == id);

    /// <summary>
    /// Ein Messwert mit Einheit ist eine Messgröße.
    /// </summary>
    [Fact]
    public void Eine_Temperatur_Ist_Eine_Messgroesse()
    {
        var item = Item("states.zone.room_temperature");

        Assert.Equal(ItemSemantics.Measurement, ItemSemanticsRules.Of(item));
    }

    /// <summary>
    /// Ein schreibbarer Wert mit Einheit ist ein Sollwert.
    /// </summary>
    [Fact]
    public void Ein_Schreibbarer_Wert_Mit_Einheit_Ist_Ein_Sollwert()
    {
        var item = Item("zone.set_point_normal");

        Assert.Equal(ItemSemantics.Setpoint, ItemSemanticsRules.Of(item));
    }

    /// <summary>
    /// Ein 0/1-Bereich ist ein Schalter - nicht eine Zahl.
    /// </summary>
    [Theory]
    [InlineData("config.general.sommerknick")]
    [InlineData("config.primary_pump.max_mode")]
    [InlineData("config.general.cooling_enabled")]
    [InlineData("config.energy_comsumer.warmwater.zwangsladung")]
    public void Ein_Bereich_Von_Null_Bis_Eins_Ist_Ein_Schalter(string id)
    {
        Assert.Equal(ItemSemantics.Flag, ItemSemanticsRules.Of(Item(id)));
    }

    /// <summary>
    /// Eine hinterlegte Werteliste macht die Zahl zur Auswahl.
    /// </summary>
    /// <remarks>
    /// `Cooling` erlaubt laut Handbuch 0, 1 und 3 - der Bereich 0…3 allein wäre
    /// irreführend.
    /// </remarks>
    [Theory]
    [InlineData("config.energy_comsumer.cooling_zone.cooling")]
    [InlineData("config.primary_pump.stopped_mode")]
    [InlineData("config.energy_comsumer.warmwater.entladeschutz")]
    [InlineData("config.general.passive_cooling_hydraulics")]
    [InlineData("config.cooling.passive_mode")]
    [InlineData("config.warmwater.equipment")]
    [InlineData("config.ventilation.fan_control")]
    public void Eine_Werteliste_Ist_Ein_Kodewert(string id)
    {
        Assert.Equal(ItemSemantics.Code, ItemSemanticsRules.Of(Item(id)));
    }

    /// <summary>
    /// Ein ganzzahliger Bereich ohne Einheit ist ebenfalls ein Kodewert.
    /// </summary>
    /// <remarks>
    /// Diese Items haben im Altbestand kein <c>[Values]</c>-Attribut, ihr
    /// Bereich ist aber eine Auswahl (Gebäudeträgheit 0…3, Legionellenschutz
    /// 0…8).
    /// </remarks>
    [Theory]
    [InlineData("config.energy_comsumer.zone.gebäudeträgheit")]
    [InlineData("config.energy_comsumer.warmwater.legionellenschutzfunktion")]
    [InlineData("config.energy_comsumer.warmwater.vorrang")]
    [InlineData("config.cooling.release")]
    [InlineData("config.warmwater.power_selection")]
    public void Ein_Ganzzahliger_Bereich_Ohne_Einheit_Ist_Ein_Kodewert(string id)
    {
        Assert.Equal(ItemSemantics.Code, ItemSemanticsRules.Of(Item(id)));
    }

    /// <summary>
    /// Ein Enum bleibt ein Enum - unabhängig von der Schreibbarkeit.
    /// </summary>
    [Theory]
    [InlineData("zone.operation_mode")]
    [InlineData("states.heat_pump.heat_pump_status")]
    [InlineData("status.zone.override")]
    public void Ein_Enum_Ist_Eine_Auswahl(string id)
    {
        Assert.Equal(ItemSemantics.Choice, ItemSemanticsRules.Of(Item(id)));
    }

    /// <summary>
    /// Ein nie gelesener Schreibwert ist ein Auslöser (Taster).
    /// </summary>
    /// <remarks>
    /// `Warmwater/DoHeatWaterNow` hat **keinen** Bereich im Katalog - ohne die
    /// Prioritäts-Regel wäre es eine Number ohne Grenzen geworden.
    /// </remarks>
    [Theory]
    [InlineData("warmwater.do_heat_water_now")]
    [InlineData("command.reset_heating_curve_adaption")]
    public void Ein_Schreibwert_Ohne_Leseprioritaet_Ist_Ein_Ausloeser(string id)
    {
        Assert.Equal(ItemSemantics.Command, ItemSemanticsRules.Of(Item(id)));
    }

    /// <summary>
    /// Zähler erzeugen eine Summe, keine Momentaufnahme.
    /// </summary>
    [Theory]
    [InlineData("states.counters.heat_pump_operating_cycles")]
    [InlineData("status.counters.service_hours")]
    [InlineData("states.controller.number_of_start_ups")]
    [InlineData("states.controller.number_of_parameter_changes")]
    public void Ein_Zaehler_Ist_Ein_Zaehler(string id)
    {
        Assert.Equal(ItemSemantics.Counter, ItemSemanticsRules.Of(Item(id)));
    }

    /// <summary>
    /// Reglerangaben ohne Messgröße sind Informationen, keine Messwerte.
    /// </summary>
    /// <remarks>
    /// Eine „Messung" des Reglertyps füllte nur die Datenbank - solche Items
    /// dürfen keine Langzeitstatistik erzeugen.
    /// </remarks>
    [Theory]
    [InlineData("states.controller.controller_type")]
    [InlineData("states.controller.software_version")]
    [InlineData("status.zone.state")]
    public void Eine_Reglerangabe_Ist_Eine_Information(string id)
    {
        Assert.Equal(ItemSemantics.Info, ItemSemanticsRules.Of(Item(id)));
    }

    /// <summary>
    /// Testausgänge sind Zustände.
    /// </summary>
    [Theory]
    [InlineData("tests.outputs_inactive")]
    [InlineData("tests.r4")]
    [InlineData("states.outputs.r3_verdichter")]
    public void Ein_Testausgang_Ist_Ein_Zustand(string id)
    {
        Assert.Equal(ItemSemantics.Flag, ItemSemanticsRules.Of(Item(id)));
    }

    /// <summary>
    /// Die beiden toten Datumswerte bleiben Datumswerte.
    /// </summary>
    [Fact]
    public void Ein_Datumswert_Bleibt_Ein_Datumswert()
    {
        var dates = Catalog.All
            .Where(item => ItemSemanticsRules.Of(item) == ItemSemantics.Date)
            .Select(item => item.Id)
            .Order()
            .ToList();

        Assert.Equal(2, dates.Count);
    }

    /// <summary>
    /// Jedes Item hat genau eine Bedeutung - und keine bleibt „unbekannt".
    /// </summary>
    [Fact]
    public void Jedes_Item_Hat_Eine_Bedeutung()
    {
        foreach (var item in Catalog.All)
        {
            var semantics = ItemSemanticsRules.Of(item);

            Assert.True(Enum.IsDefined(semantics), $"{item.Id} hat keine Bedeutung.");

            // Ein Items ohne Einheit, ohne Bereich und ohne Werteliste ist
            // entweder eine Information oder ein Zähler - nie ein Sollwert.
            if (string.IsNullOrEmpty(item.Unit) && item.Min is null && item.AllowedValues is null
                && semantics == ItemSemantics.Setpoint)
            {
                Assert.Fail($"{item.Id} ist ohne Einheit als Sollwert eingeordnet.");
            }
        }
    }

    /// <summary>
    /// Kein Kodewert bleibt ohne Auswahlmöglichkeit.
    /// </summary>
    /// <remarks>
    /// Ein Kodewert ohne <see cref="ItemDefinition.AllowedValues"/> muss
    /// wenigstens einen ganzzahligen Bereich haben, sonst kennt die Oberfläche
    /// die erlaubten Werte nicht.
    /// </remarks>
    [Fact]
    public void Jeder_Kodewert_Hat_Einen_Bereich_Oder_Eine_Werteliste()
    {
        foreach (var item in Catalog.All.Where(item => ItemSemanticsRules.Of(item) == ItemSemantics.Code))
        {
            Assert.True(
                item.AllowedValues is not null || (item.Min is not null && item.Max is not null),
                $"{item.Id} ist ein Kodewert ohne Bereich und ohne Werteliste.");
        }
    }

    /// <summary>
    /// Beschriftungen werden aus der Beschreibung gelesen - in allen drei Formen.
    /// </summary>
    [Fact]
    public void Die_Drei_Beschreibungsformen_Werden_Gelesen()
    {
        // Zahl zuerst
        var sommeknick = ItemValueLabels.FromDescription("Sommerknick:\r\n* 0: deaktiviert\r\n* 1: Sommerkick täglich um 16:00");
        Assert.Equal("deaktiviert", sommeknick[0]);
        Assert.Equal("Sommerkick täglich um 16:00", sommeknick[1]);

        // Name zuerst
        var traegheit = ItemValueLabels.FromDescription(
            "Gebäudeträgheit:\r\n* Ohne Trägheit (Testzwecke) = 0\r\n* Leichte Bauweise = 1\r\n* Normale Bauweise = 2\r\n* Schwere Bauweise = 3\r\n");
        Assert.Equal("Leichte Bauweise", traegheit[1]);
        Assert.Equal("Schwere Bauweise", traegheit[3]);

        // Liste in einer Zeile
        var puffer = ItemValueLabels.FromDescription(
            "Pufferspeichertyp: 0 = keine Funktion, 1 = ein Fühler, 11 = zwei Fühler");
        Assert.Equal(3, puffer.Count);
        Assert.Equal("zwei Fühler", puffer[11]);
    }

    /// <summary>
    /// Mehrdeutige Werte fallen auf die Zahl zurück.
    /// </summary>
    [Fact]
    public void Doppelte_Werte_Mit_Anderem_Text_Werden_Verworfen()
    {
        var labels = ItemValueLabels.FromDescription("* Eins = 1\r\n* Anderer Text = 1");

        Assert.False(labels.ContainsKey(1));
    }

    /// <summary>
    /// Fließtext erzeugt keine Beschriftungen.
    /// </summary>
    [Theory]
    [InlineData("Raumsollwert normal 5.0..30.0")]
    [InlineData("Freigabe der Kühlung (0 bis 6)")]
    [InlineData("Die Funktion wird gestartet, indem die gewünschte Dauer eingestellt wird.")]
    [InlineData("")]
    [InlineData(null)]
    public void Fliesstext_Erzeugt_Keine_Beschriftungen(string? description)
    {
        Assert.Empty(ItemValueLabels.FromDescription(description));
    }

    /// <summary>
    /// Die Beschriftungen im Katalog passen zu den erlaubten Werten.
    /// </summary>
    /// <remarks>
    /// Wache gegen einen Parser, der Zahlen aus dem Fließtext zieht: jede
    /// Beschriftung muss auf einen Wert zeigen, den das Item annehmen darf.
    /// </remarks>
    [Fact]
    public void Jede_Beschriftung_Passt_Zu_Einem_Erlaubten_Wert()
    {
        var checkedLabels = 0;

        foreach (var item in Catalog.All)
        {
            if (ItemSemanticsRules.Of(item) != ItemSemantics.Code)
            {
                continue;
            }

            var labels = ItemValueLabels.FromDescription(item.Description);
            if (labels.Count == 0)
            {
                continue;
            }

            var min = (int)(item.Min ?? 0f);
            var max = (int)(item.Max ?? 0f);

            foreach (var (number, label) in labels)
            {
                checkedLabels++;

                Assert.False(string.IsNullOrWhiteSpace(label), $"{item.Id}: leere Beschriftung für {number}.");
                Assert.True(
                    item.AllowedValues is null || item.AllowedValues.Contains(number),
                    $"{item.Id}: Beschriftung {number} steht nicht in der Werteliste.");
                Assert.InRange(number, min, max);
            }
        }

        // Der Parser muss im echten Katalog etwas finden - sonst prüft der
        // Test nichts.
        Assert.True(checkedLabels >= 20, $"Nur {checkedLabels} Beschriftungen gefunden.");
    }
}
