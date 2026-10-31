using ComfoBox.Gateway.Configuration;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Reagiert auf die „Geburt" der Home-Assistant-MQTT-Integration.
/// </summary>
/// <remarks>
/// <para>
/// Home Assistant publiziert auf <c>homeassistant/status</c> den Payload
/// <c>online</c>, wenn seine MQTT-Integration startet. Der Gateway
/// wiederholt dann die Discovery und die Zustände - so sind die Entitäten
/// auch nach einem Neustart von Home Assistant sofort da.
/// </para>
/// <para>
/// Die Discovery-Payloads sind zusätzlich **retained** - die Wiederholung
/// ist damit Komfort, keine Notwendigkeit. Sie schützt aber davor, dass
/// Home Assistant eine veraltete Discovery behält.
/// </para>
/// </remarks>
public sealed class HomeAssistantBirthHandler : IMqttConnectionHandler
{
    private readonly MqttOptions _options;
    private readonly HomeAssistantDiscovery _discovery;
    private readonly MqttStatePublisher _statePublisher;
    private readonly ILogger<HomeAssistantBirthHandler> _logger;

    private IMqttClient? _client;

    /// <summary>
    /// Erzeugt den Handler.
    /// </summary>
    /// <param name="options">Die MQTT-Einstellungen.</param>
    /// <param name="discovery">Die Discovery.</param>
    /// <param name="statePublisher">Der Publisher der Item-Werte.</param>
    /// <param name="logger">Logger.</param>
    public HomeAssistantBirthHandler(
        MqttOptions options,
        HomeAssistantDiscovery discovery,
        MqttStatePublisher statePublisher,
        ILogger<HomeAssistantBirthHandler> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(discovery);
        ArgumentNullException.ThrowIfNull(statePublisher);
        ArgumentNullException.ThrowIfNull(logger);

        _options = options;
        _discovery = discovery;
        _statePublisher = statePublisher;
        _logger = logger;
    }

    /// <summary>
    /// Das Topic, auf dem Home Assistant seine Geburt meldet.
    /// </summary>
    public string StatusTopic => $"{_options.DiscoveryPrefix}/status";

    /// <inheritdoc />
    public async Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        _client = client;
        client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        var filter = new MqttTopicFilterBuilder()
            .WithTopic(StatusTopic)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .Build();

        var options = new MqttClientSubscribeOptionsBuilder()
            .WithTopicFilter(filter)
            .Build();

        await client.SubscribeAsync(options, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Höre auf {Topic} (Geburt von Home Assistant).", StatusTopic);
    }

    /// <summary>
    /// Verarbeitet eine eingegangene Nachricht.
    /// </summary>
    /// <param name="e">Die Ereignis-Argumente.</param>
    /// <returns>Ein Task, der die Verarbeitung abschließt.</returns>
    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        if (!string.Equals(e.ApplicationMessage.Topic, StatusTopic, StringComparison.Ordinal))
        {
            return;
        }

        var payload = e.ApplicationMessage.ConvertPayloadToString();

        if (!string.Equals(payload, "online", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var client = _client;
        if (client is null)
        {
            return;
        }

        _logger.LogInformation("Home Assistant ist online - Discovery und Zustände werden wiederholt.");

        try
        {
            await _discovery.OnConnectedAsync(client, CancellationToken.None).ConfigureAwait(false);
            await _statePublisher.PublishAllAsync(client, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Die Wiederholung der Discovery nach der Geburt ist fehlgeschlagen.");
        }
    }
}