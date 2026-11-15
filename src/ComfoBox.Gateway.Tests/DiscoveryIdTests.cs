using ComfoBox.Gateway.Mqtt;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Die Tests der ASCII-Abbildung für Discovery-Kennungen.
/// </summary>
public sealed class DiscoveryIdTests
{
    /// <summary>
    /// Umlaute werden transliteriert - nicht entfernt.
    /// </summary>
    [Theory]
    [InlineData("config.energy_comsumer.zone.gebäudeträgheit", "config_energy_comsumer_zone_gebaeudetraegheit")]
    [InlineData("ÜberhöhVorlaufEnergieerz", "UeberhoehVorlaufEnergieerz")]
    [InlineData("KühlraumsollAbstStandby", "KuehlraumsollAbstStandby")]
    [InlineData("RaumsollüberhSol", "RaumsollueberhSol")]
    [InlineData("Sommerkühlgrenze", "Sommerkuehlgrenze")]
    [InlineData("Straße", "Strasse")]
    public void Umlaute_Werden_Transliteriert(string value, string expected)
    {
        Assert.Equal(expected, DiscoveryId.Slug(value));
    }

    /// <summary>
    /// Reine ASCII-Ids bleiben inhaltlich unverändert - nur das Trennzeichen
    /// wird vereinheitlicht (Punkt und Schrägstrich → Unterstrich).
    /// </summary>
    [Theory]
    [InlineData("zone.set_point_normal", "zone_set_point_normal")]
    [InlineData("config.energy_comsumer.heat_pump.restart_delay", "config_energy_comsumer_heat_pump_restart_delay")]
    [InlineData("ComfoBox/Zone/SetPointNormal", "ComfoBox_Zone_SetPointNormal")]
    public void Ascii_Bleibt_Inhaltlich_Unveraendert(string value, string expected)
    {
        Assert.Equal(expected, DiscoveryId.Slug(value));
    }

    /// <summary>
    /// Alles, was keine Kennung sein darf, wird zu <c>_</c>.
    /// </summary>
    [Fact]
    public void Sonstige_Zeichen_Werden_Ersetzt()
    {
        Assert.Equal("a_b", DiscoveryId.Slug("a/b"));
        Assert.Equal("a_b", DiscoveryId.Slug("a b"));
        Assert.Equal("a_b", DiscoveryId.Slug("a’b"));
    }
}
