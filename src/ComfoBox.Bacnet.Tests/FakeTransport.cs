using ComfoBox.Bacnet.Serialize;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// Transport-Attrappe für die Client-Tests.
/// </summary>
/// <remarks>
/// Der Attrappe speichert alle gesendeten Frames und kann Antworten
/// "einspielen", ohne dass ein serieller Port beteiligt ist.
/// </remarks>
public sealed class FakeTransport : IBacnetTransport
{
    private readonly List<byte[]> _sent = [];
    private readonly object _sync = new();

    /// <inheritdoc />
    public event EventHandler<BacnetFrameReceivedEventArgs>? FrameReceived;

    /// <summary>Alle gesendeten Nutzdaten (ohne Transport-Header).</summary>
    public IReadOnlyList<byte[]> Sent
    {
        get
        {
            lock (_sync)
            {
                return _sent.ToArray();
            }
        }
    }

    /// <summary>
    /// Erzeugt die Antwort auf einen gesendeten Request.
    /// </summary>
    /// <remarks>
    /// Der Hook bekommt die gesendeten Nutzdaten und liefert die Antwort
    /// (NPDU + APDU). Wird er nicht gesetzt, antwortet der Transport nicht.
    /// </remarks>
    public Func<byte[], byte[]>? Responder { get; set; }

    /// <summary>MAC-Adresse, von der aus geantwortet wird.</summary>
    public byte ResponseSourceAddress { get; set; } = 1;

    /// <inheritdoc />
    public BacnetAddressTypes Type => BacnetAddressTypes.MSTP;

    /// <inheritdoc />
    public int HeaderLength => 0;

    /// <inheritdoc />
    public int MaxBufferLength => 502;

    /// <inheritdoc />
    public BacnetMaxAdpu MaxAdpuLength => BacnetMaxAdpu.MAX_APDU480;

    /// <inheritdoc />
    public byte MaxInfoFrames { get; set; } = 1;

    /// <inheritdoc />
    public BacnetAddress GetBroadcastAddress() =>
        new(BacnetAddressTypes.MSTP, 0xFFFF, new byte[] { 0xFF });

    /// <inheritdoc />
    public void Start()
    {
    }

    /// <inheritdoc />
    public Task StopAsync() => Task.CompletedTask;

    /// <inheritdoc />
    public Task<int> SendAsync(
        byte[] buffer,
        int offset,
        int dataLength,
        BacnetAddress address,
        bool waitForTransmission,
        int timeout,
        CancellationToken cancellationToken)
    {
        var payload = new byte[dataLength];
        Array.Copy(buffer, offset, payload, 0, dataLength);

        lock (_sync)
        {
            _sent.Add(payload);
        }

        // Die Antwort synchron erzeugen, damit der wartende Aufrufer sie
        // bekommt, ohne dass ein weiterer Thread gebraucht wird.
        var responder = Responder;
        if (responder is not null)
        {
            var response = responder(payload);
            if (response is not null)
            {
                Respond(response, ResponseSourceAddress);
            }
        }

        return Task.FromResult(dataLength);
    }

    /// <inheritdoc />
    public Task<bool> WaitForAllTransmitsAsync(int timeout, CancellationToken cancellationToken) =>
        Task.FromResult(true);

    /// <summary>
    /// Simuliert eine eingehende Antwort.
    /// </summary>
    /// <param name="payload">NPDU und APDU der Antwort.</param>
    /// <param name="sourceAddress">MAC-Adresse der Gegenstelle.</param>
    public void Respond(byte[] payload, byte sourceAddress = 1)
    {
        var address = new BacnetAddress(BacnetAddressTypes.MSTP, 0, new[] { sourceAddress });
        FrameReceived?.Invoke(this, new BacnetFrameReceivedEventArgs(payload, address));
    }

