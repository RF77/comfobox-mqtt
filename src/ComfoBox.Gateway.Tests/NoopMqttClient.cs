using MQTTnet;
using MQTTnet.Packets;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Ein MQTT-Client, der alles schluckt - für Tests ohne Broker.
/// </summary>
internal sealed class NoopMqttClient : IMqttClient
{
    /// <summary>Die publizierten Nachrichten.</summary>
    public List<MqttApplicationMessage> Published { get; } = [];

    /// <inheritdoc />
    public bool IsConnected { get; private set; }

    /// <inheritdoc />
    public MqttClientOptions Options { get; } = new MqttClientOptionsBuilder()
        .WithClientId("comfobox-test-noop")
        .WithTcpServer("localhost", 1883)
        .Build();

    /// <inheritdoc />
    public event Func<MqttApplicationMessageReceivedEventArgs, Task>? ApplicationMessageReceivedAsync;

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
        => Task.FromResult(new MqttClientSubscribeResult(0, [], string.Empty, []));

    /// <inheritdoc />
    public Task<MqttClientUnsubscribeResult> UnsubscribeAsync(MqttClientUnsubscribeOptions options, CancellationToken cancellationToken)
        => Task.FromResult(new MqttClientUnsubscribeResult(0, [], string.Empty, []));

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
    public string? LastPayloadFor(string topic)
    {
        for (var i = Published.Count - 1; i >= 0; i--)
        {
            if (Published[i].Topic == topic)
            {
                return Published[i].ConvertPayloadToString();
            }
        }

        return null;
    }

    /// <summary>
    /// Löst das Ereignis für eine eingegangene Nachricht aus.
    /// </summary>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload.</param>
    /// <returns>Ein Task, der die Zustellung abschließt.</returns>
    public Task RaiseMessageAsync(string topic, string payload)
    {
        var handler = ApplicationMessageReceivedAsync;
        if (handler is null)
        {
            return Task.CompletedTask;
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

        return handler(new MqttApplicationMessageReceivedEventArgs(
            "comfobox-test-noop",
            message,
            packet,
            null!));
    }
}
