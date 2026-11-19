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

    /// <summary>
    /// Der Abstand der Lebenszeichen im SSE-Stream (Sekunden, <c>0</c> = aus).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Stream schickt bei jeder Wertänderung ein Ereignis; bei ruhigem Bus
    /// entsteht minutenlang keine einzige Zeile. Ein Proxy (oder eine
    /// Firewall) verwirft eine solche Verbindung stillschweigend - der Client
    /// merkt es erst, wenn er wieder Werte erwartet.
    /// </para>
    /// <para>
    /// Deshalb schickt der Gateway in diesem Abstand eine Kommentarzeile
    /// (<c>: ping</c>). Sie ist kein Ereignis und wird von Browsern wie von
    /// <c>aiohttp</c> ignoriert, hält aber die Verbindung wach.
    /// </para>
    /// </remarks>
    [Range(0, 600)]
    public int SseHeartbeatSeconds { get; init; } = 30;

    /// <summary>
    /// Ruhezeit, nach der ein Sammel-Ereignis abgeschickt wird (Millisekunden).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ein Poll-Zyklus liest die Items **nacheinander** über den Bus - 35
    /// Änderungen entstehen dabei innerhalb von Sekunden, ein
    /// Sekundärzyklus bringt 160. Ein Ereignis je Wert hieße: der Empfänger
    /// verarbeitet jeden Zyklus in Dutzenden Runden (die HA-Integration
    /// benachrichtigt bei jeder Nachricht **alle** Entitäten).
    /// </para>
    /// <para>
    /// Deshalb sammelt der Stream die Werte: er schickt sie gebündelt
    /// (<c>event: values</c> mit einem Array), sobald diese Zeit lang **kein**
    /// neuer Wert mehr kam. <c>0</c> schickt jedes Ereignis sofort.
    /// </para>
    /// </remarks>
    [Range(0, 5_000)]
    public int SseBatchQuietMs { get; init; } = 200;

    /// <summary>
    /// Die längste Verzögerung, die ein Wert im Sammelfenster wartet (Millisekunden).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Ohne diese Obergrenze würde ein dauerhaft fließender Strom nie
    /// abgeschickt. Sie begrenzt zugleich die Latenz eines einzelnen Werts.
    /// </para>
    /// <para>
    /// <c>0</c> zusammen mit <see cref="SseBatchQuietMs"/> = <c>0</c> stellt das
    /// Verhalten vor dem Sammelfenster wieder her (ein Ereignis je Wert).
    /// </para>
    /// </remarks>
    [Range(0, 60_000)]
    public int SseBatchMaxDelayMs { get; init; } = 1_000;
}
