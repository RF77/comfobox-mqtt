using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using MQTTnet;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Ein minimaler MQTT-Broker für die Tests des Setup-Assistenten.
/// </summary>
/// <remarks>
/// <para>
/// Der Broker spricht genug MQTT 5, um den Verbindungsaufbau des
/// Assistenten zu prüfen: CONNECT → CONNACK, SUBSCRIBE → SUBACK und
/// PUBLISH (mit Echo auf das abonnierte Topic).
/// </para>
/// <para>
/// Damit lassen sich die beiden Fälle trennen, die der Assistent
/// unterschiedlich melden muss:
/// </para>
/// <list type="bullet">
/// <item>der Broker lehnt die Anmeldung ab (<c>BadUserNameOrPassword</c>);</item>
/// <item>der Broker nimmt die Anmeldung an, stellt aber keine Nachrichten zu.</item>
/// </list>
/// </remarks>
internal sealed class FakeMqttBroker : IAsyncDisposable
{
    private const byte PacketConnect = 0x10;
    private const byte PacketConnack = 0x20;
    private const byte PacketPublish = 0x30;
    private const byte PacketPuback = 0x40;
    private const byte PacketSubscribe = 0x80;
    private const byte PacketSuback = 0x90;
    private const byte PacketPingreq = 0xC0;
    private const byte PacketPingresp = 0xD0;
    private const byte PacketDisconnect = 0xE0;

    private readonly TcpListener _listener;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _acceptLoop;
    private readonly List<string> _subscriptions = [];
    private readonly object _sync = new();

    /// <summary>
    /// Die negotiated Protokollversion (5 oder 4 für 3.1.1).
    /// </summary>
    private int _protocolVersion = 5;

    /// <summary>
    /// Erzeugt den Broker.
    /// </summary>
    /// <param name="resultCode">Der CONNACK-Rückgabecode.</param>
    /// <param name="echoMessages">Soll der Broker zugestellte Nachrichten wiederholen?</param>
    public FakeMqttBroker(
        MqttClientConnectResultCode resultCode = MqttClientConnectResultCode.Success,
        bool echoMessages = true)
    {
        ResultCode = resultCode;
        EchoMessages = echoMessages;

        _listener = new TcpListener(IPAddress.Loopback, 0);
        _listener.Start();

        Port = ((IPEndPoint)_listener.LocalEndpoint).Port;

        _acceptLoop = Task.Run(() => AcceptLoopAsync(_cts.Token));
    }

    /// <summary>
    /// Der Port, auf dem der Broker lauscht.
    /// </summary>
    public int Port { get; }

    /// <summary>
    /// Der CONNACK-Rückgabecode des Brokers.
    /// </summary>
    public MqttClientConnectResultCode ResultCode { get; }

    /// <summary>
    /// Wiederholt der Broker die zugestellten Nachrichten?
    /// </summary>
    public bool EchoMessages { get; }

    /// <summary>
    /// Der Benutzername aus dem CONNECT (oder <c>null</c>).
    /// </summary>
    public string? ReceivedUserName { get; private set; }

    /// <summary>
    /// Das Passwort aus dem CONNECT (oder <c>null</c>).
    /// </summary>
    public string? ReceivedPassword { get; private set; }

    /// <summary>
    /// Wurde eine Verbindung angenommen?
    /// </summary>
    public bool Accepted { get; private set; }

    /// <summary>
    /// Der letzte Fehler im Broker (oder <c>null</c>).
    /// </summary>
    public Exception? LastError { get; private set; }

    /// <summary>
    /// Beendet den Broker.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        _listener.Stop();

        try
        {
            await _acceptLoop.WaitAsync(TimeSpan.FromSeconds(5));
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        {
            // Der Broker wird nur beendet - Fehler sind hier egal.
        }

        _cts.Dispose();
    }

    /// <summary>
    /// Nimmt Verbindungen an.
    /// </summary>
    private async Task AcceptLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            TcpClient client;

            try
            {
                client = await _listener.AcceptTcpClientAsync(cancellationToken);
            }
            catch (Exception ex) when (ex is OperationCanceledException or ObjectDisposedException or SocketException)
            {
                return;
            }

