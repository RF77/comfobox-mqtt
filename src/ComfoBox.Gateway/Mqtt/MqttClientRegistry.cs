using MQTTnet;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Das Verzeichnis der aktuell verbundenen MQTT-Clients.
/// </summary>
/// <remarks>
/// <para>
/// Das Verzeichnis ist bewusst **getrennt** vom
/// <see cref="MqttConnectionManager"/>: der Publisher braucht die Clients,
/// der Manager darf aber nicht vom Publisher abhängen - sonst entsteht ein
/// Kreis in der DI-Registrierung
/// (Manager → Handler → Publisher → Provider → Manager).
/// </para>
/// </remarks>
public sealed class MqttClientRegistry : IMqttClientProvider
{
    private readonly List<IMqttClient> _clients = [];
    private readonly object _sync = new();

    /// <inheritdoc />
    public IReadOnlyList<IMqttClient> ConnectedClients
    {
        get
        {
            lock (_sync)
            {
                return [.. _clients];
            }
        }
    }

    /// <inheritdoc />
    public bool IsConnected
    {
        get
        {
            lock (_sync)
            {
                return _clients.Count > 0;
            }
        }
    }

    /// <summary>
    /// Nimmt einen Client auf (er ist verbunden).
    /// </summary>
    /// <param name="client">Der Client.</param>
    public void Add(IMqttClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        lock (_sync)
        {
            if (!_clients.Contains(client))
            {
                _clients.Add(client);
            }
        }
    }

    /// <summary>
    /// Nimmt einen Client wieder heraus (er ist getrennt).
    /// </summary>
    /// <param name="client">Der Client.</param>
    public void Remove(IMqttClient client)
    {
        ArgumentNullException.ThrowIfNull(client);

        lock (_sync)
        {
            _clients.Remove(client);
        }
    }

    /// <summary>
    /// Leert das Verzeichnis.
    /// </summary>
    public void Clear()
    {
        lock (_sync)
        {
            _clients.Clear();
        }
    }
}
