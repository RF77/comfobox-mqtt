using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Das Verzeichnis der verbundenen MQTT-Clients.
/// </summary>
public interface IMqttClientProvider
{
    /// <summary>
    /// Die aktuell verbundenen Clients (einer je Broker).
    /// </summary>
    IReadOnlyList<IMqttClient> ConnectedClients { get; }

    /// <summary>
    /// Ist mindestens ein Broker verbunden?
    /// </summary>
    bool IsConnected { get; }
}

/// <summary>
/// Das Publizieren von MQTT-Nachrichten.
/// </summary>
public interface IMqttPublisher
{
    /// <summary>
    /// Publiziert eine Nachricht auf allen verbundenen Brokern.
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload (UTF-8).</param>
    /// <param name="qos">Die QoS-Stufe.</param>
    /// <param name="retain">Soll die Nachricht retained werden?</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>
    /// <c>true</c>, wenn die Nachricht an mindestens einen Broker übergeben
    /// wurde. Ohne Verbindung wird die Nachricht **verworfen** (und geloggt) -
    /// ein Publish darf den Gateway nie zum Absturz bringen.
    /// </returns>
    Task<bool> PublishAsync(
        string topic,
        string payload,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Publiziert eine JSON-Nachricht auf allen verbundenen Brokern.
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <param name="value">Der zu serialisierende Wert.</param>
    /// <param name="qos">Die QoS-Stufe.</param>
    /// <param name="retain">Soll die Nachricht retained werden?</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn die Nachricht übergeben wurde.</returns>
    Task<bool> PublishJsonAsync(
        string topic,
        object value,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false,
        CancellationToken cancellationToken = default);
}
