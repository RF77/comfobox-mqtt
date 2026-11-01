using System.ComponentModel.DataAnnotations;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die Einstellungen der REST/SSE-Schnittstelle.
/// </summary>
public sealed class ApiOptions
{
    /// <summary>
    /// Der Name des Konfigurationsabschnitts.
    /// </summary>
    public const string SectionName = "Api";

    /// <summary>
    /// Müssen alle Anfragen authentifiziert werden?
    /// </summary>
    /// <remarks>
    /// <para>
    /// Standard ist <c>false</c>: im reinen LAN regelt die Firewall den Zugriff.
    /// Wer den Gateway aus dem Internet erreichbar macht, schaltet die
    /// Authentifizierung ein (oder stellt einen Reverse-Proxy mit Basic-Auth
    /// davor).
    /// </para>
    /// <para>
    /// Unabhängig von dieser Einstellung sind die <c>/api/setup/*</c>-Endpunkte
    /// **nach** der Ersteinrichtung geschützt - vorher ist der Gateway noch
    /// funktionslos und darf offen sein.
    /// </para>
    /// </remarks>
    public bool RequireAuthentication { get; init; }

    /// <summary>
    /// Der Port, den der Assistent für die Web-UI vorschlägt.
    /// </summary>
    [Range(1, 65535)]
    public int SuggestedUiPort { get; init; } = 8081;

    /// <summary>
    /// Das Zeitfenster des BACnet-Tests im Setup (Sekunden).
    /// </summary>
    [Range(1, 120)]
    public int BacnetTestTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Das Zeitfenster des MQTT-Tests im Setup (Sekunden).
    /// </summary>
    [Range(1, 120)]
    public int MqttTestTimeoutSeconds { get; init; } = 10;

    /// <summary>
    /// Die Lebensdauer einer Web-Sitzung in Minuten.
    /// </summary>
    /// <remarks>
    /// Die Sitzung liegt serverseitig: nach dem Ablauf muss sich der Benutzer
    /// erneut anmelden. Standard ist 8 Stunden.
    /// </remarks>
    [Range(1, 10_080)]
    public int SessionLifetimeMinutes { get; init; } = 480;
}
