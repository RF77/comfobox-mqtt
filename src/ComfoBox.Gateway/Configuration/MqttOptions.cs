using System.ComponentModel.DataAnnotations;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die Einstellungen der MQTT-Anbindung.
/// </summary>
public sealed class MqttOptions
{
    /// <summary>
    /// Der Name des Konfigurationsabschnitts.
    /// </summary>
    public const string SectionName = "Mqtt";

    /// <summary>
    /// Die Broker, mit denen verbunden wird.
    /// </summary>
    /// <remarks>
    /// Mindestens ein Broker mit Host und Port muss konfiguriert sein -
    /// sonst startet der Gateway nicht (der Start schlägt bei ungültiger
    /// Konfiguration fehl).
    /// </remarks>
    public IReadOnlyList<MqttBrokerOptions> Brokers { get; init; } = [];

    /// <summary>
    /// Die Client-ID, die der Gateway beim Broker anmeldet.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Die Client-ID muss je Installation eindeutig sein - zwei Gateways mit
    /// derselben ID verdrängen sich gegenseitig beim Broker. Der Legacy
    /// hängte den Zeitstempel an (<c>ComfoBox{ticks}</c>) - das erzeugte bei
    /// jedem Connect eine neue Sitzung.
    /// </para>
    /// <para>
    /// Der Standard ist <c>comfobox-gateway-{Hostname}</c>: stabil und
    /// trotzdem auf verschiedenen Rechnern eindeutig. Für den Parallelbetrieb
    /// (zwei Gateways auf einem Host) muss die ID konfiguriert werden.
    /// </para>
    /// </remarks>
    [Required]
    public string ClientId { get; init; } = $"comfobox-gateway-{Environment.MachineName}";

    /// <summary>
    /// Das Keep-Alive in Sekunden.
    /// </summary>
    /// <remarks>
    /// Der Legacy nutzte 2 Sekunden - das hat den Broker laufend getötet
    /// (Befund A9). Der Wert muss mindestens 15 Sekunden betragen.
    /// </remarks>
    [Range(15, 3600)]
    public int KeepAliveSeconds { get; init; } = 30;

    /// <summary>
    /// Das Präfix der Home-Assistant-Discovery-Topics.
    /// </summary>
    /// <remarks>
    /// Der Standard <c>homeassistant</c> ist der von Home Assistant
    /// erwartete Präfix.
    /// </remarks>
    [Required]
    [RegularExpression(@"^\S+$", ErrorMessage = "Mqtt:DiscoveryPrefix darf keine Leerzeichen enthalten.")]
    public string DiscoveryPrefix { get; init; } = "homeassistant";

    /// <summary>
    /// Sollen die Item-Werte retained publiziert werden?
    /// </summary>
    /// <remarks>
    /// Retain sorgt dafür, dass Home Assistant den Zustand nach einem
    /// Neustart ohne erneutes Polling kennt.
    /// </remarks>
    public bool UseMqttRetain { get; init; } = true;

    /// <summary>
    /// Das Intervall der zyklischen Status-Publishes in Sekunden.
    /// </summary>
    /// <remarks>
    /// Betrifft <c>Status/Uptime</c>, <c>Status/Writes</c>, <c>Status/Bacnet</c>
    /// und <c>Status/Mqtt</c>. Fehler werden zusätzlich sofort publiziert.
    /// </remarks>
    [Range(1, 300)]
    public int StatusIntervalSeconds { get; init; } = 10;

    /// <summary>
    /// Prüft, ob mindestens ein vollständig konfigurierter Broker vorhanden ist.
    /// </summary>
    /// <param name="options">Die zu prüfenden Einstellungen.</param>
    public static bool HasValidBroker(MqttOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        return options.Brokers.Count > 0
            && options.Brokers.All(b => !string.IsNullOrWhiteSpace(b.Host) && b.Port > 0);
    }
}

/// <summary>
/// Die Einstellungen eines MQTT-Brokers.
/// </summary>
public sealed class MqttBrokerOptions
{
    /// <summary>
    /// Der Hostname oder die IP-Adresse des Brokers.
    /// </summary>
    [Required]
    [RegularExpression(@"^\S+$", ErrorMessage = "Mqtt:Brokers:Host darf nicht leer sein.")]
    public string Host { get; init; } = string.Empty;

    /// <summary>
    /// Der Port des Brokers.
    /// </summary>
    [Range(1, 65535)]
    public int Port { get; init; } = 1883;

    /// <summary>
    /// Der Benutzername (oder <c>null</c>).
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Das Passwort (oder <c>null</c>).
    /// </summary>
    /// <remarks>
    /// Wird **nie** geloggt - der Log-Ringpuffer ist in der Web-UI sichtbar.
    /// </remarks>
    public string? Password { get; init; }

    /// <summary>
    /// Soll die Verbindung über TLS laufen?
    /// </summary>
    public bool UseTls { get; init; }

    /// <summary>
    /// Sollen selbstsignierte Zertifikate akzeptiert werden?
    /// </summary>
    /// <remarks>
    /// Nur für das LAN mit einem selbstsignierten Zertifikat. In produktiven
    /// Umgebungen darf das **nicht** aktiviert sein.
    /// </remarks>
    public bool AllowUntrustedCertificates { get; init; }
}
