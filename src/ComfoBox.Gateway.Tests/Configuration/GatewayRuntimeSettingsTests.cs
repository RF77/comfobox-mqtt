using ComfoBox.Gateway.Configuration;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für die Laufzeit-Einstellungen des Gateways.
/// </summary>
/// <remarks>
/// Der Experten-Modus ist der Auslöser für den MQTT-/Discovery-Umbau
/// (Aufgabe 10.13). Entscheidend ist, dass das Ereignis **nur bei einer echten
/// Änderung** feuert: die Oberfläche schickt beim Speichern die gesamte
/// Konfiguration, also auch unveränderte Felder.
/// </remarks>
public sealed class GatewayRuntimeSettingsTests
{
    [Fact]
    public void Der_Startwert_Wird_Uebernommen()
    {
        Assert.False(new GatewayRuntimeSettings(expertMode: false).ExpertMode);
        Assert.True(new GatewayRuntimeSettings(expertMode: true).ExpertMode);
    }

    [Fact]
    public void Ein_Wechsel_Loest_Das_Ereignis_Einmal_Aus()
    {
        var settings = new GatewayRuntimeSettings(expertMode: false);

        var aufrufe = new List<bool>();
        settings.ExpertModeChanged += (_, enabled) => aufrufe.Add(enabled);

        settings.ExpertMode = true;
        settings.ExpertMode = false;

        Assert.Equal([true, false], aufrufe);
    }

    [Fact]
    public void Derselbe_Wert_Loest_Kein_Ereignis_Aus()
    {
        var settings = new GatewayRuntimeSettings(expertMode: false);

        var aufrufe = 0;
        settings.ExpertModeChanged += (_, _) => aufrufe++;

        settings.ExpertMode = false;
        settings.ExpertMode = false;

        Assert.Equal(0, aufrufe);

        settings.ExpertMode = true;
        settings.ExpertMode = true;

        Assert.Equal(1, aufrufe);
    }

    [Fact]
    public void Ohne_Abonnenten_Ist_Das_Setzen_Gefahrlos()
    {
        var settings = new GatewayRuntimeSettings(expertMode: false);

        settings.ExpertMode = true;

        Assert.True(settings.ExpertMode);
    }

    /// <summary>
    /// Eine Ausnahme eines Abonnenten darf den Schalter nicht blockieren.
    /// </summary>
    /// <remarks>
    /// Der Setter läuft im HTTP-Request der Oberfläche. Würde ein Abonnent
    /// durchschlagen, sähe der Benutzer einen Fehler - obwohl der Wert
    /// gespeichert ist.
    /// </remarks>
    [Fact]
    public void Eine_Ausnahme_Eines_Abonnenten_Bricht_Den_Setter_Nicht()
    {
        var settings = new GatewayRuntimeSettings(expertMode: false);

        var zweiterAufruf = 0;

        settings.ExpertModeChanged += (_, _) => throw new InvalidOperationException("Abonnent kaputt.");
        settings.ExpertModeChanged += (_, _) => zweiterAufruf++;

        settings.ExpertMode = true;

        Assert.True(settings.ExpertMode);

        // Auch der zweite Abonnent läuft - jeder wird einzeln abgesichert.
        Assert.Equal(1, zweiterAufruf);

        // Und ein späteres Umschalten funktioniert weiterhin.
        settings.ExpertMode = false;
        Assert.False(settings.ExpertMode);
    }
}
