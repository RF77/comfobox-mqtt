using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Health;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Der Subscriber für die Schreib-Topics.
/// </summary>
/// <remarks>
/// <para>
/// Abonniert werden ausschließlich die <c>/Set</c>- und
/// <c>/AsNumber/Set</c>-Topics der **schreibbaren** Items. Die Schreibbarkeit
/// hängt am Experten-Modus - genau wie im Legacy.
/// </para>
/// <para>
/// Der Schreibvorgang läuft **immer** über
/// <see cref="BacnetDevice.WriteAsync"/>: nur dort wird der 24-h-Zähler
/// erhöht. Ein Schreibvorgang per MQTT darf den Schreibschutz nicht
/// umgehen.
/// </para>
/// </remarks>
public sealed class MqttSubscriber : IMqttConnectionHandler
{
    private readonly MqttTopicMap _topics;
    private readonly BacnetDevice _device;
    private readonly IErrorCollector _errors;
    private readonly IMqttPublisher _publisher;
    private readonly ILogger<MqttSubscriber> _logger;

    /// <summary>
    /// Erzeugt den Subscriber.
    /// </summary>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="device">Das BACnet-Gerät (der einzige Schreibpfad).</param>
    /// <param name="errors">Die Fehler-Senke.</param>
    /// <param name="publisher">Der Publisher (für die Rückmeldung des Wertes).</param>
    /// <param name="logger">Logger.</param>
    public MqttSubscriber(
        MqttTopicMap topics,
        BacnetDevice device,
        IErrorCollector errors,
        IMqttPublisher publisher,
        ILogger<MqttSubscriber> logger)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(logger);