            _ = Task.Run(() => HandleClientAsync(client, cancellationToken), CancellationToken.None);
        }
    }

    /// <summary>
    /// Bedient eine Verbindung.
    /// </summary>
    private async Task HandleClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using (client)
        {
            var stream = client.GetStream();

            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var packet = await ReadPacketAsync(stream, cancellationToken);

                    if (packet is null)
                    {
                        return;
                    }

                    var (type, payload) = packet.Value;

                    switch (type & 0xF0)
                    {
                        case PacketConnect:
                            await HandleConnectAsync(payload, stream);
                            break;

                        case PacketSubscribe:
                            await HandleSubscribeAsync(payload, stream);
                            break;

                        case PacketPublish:
                            await HandlePublishAsync(payload, stream);
                            break;

                        case PacketPingreq:
                            await stream.WriteAsync(new byte[] { PacketPingresp, 0x00 }, cancellationToken);
                            break;

                        case PacketDisconnect:
                            return;

                        default:
                            break;
                    }
                }
            }
            catch (Exception ex) when (ex is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // Die Verbindung wurde geschlossen - das ist normal.
            }
            catch (Exception ex)
            {
                LastError = ex;
            }
        }
    }

    /// <summary>
    /// Beantwortet das CONNECT.
    /// </summary>
    private async Task HandleConnectAsync(byte[] payload, NetworkStream stream)
    {
        var (version, userName, password) = ParseConnect(payload);

        _protocolVersion = version;

        ReceivedUserName = userName;
        ReceivedPassword = password;

        if (ResultCode != MqttClientConnectResultCode.Success)
        {
            // Der Broker lehnt ab: CONNACK mit dem Fehlercode, dann Ende.
            await stream.WriteAsync(Connack(version, ResultCode), CancellationToken.None);
            Accepted = false;
            return;
        }

        Accepted = true;
        await stream.WriteAsync(Connack(version, MqttClientConnectResultCode.Success), CancellationToken.None);
    }

    /// <summary>
    /// Beantwortet das SUBSCRIBE.
    /// </summary>
    private async Task HandleSubscribeAsync(byte[] payload, NetworkStream stream)
    {
        // MQTT 5: Packet-Id (2 Bytes) + Eigenschaften + Topic-Filter.
        var offset = 2;
        var propertiesLength = (int)ReadVariableByteInteger(payload, ref offset);
        offset += propertiesLength;

        while (offset < payload.Length)
        {
            var topicLength = (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;

            var topic = Encoding.UTF8.GetString(payload, offset, topicLength);
            offset += topicLength;

            // MQTT 5: nach dem Topic-Filter folgt ein Byte mit den
            // Abonnement-Optionen (QoS, No Local, Retain …).
            if (_protocolVersion >= 5)
            {
                offset += 1;
            }

            lock (_sync)
            {
                _subscriptions.Add(topic);
            }

            // Der Broker stimmt allen Abonnements zu (QoS 0).
            // MQTT 5: Packet-Id, Länge der Eigenschaften (0), Grund (0).
            await stream.WriteAsync(
                new byte[] { PacketSuback, 0x04, payload[0], payload[1], 0x00, 0x00 },
                CancellationToken.None);
        }
    }

    /// <summary>
    /// Beantwortet das PUBLISH (mit Echo, wenn gewünscht).
    /// </summary>
    private async Task HandlePublishAsync(byte[] payload, NetworkStream stream)
    {
        var offset = 0;
        var topicLength = (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
        offset += 2;

        var topic = Encoding.UTF8.GetString(payload, offset, topicLength);
        offset += topicLength;

        // Die QoS steht im Kopfbyte (0x30 | QoS).
        var qos = (payload[0] & 0x06) >> 1;

        var topicBytes = payload.AsSpan(0, 2 + topicLength).ToArray();

        if (qos > 0)
        {
            offset += 2; // Packet-Id
        }

        var propertiesStart = offset;
        var propertiesLength = (int)ReadVariableByteInteger(payload, ref offset);
        var propertiesBytes = payload.AsSpan(propertiesStart, propertiesLength + (offset - propertiesStart)).ToArray();

        offset += propertiesLength;

        var body = payload[offset..];

        bool subscribed;

        lock (_sync)
        {
            subscribed = _subscriptions.Contains(topic, StringComparer.Ordinal);
        }

        if (!EchoMessages || !subscribed)
        {
            return;
        }

        // QoS 0: das Echo trägt keine Packet-Id.
        var echo = new List<byte>
        {
            PacketPublish,
        };

        var bodyWithTopic = new List<byte>();
        bodyWithTopic.AddRange(topicBytes);
        bodyWithTopic.AddRange(propertiesBytes);
        bodyWithTopic.AddRange(body);

        echo.AddRange(EncodeVariableByteInteger((uint)bodyWithTopic.Count));
        echo.AddRange(bodyWithTopic);

        await stream.WriteAsync(echo.ToArray(), CancellationToken.None);
    }

    /// <summary>
    /// Baut das CONNACK.
    /// </summary>
    private static byte[] Connack(int version, MqttClientConnectResultCode code)
    {
        if (version >= 5)
        {
            // CONNACK: Kennzeichen, Grundeinstellungen, Grund, Eigenschaften (0).
            return [PacketConnack, 0x03, 0x00, (byte)code, 0x00];
        }

        // MQTT 3.1.1: der Rückgabecode hat andere Werte.
        var legacy = code switch
        {
            MqttClientConnectResultCode.BadUserNameOrPassword => (byte)0x04,
            MqttClientConnectResultCode.NotAuthorized => (byte)0x05,
            _ => (byte)0x01,
        };

        return [PacketConnack, 0x02, 0x00, legacy];
    }

    /// <summary>
    /// Liest ein CONNECT-Paket aus.
    /// </summary>
    /// <returns>Die Protokollversion, der Benutzername und das Passwort.</returns>
    private static (int Version, string? UserName, string? Password) ParseConnect(byte[] payload)
    {
        var offset = 0;

        // Protokollname (Länge + Text) und Version.
        var nameLength = (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
        offset += 2 + nameLength;

        var version = payload[offset];
        offset++;

        // Kennzeichen, Keep-Alive.
        var flags = payload[offset];
        offset += 3;

        if (version >= 5)
        {
            var propertiesLength = (int)ReadVariableByteInteger(payload, ref offset);
            offset += propertiesLength;
        }

        // Client-Id.
        var clientIdLength = (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
        offset += 2 + clientIdLength;

        if ((flags & 0x04) != 0)
        {
            // Will-Nachricht: Eigenschaften, Topic, Nutzlast.
            var willProperties = (int)ReadVariableByteInteger(payload, ref offset);
            offset += willProperties;

            var willTopicLength = (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2 + willTopicLength;

            var willLength = (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2 + willLength;
        }

        string? userName = null;
        string? password = null;

        if ((flags & 0x80) != 0)
        {
            var length = (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;

            userName = Encoding.UTF8.GetString(payload, offset, length);
            offset += length;
        }

        if ((flags & 0x40) != 0)
        {
            var length = (int)BinaryPrimitives.ReadUInt16BigEndian(payload.AsSpan(offset));
            offset += 2;

            password = Encoding.UTF8.GetString(payload, offset, length);
        }

        return (version, userName, password);
    }

    /// <summary>
    /// Liest ein Paket (Kopfbyte + Restlänge + Nutzlast).
    /// </summary>
    private static async Task<(byte Type, byte[] Payload)?> ReadPacketAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var header = new byte[1];

        if (await stream.ReadAsync(header, cancellationToken) == 0)
        {
            return null;
        }

        var length = 0;
        var multiplier = 1;

        while (true)
        {
            var next = new byte[1];

            if (await stream.ReadAsync(next, cancellationToken) == 0)
            {
                return null;
            }

            length += (next[0] & 0x7F) * multiplier;
            multiplier *= 128;

            if ((next[0] & 0x80) == 0)
            {
                break;
            }
        }

        var payload = new byte[length];
        var read = 0;

        while (read < length)
        {
            var count = await stream.ReadAsync(payload.AsMemory(read), cancellationToken);

            if (count == 0)
            {
                return null;
            }

            read += count;
        }

        return (header[0], payload);
    }

    /// <summary>
    /// Liest eine variable Byte-Ganzzahl (MQTT).
    /// </summary>
    private static uint ReadVariableByteInteger(byte[] data, ref int offset)
    {
        uint multiplier = 1;
        uint value = 0;
        byte next;

        do
        {
            next = data[offset];
            offset++;
            value += (uint)(next & 0x7F) * multiplier;
            multiplier *= 128;
        }
        while ((next & 0x80) != 0);

        return value;
    }

    /// <summary>
    /// Kodiert eine variable Byte-Ganzzahl (MQTT).
    /// </summary>
    private static byte[] EncodeVariableByteInteger(uint value)
    {
        var bytes = new List<byte>();

        do
        {
            var digit = (byte)(value % 128);
            value /= 128;

            if (value > 0)
            {
                digit |= 0x80;
            }

            bytes.Add(digit);
        }
        while (value > 0);

        return [.. bytes];
    }
}
