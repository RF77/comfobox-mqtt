using System.ComponentModel.DataAnnotations;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die Grundeinstellungen des Gateways.
/// </summary>
public sealed class GatewayOptions
{
    /// <summary>
    /// Der Name des Konfigurationsabschnitts.
    /// </summary>
    public const string SectionName = "Gateway";

    /// <summary>
    /// Das Basis-Topic aller MQTT-Topics.
    /// </summary>
    /// <remarks>
    /// Der Wert ist Teil des MQTT-Vertrags - der Standard <c>ComfoBox</c> darf
    /// nicht geändert werden. Leerzeichen sind verboten, weil sie die
    /// Topic-Struktur zerstören würden.
    /// </remarks>
    [Required]
    [RegularExpression(@"^\S+$", ErrorMessage = "Gateway:BaseTopic darf keine Leerzeichen enthalten.")]
    public string BaseTopic { get; init; } = "ComfoBox";

    /// <summary>
    /// Der Experten-Modus.
    /// </summary>
    /// <remarks>
    /// Ohne Expert-Modus sind alle Items mit <c>[ExpertMode]</c> nicht
    /// schreibbar - genau wie im Legacy-Stand.
    /// </remarks>
    public bool ExpertMode { get; init; }

    /// <summary>
    /// Das Datenverzeichnis (Schreibzähler, später die Setup-Konfiguration).
    /// </summary>
    /// <remarks>
    /// Relative Pfade werden gegen das Arbeitsverzeichnis des Prozesses
    /// aufgelöst. Im Container ist das ein Volume - der Schreibzähler muss
    /// einen Neustart überleben.
    /// </remarks>
    [Required]
    public string DataDirectory { get; init; } = "data";

    /// <summary>
    /// Das Poll-Intervall in Sekunden (Legacy-Stand: 60 s).
    /// </summary>
    [Range(1, 3600)]
    public int PollIntervalSeconds { get; init; } = 60;

    /// <summary>
    /// Wie oft ein sekundäres Item gelesen wird.
    /// </summary>
    /// <remarks>
    /// Sekundäre Items werden alle <c>SecondaryPollFactor</c> Zyklen gelesen
    /// (Standard: alle 5 Zyklen). Primäre Items werden in jedem Zyklus gelesen.
    /// </remarks>
    [Range(1, 60)]
    public int SecondaryPollFactor { get; init; } = 5;

    /// <summary>
    /// Die maximale Anzahl der Schreibvorgänge im 24-h-Fenster.
    /// </summary>
    /// <remarks>
    /// Der Wert ist der EEPROM-Schutz der ComfoBox. <c>0</c> schaltet den
    /// Schutz ab (der Zähler läuft dann nur noch mit).
    /// </remarks>
    [Range(0, 100_000)]
    public int MaxWritesPer24h { get; init; } = 200;
}
