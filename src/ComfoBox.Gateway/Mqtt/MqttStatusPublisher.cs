using System.Globalization;
using System.Text.Json;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Protection;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Publiziert die Status- und Health-Topics.
/// </summary>
/// <remarks>
/// <para>
/// Der Health-Status ist der Availability-Kanal von Home Assistant:
/// <c>Status/Health</c> trägt <c>online</c>, <c>degraded</c> oder
/// <c>offline</c>.
/// </para>
/// <para>
/// <c>degraded</c> bedeutet: der Gateway läuft, aber mindestens eine
/// Komponente ist defekt (z. B. der BACnet-Bus). Die Werte sind dann
/// <c>stale</c>, die Entitäten bleiben aber verfügbar.
/// </para>
/// <para>
/// Benutzerfehler (ein abgelehnter Schreibvorgang) verschlechern den
/// Status **nicht**.
/// </para>
/// </remarks>
public sealed class MqttStatusPublisher : BackgroundService, IMqttConnectionHandler
{
    private readonly MqttTopicMap _topics;
    private readonly IMqttPublisher _publisher;
    private readonly IHealthReporter _health;
    private readonly IErrorCollector _errors;
    private readonly IWriteCounter _writeCounter;
    private readonly BacnetOptions _bacnetOptions;
    private readonly MqttOptions _options;
    private readonly GatewayOptions _gatewayOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<MqttStatusPublisher> _logger;
    private readonly DateTimeOffset _startedAt;

