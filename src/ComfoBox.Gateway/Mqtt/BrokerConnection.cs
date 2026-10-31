using ComfoBox.Gateway.Configuration;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Die Verbindung zu **einem** MQTT-Broker.
/// </summary>
/// <remarks>
/// <para>
/// Jeder Broker hat zwei Verbindungen:
/// </para>
/// <list type="bullet">
/// <item>die <b>Hauptverbindung</b> - sie publiziert, abonniert und trägt den
/// Last Will auf <c>Status/Health</c>;</item>
/// <item>die <b>Will-Verbindung</b> - sie trägt den Last Will auf
/// <c>Connected</c> und tut sonst nichts.</item>
/// </list>
/// <para>
/// Grund: MQTT 5 erlaubt pro Verbindung nur **einen** Last Will, der Vertrag
/// verlangt aber zwei (Availability und Kompatibilität).
/// </para>
/// </remarks>
internal sealed class BrokerConnection : IAsyncDisposable
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly MqttBrokerOptions _broker;
    private readonly MqttConnectionFactory _factory;
    private readonly MqttTopicMap _topics;
    private readonly ILogger _logger;
    private readonly string _clientId;
    private readonly MqttClientRegistry _registry;
    private readonly Func<IMqttClient, CancellationToken, Task> _onConnected;

    private TaskCompletionSource _disconnected = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IMqttClient? _client;
    private IMqttClient? _willClient;

    /// <summary>
    /// Erzeugt die Verbindung.
    /// </summary>
    /// <param name="broker">Der Broker.</param>
    /// <param name="factory">Die Fabrik für die Verbindungsoptionen.</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="clientId">Die Client-ID der Hauptverbindung.</param>
    /// <param name="registry">Das Verzeichnis der verbundenen Clients.</param>
    /// <param name="onConnected">
    /// Der Rückruf, der nach jedem erfolgreichen Verbindungsaufbau läuft.
    /// </param>
    /// <param name="logger">Logger.</param>
    public BrokerConnection(
        MqttBrokerOptions broker,
        MqttConnectionFactory factory,
        MqttTopicMap topics,
        string clientId,
        MqttClientRegistry registry,
        Func<IMqttClient, CancellationToken, Task> onConnected,
        ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(broker);
        ArgumentNullException.ThrowIfNull(factory);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(onConnected);
        ArgumentNullException.ThrowIfNull(logger);

        _broker = broker;
        _factory = factory;
        _topics = topics;
        _clientId = clientId;
        _registry = registry;
        _onConnected = onConnected;
        _logger = logger;
    }

    /// <summary>
    /// Die Beschreibung des Brokers (für Logs).
    /// </summary>
    public string Description => $"{_broker.Host}:{_broker.Port}";

    /// <summary>
    /// Der Haupt-Client (oder <c>null</c>, wenn nicht verbunden).
    /// </summary>
    public IMqttClient? Client => _client;

    /// <summary>
    /// Ist der Broker verbunden?
    /// </summary>
    public bool IsConnected => _client?.IsConnected == true;

    /// <summary>
    /// Verbindet sich und hält die Verbindung - mit Backoff bei Fehlern.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der endet, wenn der Abbruch signalisiert wird.</returns>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var attempt = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            // Das Signal wird vor dem Verbinden installiert: ein Trennen
            // während des Verbindungsaufbaus geht sonst verloren.
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Volatile.Write(ref _disconnected, signal);

            try
            {
                await ConnectAsync(cancellationToken).ConfigureAwait(false);
                attempt = 0;

                _logger.LogInformation("MQTT verbunden: {Broker}", Description);

                await OnConnectedAsync(cancellationToken).ConfigureAwait(false);

                await signal.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                attempt++;
                var delay = Backoff(attempt);

                _logger.LogError(
                    ex,
                    "MQTT-Verbindung zu {Broker} fehlgeschlagen (Versuch {Attempt}). Neuer Versuch in {Delay}.",
                    Description,
                    attempt,
                    delay);

                try
                {
                    await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Beendet die Verbindung sauber: erst <c>offline</c> publizieren, dann
    /// trennen - sonst feuert der Last Will.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Abschluss abschließt.</returns>
    public async Task ShutdownAsync(CancellationToken cancellationToken)
    {
        if (_client is { IsConnected: true })
        {
            try
            {
                await PublishAsync(_topics.HealthTopic, "offline", retain: true, cancellationToken)
                    .ConfigureAwait(false);
                await PublishAsync(_topics.ConnectedTopic, "0", retain: true, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Der Abschluss-Publish für {Broker} ist fehlgeschlagen.", Description);
            }

            try
            {
                await _client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Die MQTT-Verbindung zu {Broker} konnte nicht sauber getrennt werden.", Description);
            }
        }

        await DisposeAsync().ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            _client.DisconnectedAsync -= OnDisconnected;
            _registry.Remove(_client);
            _client.Dispose();
            _client = null;
        }

        if (_willClient is not null)
        {
            _willClient.DisconnectedAsync -= OnDisconnected;
            _willClient.Dispose();
            _willClient = null;
        }

        await Task.CompletedTask.ConfigureAwait(false);
    }

    /// <summary>
    /// Baut die beiden Verbindungen auf.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Verbindungsaufbau abschließt.</returns>
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        await DisposeAsync().ConfigureAwait(false);

        _client = MqttConnectionFactory.CreateClient();
        _willClient = MqttConnectionFactory.CreateClient();

        _client.DisconnectedAsync += OnDisconnected;
        _willClient.DisconnectedAsync += OnDisconnected;

        var mainOptions = _factory.BuildOptions(_broker, _clientId, MqttConnectionRole.Main);
        var willOptions = _factory.BuildOptions(
            _broker,
            MqttConnectionFactory.WillClientId(_clientId),
            MqttConnectionRole.Will);

        try
        {
            await _client.ConnectAsync(mainOptions, cancellationToken).ConfigureAwait(false);

            // Der Client ist jetzt benutzbar - er kommt ins Verzeichnis, damit
            // der Publisher auf ihn zugreifen kann.
            _registry.Add(_client);

            try
            {
                await _willClient.ConnectAsync(willOptions, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                // Die Hauptverbindung wird sauber getrennt - ein Dispose würde
                // den Last Will auslösen und damit ein falsches "offline" senden.
                _logger.LogError(ex, "Die Will-Verbindung zu {Broker} ist fehlgeschlagen.", Description);
                await _client.DisconnectAsync(new MqttClientDisconnectOptions(), cancellationToken)
                    .ConfigureAwait(false);
                throw;
            }
        }
        catch
        {
            await DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    /// <summary>
    /// Publiziert eine Nachricht auf diesem Broker.
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload.</param>
    /// <param name="retain">Soll retained werden?</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    public async Task PublishAsync(
        string topic,
        string payload,
        bool retain,
        CancellationToken cancellationToken)
    {
        if (_client is not { IsConnected: true })
        {
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(retain)
            .Build();

        await _client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Führt die Schritte nach dem Verbindungsaufbau aus.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der die Schritte abschließt.</returns>
    private async Task OnConnectedAsync(CancellationToken cancellationToken)
    {
        if (_client is not { IsConnected: true })
        {
            return;
        }

        try
        {
            // Befund A10: "Connected=1" muss bei **jedem** Connect publiziert
            // werden - der Legacy tat das nur beim ersten Connect.
            await PublishAsync(_topics.ConnectedTopic, "1", retain: true, cancellationToken)
                .ConfigureAwait(false);
            await PublishAsync(_topics.VersionTopic, MqttVersion.Legacy, retain: true, cancellationToken)
                .ConfigureAwait(false);

            await _onConnected(_client, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Ein fehlerhafter Handler darf die Verbindung nicht zerstören.
            _logger.LogError(ex, "Die Nachbereitung der MQTT-Verbindung zu {Broker} ist fehlgeschlagen.", Description);
        }
    }

    /// <summary>
    /// Der Backoff zwischen zwei Verbindungsversuchen.
    /// </summary>
    /// <param name="attempt">Der Versuch (1-basiert).</param>
    /// <returns>Die Wartezeit.</returns>
    private static TimeSpan Backoff(int attempt)
    {
        var seconds = Math.Min(MaxBackoff.TotalSeconds, Math.Pow(2, Math.Min(attempt, 6)));
        return TimeSpan.FromSeconds(seconds);
    }

    /// <summary>
    /// Wird aufgerufen, wenn eine der beiden Verbindungen getrennt wird.
    /// </summary>
    /// <param name="e">Die Ereignis-Argumente.</param>
    /// <returns>Ein Task, der die Signalierung abschließt.</returns>
    private Task OnDisconnected(MqttClientDisconnectedEventArgs e)
    {
        _logger.LogWarning(e.Exception, "MQTT getrennt ({Broker}): {Reason}", Description, e.ReasonString);

        if (_client is not null)
        {
            _registry.Remove(_client);
        }

        var signal = Volatile.Read(ref _disconnected);
        signal.TrySetResult();

        return Task.CompletedTask;
    }
}
