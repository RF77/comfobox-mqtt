using System.Runtime.CompilerServices;
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
/// <b>Experten-Modus zur Laufzeit (Aufgabe 10.13):</b> Wird der Modus
/// umgeschaltet, bestellt <see cref="RefreshSubscriptionsAsync"/> die alten
/// Topics ab und die neuen an - ohne Neustart.
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
    private readonly object _sync = new();

    // Welche Client-Instanz hat den Nachrichten-Handler schon? Ein Client
    // entsteht bei jedem Reconnect neu (siehe BrokerConnection) - deshalb
    // schwache Schlüssel: sonst wüchse die Menge mit jedem Reconnect.
    private readonly ConditionalWeakTable<IMqttClient, object> _handlerRegistered = new();

    private static readonly object HandlerRegisteredMarker = new();

    private IReadOnlyList<string> _subscribedTopics = [];

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
    public int SubscribedCount
    {
        get
        {
            lock (_sync)
            {
                return _subscribedTopics.Count;
            }
        }
    }

    /// <summary>
    /// Die Topics, die gerade abonniert sind.
    /// </summary>
    /// <remarks>
    /// Die Liste ist die Grundlage für das Abbestehen beim Moduswechsel: der
    /// Broker kennt keine "alles außer…"-Anfrage, die alten Topics müssen
    /// einzeln genannt werden.
    /// </remarks>
    public IReadOnlyList<string> SubscribedTopics
    {
        get
        {
            lock (_sync)
            {
                return _subscribedTopics;
            }
        }
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        EnsureHandlerRegistered(client);

        await SubscribeAsync(client, _topics.WriteTopics(), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Baut die Abonnements auf allen Clients neu auf (Wechsel des Experten-Modus).
    /// </summary>
    /// <param name="clients">Die verbundenen Clients.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Umbau abschließt.</returns>
    /// <remarks>
    /// Ohne Clients passiert nichts - der nächste Verbindungsaufbau abonniert
    /// ohnehin den dann gültigen Satz (<see cref="OnConnectedAsync"/>).
    /// </remarks>
    public async Task RefreshSubscriptionsAsync(
        IReadOnlyList<IMqttClient> clients,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clients);

        var newTopics = _topics.WriteTopics();

        IReadOnlyList<string> oldTopics;
        lock (_sync)
        {
            oldTopics = _subscribedTopics;
        }

        var unchanged = SameTopics(oldTopics, newTopics);

        foreach (var client in clients)
        {
            EnsureHandlerRegistered(client);

            if (!unchanged)
            {
                await UnsubscribeAsync(client, oldTopics, cancellationToken).ConfigureAwait(false);
            }

            await SubscribeAsync(client, newTopics, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Abonniert einen Satz Schreib-Topics auf einem Client.
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <param name="topics">Die Topics.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der das Abonnement abschließt.</returns>
    private async Task SubscribeAsync(
        IMqttClient client,
        IReadOnlyList<string> topics,
        CancellationToken cancellationToken)
    {
        if (topics.Count == 0)
        {
            _logger.LogInformation("Keine Schreib-Topics - es werden keine /Set-Topics abonniert.");
            SetSubscribedTopics([]);
            return;
        }

        var subscribeOptions = new MqttClientSubscribeOptionsBuilder();

        foreach (var topic in topics)
        {
            subscribeOptions.WithTopicFilter(new MqttTopicFilterBuilder()
                .WithTopic(topic)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
                .Build());
        }

        var options = subscribeOptions.Build();

        await client.SubscribeAsync(options, cancellationToken).ConfigureAwait(false);

        SetSubscribedTopics(topics);

        _logger.LogInformation("{Count} Schreib-Topics abonniert.", topics.Count);
    }

    /// <summary>
    /// Bestellt einen Satz Schreib-Topics auf einem Client ab.
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <param name="topics">Die Topics.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der das Abbestellen abschließt.</returns>
    private async Task UnsubscribeAsync(
        IMqttClient client,
        IReadOnlyList<string> topics,
        CancellationToken cancellationToken)
    {
        if (topics.Count == 0)
        {
            return;
        }

        var unsubscribeOptions = new MqttClientUnsubscribeOptionsBuilder();

        foreach (var topic in topics)
        {
            unsubscribeOptions.WithTopicFilter(topic);
        }

        await client.UnsubscribeAsync(unsubscribeOptions.Build(), cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("{Count} Schreib-Topics abbestellt.", topics.Count);
    }

    /// <summary>
    /// Merkt sich den aktuell abonnierten Satz.
    /// </summary>
    /// <param name="topics">Die Topics.</param>
    private void SetSubscribedTopics(IReadOnlyList<string> topics)
    {
        lock (_sync)
        {
            _subscribedTopics = topics;
        }
    }

    /// <summary>
    /// Sind zwei Topic-Listen gleich (auch in der Reihenfolge)?
    /// </summary>
    /// <param name="left">Die eine Liste.</param>
    /// <param name="right">Die andere Liste.</param>
    /// <returns><c>true</c>, wenn sie identisch sind.</returns>
    private static bool SameTopics(IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!string.Equals(left[i], right[i], StringComparison.Ordinal))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Hängt den Nachrichten-Handler **genau einmal** an einen Client.
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <remarks>
    /// <see cref="OnConnectedAsync"/> und <see cref="RefreshSubscriptionsAsync"/>
    /// können für denselben Client laufen. Ohne diesen Schutz hinge der Handler
    /// zweimal am Client - jede Nachricht würde doppelt verarbeitet und damit
    /// doppelt geschrieben.
    /// </remarks>
    private void EnsureHandlerRegistered(IMqttClient client)
    {
        lock (_sync)
        {
            if (_handlerRegistered.TryGetValue(client, out _))
            {
                return;
            }

            _handlerRegistered.Add(client, HandlerRegisteredMarker);
            client.ApplicationMessageReceivedAsync += OnMessageReceivedAsync;
        }
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