    /// <summary>
    /// Erzeugt den Publisher.
    /// </summary>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="publisher">Der Publisher.</param>
    /// <param name="health">Der Health-Reporter.</param>
    /// <param name="errors">Die Fehler-Senke.</param>
    /// <param name="writeCounter">Der Schreibzähler.</param>
    /// <param name="bacnetOptions">Die BACnet-Einstellungen.</param>
    /// <param name="options">Die MQTT-Einstellungen.</param>
    /// <param name="gatewayOptions">Die Grundeinstellungen (Geräte-ID).</param>
    /// <param name="timeProvider">Die Zeitquelle.</param>
    /// <param name="logger">Logger.</param>
    public MqttStatusPublisher(
        MqttTopicMap topics,
        IMqttPublisher publisher,
        IHealthReporter health,
        IErrorCollector errors,
        IWriteCounter writeCounter,
        BacnetOptions bacnetOptions,
        MqttOptions options,
        GatewayOptions gatewayOptions,
        TimeProvider timeProvider,
        ILogger<MqttStatusPublisher> logger)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(writeCounter);
        ArgumentNullException.ThrowIfNull(bacnetOptions);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(gatewayOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _topics = topics;
        _publisher = publisher;
        _health = health;
        _errors = errors;
        _writeCounter = writeCounter;
        _bacnetOptions = bacnetOptions;
        _options = options;
        _gatewayOptions = gatewayOptions;
        _timeProvider = timeProvider;
        _logger = logger;
        _startedAt = timeProvider.GetUtcNow();

        _health.Changed += OnHealthChanged;
        _errors.Reported += OnErrorReported;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(_options.StatusIntervalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PublishPeriodicAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Der Status-Publish ist fehlgeschlagen.");
            }

            try
            {
                await Task.Delay(interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        // Nach dem Verbindungsaufbau den aktuellen Stand senden - sonst wartet
        // Home Assistant bis zum nächsten Zyklus auf die Werte.
        await PublishAsync(client, _topics.HealthTopic, HealthPayload(), retain: true, cancellationToken)
            .ConfigureAwait(false);
        await PublishAsync(client, _topics.StatusVersionTopic, MqttVersion.Current, retain: true, cancellationToken)
            .ConfigureAwait(false);
        await PublishAsync(client, _topics.StartedAtTopic, _startedAt.ToString("O"), retain: true, cancellationToken)
            .ConfigureAwait(false);
        await PublishAsync(client, _topics.UptimeTopic, UptimeSeconds().ToString(CultureInfo.InvariantCulture), retain: true, cancellationToken)
            .ConfigureAwait(false);
        await PublishAsync(client, _topics.WritesTopic, _writeCounter.Count.ToString(CultureInfo.InvariantCulture), retain: true, cancellationToken)
            .ConfigureAwait(false);
        await PublishAsync(client, _topics.WritesMaxTopic, _writeCounter.Limit.ToString(CultureInfo.InvariantCulture), retain: true, cancellationToken)
            .ConfigureAwait(false);
        await PublishAsync(client, _topics.NumberOfWritesTopic, _writeCounter.Count.ToString(CultureInfo.InvariantCulture), retain: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Die Laufzeit in Sekunden.
    /// </summary>
    /// <returns>Die Laufzeit.</returns>
    private long UptimeSeconds()
        => (long)(_timeProvider.GetUtcNow() - _startedAt).TotalSeconds;

    /// <summary>
    /// Die Payload des Health-Topics.
    /// </summary>
    /// <returns><c>online</c>, <c>degraded</c> oder <c>offline</c>.</returns>
    private string HealthPayload() => _health.Current switch
    {
        HealthState.Online => "online",
        HealthState.Degraded => "degraded",
        _ => "offline",
    };

    /// <summary>
    /// Publiziert die zyklischen Status-Topics.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    private async Task PublishPeriodicAsync(CancellationToken cancellationToken)
    {
        await _publisher.PublishAsync(
            _topics.HealthTopic,
            HealthPayload(),
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: true,
            cancellationToken).ConfigureAwait(false);

        await _publisher.PublishAsync(
            _topics.UptimeTopic,
            UptimeSeconds().ToString(CultureInfo.InvariantCulture),
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: true,
            cancellationToken).ConfigureAwait(false);

        await _publisher.PublishAsync(
            _topics.WritesTopic,
            _writeCounter.Count.ToString(CultureInfo.InvariantCulture),
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: true,
            cancellationToken).ConfigureAwait(false);

        await _publisher.PublishAsync(
            _topics.NumberOfWritesTopic,
            _writeCounter.Count.ToString(CultureInfo.InvariantCulture),
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: true,
            cancellationToken).ConfigureAwait(false);

        await _publisher.PublishAsync(
            _topics.BacnetTopic,
            BacnetPayload(),
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: true,
            cancellationToken).ConfigureAwait(false);

        await _publisher.PublishAsync(
            _topics.MqttTopic,
            MqttStatusPayload(),
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: true,
            cancellationToken).ConfigureAwait(false);

        var lastError = _errors.Snapshot().FirstOrDefault();
        await _publisher.PublishAsync(
            _topics.LastErrorTopic,
            lastError is null ? string.Empty : lastError.Message,
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: true,
            cancellationToken).ConfigureAwait(false);

        // Die Fehlerhistorie ist nicht retained - sie wächst sonst unbegrenzt.
        await _publisher.PublishAsync(
            _topics.ErrorsTopic,
            JsonSerializer.Serialize(_errors.Snapshot()),
            MqttQualityOfServiceLevel.AtLeastOnce,
            retain: false,
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Der BACnet-Status als JSON.
    /// </summary>
    /// <returns>Das JSON.</returns>
    private string BacnetPayload()
    {
        var state = _health.Components.TryGetValue("Bacnet", out var bacnet)
            ? (bacnet.State == HealthState.Online ? "online" : bacnet.State == HealthState.Degraded ? "degraded" : "offline")
            : "unknown";

        return JsonSerializer.Serialize(new
        {
            state,
            port = _bacnetOptions.Port,
            baudrate = _bacnetOptions.Baudrate,
            deviceId = _bacnetOptions.DeviceId,
            simulated = _bacnetOptions.Simulation,
        });
    }

    /// <summary>
    /// Der MQTT-Status als JSON.
    /// </summary>
    /// <returns>Das JSON.</returns>
    private string MqttStatusPayload()
    {
        var state = _health.Components.TryGetValue("Mqtt", out var mqtt)
            ? (mqtt.State == HealthState.Online ? "online" : mqtt.State == HealthState.Degraded ? "degraded" : "offline")
            : "unknown";

        return JsonSerializer.Serialize(new
        {
            state,
            clientId = MqttClientId.Effective(_options.ClientId, _gatewayOptions.DeviceId),
            keepAlive = _options.KeepAliveSeconds,
            brokers = _options.Brokers.Count,
        });
    }

    /// <summary>
    /// Publiziert eine Nachricht auf einem bestimmten Client.
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload.</param>
    /// <param name="retain">Soll retained werden?</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    private static async Task PublishAsync(
        IMqttClient client,
        string topic,
        string payload,
        bool retain,
        CancellationToken cancellationToken)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(retain)
            .Build();

        await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Wird aufgerufen, wenn sich der Health-Status ändert.
    /// </summary>
    /// <param name="sender">Der Absender.</param>
    /// <param name="e">Die Ereignis-Argumente.</param>
    private void OnHealthChanged(object? sender, HealthChangedEventArgs e)
    {
        _logger.LogInformation("Health-Status: {State} ({Message})", e.State, e.Message);

        PublishInBackground(_topics.HealthTopic, HealthPayload(), retain: true);
    }

    /// <summary>
    /// Wird aufgerufen, wenn ein Fehler gemeldet wurde.
    /// </summary>
    /// <param name="sender">Der Absender.</param>
    /// <param name="entry">Der Fehler.</param>
    private void OnErrorReported(object? sender, ErrorEntry entry)
    {
        PublishInBackground(_topics.LastErrorTopic, entry.Message, retain: true);
        PublishInBackground(_topics.ErrorsTopic, JsonSerializer.Serialize(_errors.Snapshot()), retain: false);
    }

    /// <summary>
    /// Publiziert eine Nachricht im Hintergrund (Fehler werden nur protokolliert).
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload.</param>
    /// <param name="retain">Soll retained werden?</param>
    private void PublishInBackground(string topic, string payload, bool retain)
        => _ = PublishSafelyAsync(topic, payload, retain);

    /// <summary>
    /// Publiziert eine Nachricht und schluckt Fehler.
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload.</param>
    /// <param name="retain">Soll retained werden?</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    private async Task PublishSafelyAsync(string topic, string payload, bool retain)
    {
        try
        {
            await _publisher
                .PublishAsync(topic, payload, MqttQualityOfServiceLevel.AtLeastOnce, retain, CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Der Status-Publish auf {Topic} ist fehlgeschlagen.", topic);
        }
    }
}
