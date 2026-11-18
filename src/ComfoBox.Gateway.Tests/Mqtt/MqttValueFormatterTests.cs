using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Mqtt;

namespace ComfoBox.Gateway.Tests.Mqtt;

/// <summary>
/// Die Tests der Anzeigenamen-Ableitung (<see cref="MqttValueFormatter.DisplayName"/>).
/// </summary>
/// <remarks>
/// <para>
/// Der Name wird in zwei Richtungen verwendet: als Discovery-Name der
/// MQTT-Entität und als Name der HACS-Entität - und Home Assistant bildet
/// daraus die <b>Entitäts-Id</b>.
/// </para>
/// <para>
/// <b>Regression (2026-11-17):</b> Die Entität hieß
/// <c>Kühlfunktion: 0 = gesperrt, 1 = freigegeben</c> - und damit auch
/// <c>sensor.…_kuhlfunktion_0_gesperrt_1_freigegeben</c>. Der Katalogtitel
/// lautet aber nur <c>Kühlfunktion</c>; der Rest ist eine Erklärung und
/// gehört in den Tooltip.
/// </para>
/// </remarks>
public sealed class MqttValueFormatterTests
{
    /// <summary>
    /// Der Titel vor dem Doppelpunkt ist der Name - der Rest ist Erklärung.
    /// </summary>
    [Fact]
    public void Der_Titel_Vor_Dem_Doppelpunkt_Ist_Der_Name()
    {
        var item = Item(
            "config.general.cooling_enabled",
            "Config/General",
            "CoolingEnabled",
            "Kühlfunktion: 0 = gesperrt, 1 = freigegeben");

        Assert.Equal("Kühlfunktion", MqttValueFormatter.DisplayName(item));
    }

    /// <summary>
    /// Eine Beschreibung ohne Doppelpunkt bleibt unverändert.
    /// </summary>
    [Fact]
    public void Eine_Beschreibung_Ohne_Doppelpunkt_Bleibt_Unveraendert()
    {
        var item = Item("zone.operation_mode", "Zone", "OperationMode", "Betriebsart");

        Assert.Equal("Betriebsart", MqttValueFormatter.DisplayName(item));
    }

    /// <summary>
    /// Ein langer Fließtext ist als Name unbrauchbar - dann gilt der Topic-Name.
    /// </summary>
    [Fact]
    public void Ein_Langer_Fliesstext_Ergibt_Den_Topic_Namen()
    {
        var item = Item(
            "zone.party_time",
            "Zone",
            "PartyTime",
            "Die Funktion wird gestartet, indem die gewünschte Dauer eingestellt wird.");

        Assert.Equal("PartyTime", MqttValueFormatter.DisplayName(item));
    }

    /// <summary>
    /// Ist der Titel zu lang, gilt der Topic-Name.
    /// </summary>
    [Fact]
    public void Ein_Zu_Langer_Titel_Ergibt_Den_Topic_Namen()
    {
        var item = Item(
            "zone.test",
            "Zone",
            "Test",
            "Ein viel zu langer Titel der nicht als Name taugt: kurz");

        Assert.Equal("Test", MqttValueFormatter.DisplayName(item));
    }

    /// <summary>
    /// Ein Doppelpunkt am Zeilenanfang ist kein Titel.
    /// </summary>
    [Fact]
    public void Ein_Fuehrender_Doppelpunkt_Ergibt_Keinen_Leeren_Namen()
    {
        var item = Item("zone.test", "Zone", "Test", ": ohne Titel");

        Assert.Equal(": ohne Titel", MqttValueFormatter.DisplayName(item));
    }

    /// <summary>
    /// Eine Werteliste in Klammern ist Erklärung, nicht Namensteil.
    /// </summary>
    [Fact]
    public void Eine_Werteliste_In_Klammern_Entfaellt()
    {
        var item = Item(
            "config.cooling.release",
            "Config/Cooling",
            "Release",
            "Freigabe der Kühlung (0 bis 6): 0 = gesperrt");

        Assert.Equal("Kühlung: Freigabe der Kühlung", MqttValueFormatter.DisplayName(item));
    }

    /// <summary>
    /// Ein Parenthese-Wert, der zwei Items unterscheidet, bleibt erhalten.
    /// </summary>
    [Theory]
    [InlineData("Min Vorl'temp Kühlen (20 °C)")]
    [InlineData("Kollektor-Differenztemperatur (dT)")]
    [InlineData("Energieerz'anhebung (Fühler)")]
    public void Ein_Parenthese_Wert_Bleibt_Erhalten(string description)
    {
        var item = Item("zone.test", "Zone", "Test", description);

        Assert.Equal(description, MqttValueFormatter.DisplayName(item));
    }

    /// <summary>
    /// Der Katalogtitel der Kühlfunktion lautet nur „Kühlfunktion“.
    /// </summary>
    /// <remarks>
    /// Wächter über den echten Katalog: Wird die Beschreibung zu einem
    /// Fließtext geändert, wird der Name wieder unbrauchbar.
    /// </remarks>
    [Fact]
    public void Die_Kuehlfunktion_Heisst_Im_Katalog_Nur_Kuehlfunktion()
    {
        var item = Catalog.All.Single(entry => entry.Id == "config.general.cooling_enabled");

        Assert.Equal("Kühlfunktion", MqttValueFormatter.DisplayName(item));
    }

    /// <summary>
    /// Kein Anzeigename im Katalog ist länger als 48 Zeichen.
    /// </summary>
    /// <remarks>
    /// Der Name landet in Home Assistant in der Entitäts-Id. Ein längerer
    /// Name bedeutet dort eine unbrauchbare Id.
    /// </remarks>
    [Fact]
    public void Kein_Anzeigename_Ist_Laenger_Als_Die_Grenze()
    {
        var tooLong = Catalog.All
            .Select(MqttValueFormatter.DisplayName)
            .Where(name => name.Length > 48)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        Assert.Empty(tooLong);
    }

    private static ItemDefinition Item(string id, string group, string name, string description) => new()
    {
        Id = id,
        Group = group,
        Name = name,
        BacnetObjectId = 1,
        Kind = ItemValueKind.Analog,
        Description = description,
    };
}
