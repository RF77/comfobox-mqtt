using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Ein MQTT-Client, der alles schluckt - für Tests ohne Broker.
/// </summary>
/// <remarks>
/// Der Client <b>zeichnet auf</b>, was der Gateway mit ihm tut: Abonnements,
/// Abbestellungen und die Anzahl der Nachrichten-Handler. Nur so lässt sich
/// prüfen, dass ein Wechsel des Experten-Modus die <c>/Set</c>-Topics wirklich
/// ab- und wieder anbestellt.
/// </remarks>
internal sealed class NoopMqttClient : IMqttClient
{
    private readonly object _sync = new();
    private readonly List<Func<MqttApplicationMessageReceivedEventArgs, Task>> _handlers = [];

    /// <summary>Die publizierten Nachrichten.</summary>
    public List<MqttApplicationMessage> Published { get; } = [];

    /// <summary>Die abonnierten Topic-Filter, in der Reihenfolge der Anfragen.</summary>
    public List<string> SubscribedTopicFilters { get; } = [];

    /// <summary>Die abbestellten Topic-Filter, in der Reihenfolge der Anfragen.</summary>
    public List<string> UnsubscribedTopicFilters { get; } = [];

    /// <summary>Die Topics, die aktuell abonniert sind (Subscribe minus Unsubscribe).</summary>
    public HashSet<string> ActiveSubscriptions { get; } = new(StringComparer.Ordinal);

    /// <summary>Eine Ausnahme, die <see cref="SubscribeAsync"/> auslöst (Fehlerpfad).</summary>
    public Exception? SubscribeFailure { get; set; }

    /// <summary>Wie viele Nachrichten-Handler gerade angehängt sind.</summary>
    public int MessageHandlerCount
    {
        get
        {
            lock (_sync)
            {
                return _handlers.Count;
            }
        }
    }

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public MqttClientOptions Options { get; } = new MqttClientOptionsBuilder()
        .WithClientId("comfobox-test-noop")
        .WithTcpServer("localhost", 1883)
        .Build();

    /// <inheritdoc />
    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync
    {
        add
        {
            if (value is null)
            {
                return;
            }

            lock (_sync)
            {
                _handlers.Add(value);
            }
        }

        remove
        {
            if (value is null)
            {
                return;
            }

            lock (_sync)
            {
                _handlers.Remove(value);
            }
        }
    }

    /// <inheritdoc />
    public event Func<MqttClientConnectedEventArgs, Task>? ConnectedAsync
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event Func<MqttClientConnectingEventArgs, Task>? ConnectingAsync
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event Func<MqttClientDisconnectedEventArgs, Task>? DisconnectedAsync
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public event Func<MQTTnet.Diagnostics.PacketInspection.InspectMqttPacketEventArgs, Task>? InspectPacketAsync
    {
        add { }
        remove { }
    }

    /// <inheritdoc />
    public Task<MqttClientConnectResult> ConnectAsync(MqttClientOptions options, CancellationToken cancellationToken)
    {
        IsConnected = true;
        return Task.FromResult(new MqttClientConnectResult());
    }

    /// <inheritdoc />
    public Task DisconnectAsync(MqttClientDisconnectOptions options, CancellationToken cancellationToken)
    {
        IsConnected = false;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task PingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    /// <inheritdoc />
    public Task<MqttClientPublishResult> PublishAsync(MqttApplicationMessage applicationMessage, CancellationToken cancellationToken)
    {
        Published.Add(applicationMessage);
        return Task.FromResult(new MqttClientPublishResult(
            null,
            MqttClientPublishReasonCode.Success,
            string.Empty,
            []));
    }

    /// <inheritdoc />
    public Task<MqttClientSubscribeResult> SubscribeAsync(MqttClientSubscribeOptions options, CancellationToken cancellationToken)
    {
        if (SubscribeFailure is not null)
        {
            throw SubscribeFailure;
        }

        foreach (var filter in options.TopicFilters)
        {
            SubscribedTopicFilters.Add(filter.Topic);
            ActiveSubscriptions.Add(filter.Topic);
        }

        return Task.FromResult(new MqttClientSubscribeResult(0, [], string.Empty, []));
    }

    /// <inheritdoc />
    public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken)
    {
        foreach (var topic in options.TopicFilters)
        {
            UnsubscribedTopicFilters.Add(topic);
            ActiveSubscriptions.Remove(topic);
        }

        return Task.FromResult(new MqttClientUnsubscribeResult(0, [], string.Empty, []));
    }

    /// <inheritdoc />
    public Task SendEnhancedAuthenticationExchangeDataAsync(
        MqttEnhancedAuthenticationExchangeData data,
        CancellationToken cancellationToken)
        => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Die zuletzt publizierte Payload zu einem Topic (oder <c>null</c>).
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <returns>Die Payload.</returns>
    public string? LastPayloadFor(string topic) => LastMessageFor(topic)?.ConvertPayloadToString();

    /// <summary>
    /// Die letzte Nachricht zu einem Topic (oder <c>null</c>).
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <returns>Die Nachricht.</returns>
    public MqttApplicationMessage? LastMessageFor(string topic)
    {
        for (var i = Published.Count - 1; i >= 0; i--)
        {
            if (string.Equals(Published[i].Topic, topic, StringComparison.Ordinal))
            {
                return Published[i];
            }
        }

        return null;
    }

    /// <summary>
    /// Wurde das Topic zuletzt mit einer **leeren** Payload geräumt?
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <returns><c>true</c>, wenn die letzte Nachricht leer und retained ist.</returns>
    /// <remarks>
    /// Nur eine leere, retained Nachricht löscht ein Discovery-Topic im Broker.
    /// MQTTnet 5 liefert für eine leere Payload <c>null</c> statt <c>""</c>.
    /// </remarks>
    public bool WasCleared(string topic)
    {
        var message = LastMessageFor(topic);

        return message is not null
            && message.Retain
            && message.ConvertPayloadToString() is null;
    }

    /// <summary>
    /// Löst das Ereignis für eine eingegangene Nachricht aus.
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload.</param>
    /// <returns>Ein Task, der die Zustellung abschließt.</returns>
    public async Task RaiseMessageAsync(string topic, string payload)
    {
        Func<MqttApplicationMessageReceivedEventArgs, Task>[] handlers;
        lock (_sync)
        {
            handlers = [.. _handlers];
        }

        if (handlers.Length == 0)
        {
            return;
        }

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(payload)
            .Build();

        var packet = new MqttPublishPacket
        {
            Topic = topic,
            PayloadSegment = new ArraySegment<byte>(System.Text.Encoding.UTF8.GetBytes(payload)),
            QualityOfServiceLevel = MqttQualityOfServiceLevel.AtMostOnce,
            Retain = false,
        };

        var args = new MqttApplicationMessageReceivedEventArgs(
            "comfobox-test-noop",
            message,
            packet,
            null!);

        foreach (var handler in handlers)
        {
            await handler(args).ConfigureAwait(false);
        }
    }
}
