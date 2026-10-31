using System.Text.Json;
using System.Text.Json.Serialization;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Der Publisher des Gateways.
/// </summary>
/// <remarks>
/// <para>
/// Der Publisher verteilt eine Nachricht an **alle** verbundenen Broker. Ist
/// kein Broker verbunden, wird die Nachricht verworfen und protokolliert -
/// ein Publish darf den Gateway nie zum Absturz bringen.
/// </para>
/// <para>
/// Die QoS-Stufe und das Retain-Flag werden **je Aufruf** gewählt: Werte
/// laufen mit QoS 0 (hohe Frequenz), Discovery und Status mit QoS 1.
/// </para>
/// </remarks>
public sealed class MqttPublisher : IMqttPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly IMqttClientProvider _clients;
    private readonly ILogger<MqttPublisher> _logger;

    /// <summary>
    /// Erzeugt den Publisher.
    /// </summary>
    /// <param name="clients">Die verbundenen Clients.</param>
    /// <param name="logger">Logger.</param>
    public MqttPublisher(IMqttClientProvider clients, ILogger<MqttPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(clients);
        ArgumentNullException.ThrowIfNull(logger);

        _clients = clients;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<bool> PublishAsync(
        string topic,
        string payload,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);

        var clients = _clients.ConnectedClients;
        if (clients.Count == 0)
        {
            _logger.LogWarning("Publish verworfen (kein Broker verbunden): {Topic}", topic);
            return false;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(qos)
            .WithRetainFlag(retain)
            .Build();

        var published = false;

        foreach (var client in clients)
        {
            try
            {
                await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
                published = true;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                // Ein defekter Broker darf die anderen nicht blockieren (Befund A25).
                _logger.LogError(ex, "Publish auf {Topic} ist fehlgeschlagen.", topic);
            }
        }

        return published;
    }

    /// <inheritdoc />
    public Task<bool> PublishJsonAsync(
        string topic,
        object value,
        MqttQualityOfServiceLevel qos = MqttQualityOfServiceLevel.AtMostOnce,
        bool retain = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(value);

        return PublishAsync(topic, JsonSerializer.Serialize(value, JsonOptions), qos, retain, cancellationToken);
    }
}
