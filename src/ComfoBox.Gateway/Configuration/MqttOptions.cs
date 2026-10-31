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
    [Required]
    public string ClientId { get; init; } = "ComfoBox";

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
}
