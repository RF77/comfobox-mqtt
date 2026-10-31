using ComfoBox.Gateway.Configuration;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Die Rolle einer MQTT-Verbindung.
/// </summary>
/// <remarks>
/// MQTT 5 erlaubt **genau einen** Last Will pro Verbindung. Der Vertrag
/// verlangt aber zwei: <c>Status/Health</c> (Availability in Home Assistant)
/// und <c>Connected</c> (Altbestand). Deshalb hält der Gateway je Broker
/// **zwei** Verbindungen: die Hauptverbindung und eine reine
/// Will-Verbindung, die nichts publishiert und nichts abonniert.
/// </remarks>
public enum MqttConnectionRole
{
    /// <summary>
    /// Die Hauptverbindung - sie trägt den Will auf <c>Status/Health</c>.
    /// </summary>
    Main,

    /// <summary>
    /// Die Will-Verbindung - sie trägt den Will auf <c>Connected</c>.
    /// </summary>
    Will,
}

/// <summary>
/// Erzeugt die MQTT-Verbindungsoptionen.
/// </summary>
public sealed class MqttConnectionFactory
{
    private readonly MqttOptions _mqttOptions;
    private readonly MqttTopicMap _topics;

    /// <summary>
    /// Erzeugt die Fabrik.
    /// </summary>
    /// <param name="mqttOptions">Die MQTT-Einstellungen.</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    public MqttConnectionFactory(MqttOptions mqttOptions, MqttTopicMap topics)
    {
        ArgumentNullException.ThrowIfNull(mqttOptions);
        ArgumentNullException.ThrowIfNull(topics);

        _mqttOptions = mqttOptions;
        _topics = topics;
    }

    /// <summary>
    /// Baut die Optionen einer Verbindung.
    /// </summary>
    /// <param name="broker">Der Broker (Host, Port, Zugangsdaten, TLS).</param>
    /// <param name="clientId">Die Client-ID.</param>
    /// <param name="role">Die Rolle der Verbindung.</param>
    /// <returns>Die Verbindungsoptionen.</returns>
    public MqttClientOptions BuildOptions(MqttBrokerOptions broker, string clientId, MqttConnectionRole role)
    {
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);

        var builder = new MqttClientOptionsBuilder()
            .WithClientId(clientId)
            .WithTcpServer(broker.Host, broker.Port)
            .WithCleanStart()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(_mqttOptions.KeepAliveSeconds))
            .WithProtocolVersion(MqttProtocolVersion.V500)
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithWillRetain();

        switch (role)
        {
            case MqttConnectionRole.Main:
                builder = builder
                    .WithWillTopic(_topics.HealthTopic)
                    .WithWillPayload("offline");
                break;

            case MqttConnectionRole.Will:
                builder = builder
                    .WithWillTopic(_topics.ConnectedTopic)
                    .WithWillPayload("0");
                break;

            default:
                throw new ArgumentOutOfRangeException(nameof(role), role, "Unbekannte Verbindungsrolle.");
        }

        if (!string.IsNullOrEmpty(broker.Username))
        {
            builder = builder.WithCredentials(broker.Username, broker.Password ?? string.Empty);
        }

        if (broker.UseTls)
        {
            builder = builder.WithTlsOptions(tls =>
            {
                tls.UseTls();

                if (broker.AllowUntrustedCertificates)
                {
                    // Nur für das LAN mit selbstsigniertem Zertifikat.
                    tls.WithCertificateValidationHandler(_ => true);
                }
            });
        }

        return builder.Build();
    }

    /// <summary>
    /// Erzeugt einen Client (noch nicht verbunden).
    /// </summary>
    /// <returns>Der Client.</returns>
    public static IMqttClient CreateClient()
        => new MqttClientFactory().CreateMqttClient();

    /// <summary>
    /// Die Client-ID der Will-Verbindung.
    /// </summary>
    /// <param name="clientId">Die Client-ID der Hauptverbindung.</param>
    /// <returns>Die abgeleitete Client-ID.</returns>
    public static string WillClientId(string clientId)
        => $"{clientId}-will";
}