    /// <summary>
    /// Baut eine ComplexAck-Antwort auf eine ReadProperty-Anfrage.
    /// </summary>
    public static byte[] BuildReadPropertyAcknowledge(
        byte invokeId,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        params BacnetValue[] values)
    {
        var buffer = new EncodeBuffer(new byte[512], 0);

        NPDU.Encode(
            buffer,
            BacnetNpduControls.PriorityNormalMessage,
            null,
            null,
            0xFF,
            BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
            0);
        APDU.EncodeComplexAck(
            buffer,
            BacnetPduTypes.PDU_TYPE_COMPLEX_ACK,
            BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY,
            invokeId,
            0,
            0);
        Services.EncodeReadPropertyAcknowledge(buffer, objectId, (uint)propertyId, ASN1.BACNET_ARRAY_ALL, values);

        var result = new byte[buffer.offset];
        Array.Copy(buffer.buffer, 0, result, 0, buffer.offset);
        return result;
    }

    /// <summary>
    /// Baut eine Error-Antwort.
    /// </summary>
    public static byte[] BuildError(
        byte invokeId,
        BacnetErrorClasses errorClass,
        BacnetErrorCodes errorCode)
    {
        var buffer = new EncodeBuffer(new byte[64], 0);

        NPDU.Encode(
            buffer,
            BacnetNpduControls.PriorityNormalMessage,
            null,
            null,
            0xFF,
            BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
            0);
        APDU.EncodeError(
            buffer,
            BacnetPduTypes.PDU_TYPE_ERROR,
            BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY,
            invokeId);
        Services.EncodeError(buffer, errorClass, errorCode);

        var result = new byte[buffer.offset];
        Array.Copy(buffer.buffer, 0, result, 0, buffer.offset);
        return result;
    }

    /// <summary>
    /// Baut eine Abort-Antwort.
    /// </summary>
    public static byte[] BuildAbort(byte invokeId, byte reason)
    {
        var buffer = new EncodeBuffer(new byte[64], 0);

        NPDU.Encode(
            buffer,
            BacnetNpduControls.PriorityNormalMessage,
            null,
            null,
            0xFF,
            BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
            0);
        APDU.EncodeAbort(buffer, BacnetPduTypes.PDU_TYPE_ABORT, invokeId, reason);

        var result = new byte[buffer.offset];
        Array.Copy(buffer.buffer, 0, result, 0, buffer.offset);
        return result;
    }

    /// <summary>
    /// Baut eine SimpleAck-Antwort (z. B. nach WriteProperty).
    /// </summary>
    public static byte[] BuildSimpleAck(byte invokeId)
    {
        var buffer = new EncodeBuffer(new byte[64], 0);

        NPDU.Encode(
            buffer,
            BacnetNpduControls.PriorityNormalMessage,
            null,
            null,
            0xFF,
            BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
            0);
        APDU.EncodeSimpleAck(
            buffer,
            BacnetPduTypes.PDU_TYPE_SIMPLE_ACK,
            BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY,
            invokeId);

        var result = new byte[buffer.offset];
        Array.Copy(buffer.buffer, 0, result, 0, buffer.offset);
        return result;
    }

    /// <summary>
    /// Baut eine IAm-Antwort.
    /// </summary>
    public static byte[] BuildIam(uint deviceId)
    {
        var buffer = new EncodeBuffer(new byte[64], 0);

        NPDU.Encode(
            buffer,
            BacnetNpduControls.PriorityNormalMessage,
            null,
            null,
            0xFF,
            BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
            0);
        APDU.EncodeUnconfirmedServiceRequest(
            buffer,
            BacnetPduTypes.PDU_TYPE_UNCONFIRMED_SERVICE_REQUEST,
            BacnetUnconfirmedServices.SERVICE_UNCONFIRMED_I_AM);
        Services.EncodeIamBroadcast(
            buffer,
            deviceId,
            480,
            BacnetSegmentations.SEGMENTATION_BOTH,
            260);

        var result = new byte[buffer.offset];
        Array.Copy(buffer.buffer, 0, result, 0, buffer.offset);
        return result;
    }

    /// <inheritdoc />
    public void Dispose()
    {
        FrameReceived = null;
    }
}
