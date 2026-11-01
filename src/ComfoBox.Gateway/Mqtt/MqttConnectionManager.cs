using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using MQTTnet;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Verwaltet die MQTT-Verbindungen des Gateways.
/// </summary>
/// <remarks>
/// <para>
/// Der Manager startet für jeden konfigurierten Broker eine eigene
/// <see cref="BrokerConnection"/> und hält sie am Leben: Connect, Reconnect
/// mit Backoff, orderly Shutdown.
/// </para>
/// <para>
/// Ein Broker-Ausfall blockiert die anderen nicht (Befund A25) - jeder Broker
/// hat seine eigene Schleife.
/// </para>
/// </remarks>
public sealed class MqttConnectionManager : BackgroundService, IAsyncDisposable
{
    private readonly MqttOptions _mqttOptions;
    private readonly MqttTopicMap _topics;
    private readonly MqttConnectionFactory _factory;
    private readonly MqttClientRegistry _registry;
    private readonly IHealthReporter _health;
    private readonly IErrorCollector _errors;
    private readonly IReadOnlyList<IMqttConnectionHandler> _handlers;
    private readonly ILogger<MqttConnectionManager> _logger;

    private readonly List<BrokerConnection> _connections = [];
    private readonly List<Task> _runs = [];
    private readonly CancellationTokenSource _shutdown = new();

    /// <summary>
    /// Erzeugt den Manager.
    /// </summary>
    /// <param name="mqttOptions">Die MQTT-Einstellungen.</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="factory">Die Fabrik für die Verbindungsoptionen.</param>
    /// <param name="registry">Das Verzeichnis der verbundenen Clients.</param>
    /// <param name="health">Der Health-Reporter.</param>
    /// <param name="errors">Die Fehler-Senke.</param>
    /// <param name="handlers">Die Handler, die nach jedem Connect laufen.</param>
    /// <param name="logger">Logger.</param>
    public MqttConnectionManager(
        MqttOptions mqttOptions,
        MqttTopicMap topics,
        MqttConnectionFactory factory,
        MqttClientRegistry registry,
        IHealthReporter health,
        IErrorCollector errors,
        IEnumerable<IMqttConnectionHandler> handlers,
        ILogger<MqttConnectionManager> logger)
    {
        ArgumentNullException.ThrowIfNull(mqttOptions);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(handlers);
        ArgumentNullException.ThrowIfNull(logger);

        _mqttOptions = mqttOptions;
        _topics = topics;
        _factory = factory;
        _registry = registry;
        _health = health;
        _errors = errors;
        _handlers = [.. handlers];
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken, _shutdown.Token);

        foreach (var broker in _mqttOptions.Brokers)
        {
            var connection = new BrokerConnection(
                broker,
                _factory,
                _topics,
                _mqttOptions.ClientId,
                _registry,
                OnConnectedAsync,
                _logger);

            _connections.Add(connection);
            _runs.Add(connection.RunAsync(linked.Token));
        }

        _logger.LogInformation(
            "MQTT-Verbindungsmanagement gestartet: {Count} Broker, Keepalive {Keepalive} s.",
            _connections.Count,
            _mqttOptions.KeepAliveSeconds);

        try
        {
            await Task.WhenAll(_runs).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Der geordnete Shutdown läuft über StopAsync.
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("MQTT-Verbindungen werden beendet.");

        // Zuerst die Reconnect-Schleifen beenden - sonst verbindet sich der
        // Gateway während des Shutdowns erneut.
        await _shutdown.CancelAsync().ConfigureAwait(false);

        try
        {
            await Task.WhenAll(_runs).WaitAsync(TimeSpan.FromSeconds(5), cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Der Shutdown läuft weiter.
        }
        catch (TimeoutException)
        {
            _logger.LogWarning("Die MQTT-Verbindungen wurden nicht rechtzeitig beendet.");
        }

        // Jetzt der geordnete Shutdown: erst "offline" publizieren, dann
        // trennen - sonst feuert der Last Will.
        foreach (var connection in _connections)
        {
            try
            {
                await connection.ShutdownAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Die MQTT-Verbindung zu {Broker} konnte nicht sauber beendet werden.", connection.Description);
            }
        }

        _health.Report("Mqtt", HealthState.Offline, "Der Gateway wird beendet.");

        await base.StopAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Fordert einen Neuaufbau der Broker-Verbindungen an.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Anzahl der Broker, für die ein Neuaufbau angefordert wurde.</returns>
    /// <remarks>
    /// Der Normalfall ist der automatische Reconnect mit Backoff. Dieser
    /// Endpunkt ist für den Fall, dass der Broker wieder läuft, aber die
    /// Verbindung nicht mehr sauber ist.
    /// </remarks>
    public async Task<int> RequestReconnectAsync(CancellationToken cancellationToken)
    {
        var count = 0;

        foreach (var connection in _connections)
        {
            try
            {
                await connection.RequestReconnectAsync(cancellationToken).ConfigureAwait(false);
                count++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Der MQTT-Reconnect für {Broker} ist fehlgeschlagen.",
                    connection.Description);
            }
        }

        return count;
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        foreach (var connection in _connections)
        {
            await connection.DisposeAsync().ConfigureAwait(false);
        }

        _connections.Clear();
        _registry.Clear();
        _shutdown.Dispose();
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Der Rückruf, der nach jedem Verbindungsaufbau läuft.
    /// </summary>
    /// <param name="client">Der Client des Brokers.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der die Nachbereitung abschließt.</returns>
    private async Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        _health.ReportHealthy("Mqtt", $"MQTT verbunden ({client.Options.ClientId}).");

        foreach (var handler in _handlers)
        {
            try
            {
                await handler.OnConnectedAsync(client, cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Der MQTT-Handler {Handler} ist fehlgeschlagen.", handler.GetType().Name);
                _errors.Report("Mqtt", "handler_failed", $"MQTT-Handler {handler.GetType().Name} ist fehlgeschlagen: {ex.Message}", ex);
            }
        }
    }
}
