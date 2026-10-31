using ComfoBox.Gateway.Mqtt;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Ein Publisher, der die Nachrichten protokolliert statt sie zu senden.
/// </summary>
internal sealed class RecordingMqttPublisher : IMqttPublisher
{
    /// <summary>Die publizierten Nachrichten (Topic → Payload).</summary>
    public List<(string Topic, string Payload)> Published { get; } = [];

    /// <summary>Soll der Publish fehlschlagen?</summary>
    public bool Fail { get; set; }

    /// <inheritdoc />
    public Task<bool> PublishAsync(
        string topic,
        string payload,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false,
        CancellationToken cancellationToken = default)
    {
        if (Fail)
        {
            throw new InvalidOperationException("Der Publish wurde abgelehnt.");
        }

        Published.Add((topic, payload));
        return Task.FromResult(true);
    }

    /// <inheritdoc />
    public Task<bool> PublishJsonAsync(
        string topic,
        object value,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false,
        CancellationToken cancellationToken = default)
        => PublishAsync(topic, System.Text.Json.JsonSerializer.Serialize(value), qos, retain, cancellationToken);

    /// <summary>
    /// Die zuletzt publizierte Payload zu einem Topic (oder <c>null</c>).
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <returns>Die Payload.</returns>
    public string? LastPayloadFor(string topic)
        => Published.LastOrDefault(p => p.Topic == topic).Payload;
}
