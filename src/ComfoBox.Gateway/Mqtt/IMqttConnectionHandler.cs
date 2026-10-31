using MQTTnet;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Ein Teil des Gateways, der nach jedem Verbindungsaufbau etwas tun muss.
/// </summary>
/// <remarks>
/// Der Handler bekommt den **konkreten** Client des Brokers - so
/// abonniert und publiziert jeder Broker für sich.
/// </remarks>
public interface IMqttConnectionHandler
{
    /// <summary>
    /// Wird nach jedem erfolgreichen Verbindungsaufbau aufgerufen.
    /// </summary>
    /// <param name="client">Der Client des Brokers.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Aufbau abschließt.</returns>
    Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken);
}