        _topics = topics;
        _device = device;
        _errors = errors;
        _publisher = publisher;
        _logger = logger;
    }

    /// <summary>
    /// Die Anzahl der abonnierten Schreib-Topics.
    /// </summary>
    public int SubscribedCount { get; private set; }

    /// <inheritdoc />
    public async Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var writeTopics = _topics.WriteTopics();
        if (writeTopics.Count == 0)
        {
            _logger.LogInformation("Keine Schreib-Topics - es werden keine /Set-Topics abonniert.");
            return;
        }

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder();

        foreach (var topic in writeTopics)
        {
            subscribeOptions.WithTopicFilter(new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());
        }

        var options = subscribeOptions.Build();

        client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;

        await client.SubscribeAsync(options, cancellationToken).ConfigureAwait(false);

        SubscribedCount = writeTopics.Count;

        _logger.LogInformation("{Count} Schreib-Topics abonniert.", writeTopics.Count);
    }

    /// <summary>
    /// Verarbeitet eine eingegangene Nachricht.
    /// </summary>
    /// <param name="e">Die Ereignis-Argumente.</param>
    /// <returns>Ein Task, der die Verarbeitung abschließt.</returns>
    private async Task OnMessageReceivedAsync(MqttApplicationMessageReceivedEventArgs e)
    {
        var topic = e.ApplicationMessage.Topic;
        var payload = e.ApplicationMessage.ConvertPayloadToString();

        try
        {
            await HandleMessageAsync(topic, payload).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Der Shutdown läuft - der Schreibvorgang wird abgebrochen.
        }
    }

    /// <summary>
    /// Verarbeitet eine Nachricht auf einem Schreib-Topic.
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload.</param>
    /// <returns>Ein Task, der die Verarbeitung abschließt.</returns>
    internal async Task HandleMessageAsync(string topic, string payload)
    {
        if (!_topics.TryResolve(topic, out var item, out _))
        {
            return;
        }

        try
        {
            await HandleAsync(item!, payload).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Der Schreibvorgang für {Topic} ist fehlgeschlagen.", topic);
            _errors.Report("Mqtt", "write_failed", $"Schreibvorgang ({item!.TopicPath}) ist fehlgeschlagen: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Führt einen Schreibvorgang aus.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="payload">Die Payload.</param>
    /// <returns>Ein Task, der den Schreibvorgang abschließt.</returns>
    private async Task HandleAsync(ItemDefinition item, string payload)
    {
        _logger.LogInformation("Schreibvorgang angefordert: {Topic} = {Payload}", item.TopicPath, payload);

        double? target = null;

        switch (item.Kind)
        {
            case ItemValueKind.Enum:
                if (!MqttPayload.TryParseEnum(payload, item.EnumValues, out var enumValue) || enumValue is null)
                {
                    // Befund A6: niemals den Wert 0 schreiben.
                    Reject(item, $"'{payload}' ist kein gültiger Wert für {item.Name}.");
                    return;
                }

                target = enumValue.Value;
                break;

            case ItemValueKind.Analog:
                if (!MqttPayload.TryParse(payload, out var number))
                {
                    Reject(item, $"'{payload}' ist keine gültige Zahl (erwartet wird z. B. '21.5').");
                    return;
                }

                target = number;
                break;

            default:
                // Die Legacy-DateItems haben keinen Wert (Befund A20).
                _logger.LogDebug("Für {Item} gibt es keinen schreibbaren Wert.", item.Id);
                return;
        }

        var outcome = await _device.WriteAsync(item, target.Value, CancellationToken.None).ConfigureAwait(false);

        if (outcome != WriteOutcome.Ok)
        {
            ReportOutcome(item, outcome);
            return;
        }

        await PublishWrittenValueAsync(item, target.Value).ConfigureAwait(false);
    }

    /// <summary>
    /// Publiziert den geschriebenen Wert sofort - so sieht Home Assistant
    /// die Änderung ohne den nächsten Poll-Zyklus abwarten zu müssen.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="value">Der geschriebene Wert.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    private async Task PublishWrittenValueAsync(ItemDefinition item, double value)
    {
        if (item.Kind == ItemValueKind.Enum)
        {
            // Der Vertrag veröffentlicht den Enum-Namen, nicht die Zahl.
            var name = item.EnumValues.TryGetValue((int)value, out var enumName) ? enumName : string.Empty;
            await _publisher
                .PublishAsync(_topics.ItemTopic(item), name, MqttQualityOfServiceLevel.AtMostOnce, retain: true)
                .ConfigureAwait(false);

            await _publisher
                .PublishAsync(
                    _topics.ItemAsNumberTopic(item),
                    MqttPayload.Format((float)value),
                    MqttQualityOfServiceLevel.AtMostOnce,
                    retain: true)
                .ConfigureAwait(false);

            return;
        }

        await _publisher
            .PublishAsync(
                _topics.ItemTopic(item),
                MqttPayload.Format((float)value),
                MqttQualityOfServiceLevel.AtMostOnce,
                retain: true)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Meldet einen **abgelehnten** Schreibvorgang.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="reason">Der Ablehnungsgrund.</param>
    private void Reject(ItemDefinition item, string reason)
    {
        _logger.LogWarning("Schreibvorgang für {Item} abgelehnt: {Reason}", item.Id, reason);

        _errors.Report(
            "Mqtt",
            "invalid_value",
            $"Schreibvorgang ({item.TopicPath}) abgelehnt: {reason}",
            null,
            "warning");
    }

    /// <summary>
    /// Meldet einen vom BACnet-Gerät abgelehnten Schreibvorgang.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="outcome">Das Ergebnis.</param>
    private void ReportOutcome(ItemDefinition item, WriteOutcome outcome)
    {
        // LimitExceeded und BusError meldet das BACnet-Gerät bereits selbst.
        var (code, message) = outcome switch
        {
            WriteOutcome.ReadOnly => ("read_only", $"{item.TopicPath} ist nicht schreibbar."),
            WriteOutcome.ExpertModeRequired => ("expert_mode_required", $"{item.TopicPath} erfordert den Experten-Modus."),
            WriteOutcome.OutOfRange => ("value_out_of_range", $"Der Wert für {item.TopicPath} liegt außerhalb des gültigen Bereichs."),
            WriteOutcome.InvalidType => ("invalid_value", $"Der Wert für {item.TopicPath} ist ungültig."),
            _ => (string.Empty, string.Empty),
        };

        if (string.IsNullOrEmpty(code))
        {
            return;
        }

        _errors.Report("Mqtt", code, $"Schreibvorgang ({item.TopicPath}) abgelehnt: {message}", null, "warning");
    }
}
