using System.Collections.Concurrent;
using ComfoBox.Bacnet.Serialize;
using Microsoft.Extensions.Logging;

namespace ComfoBox.Bacnet;

/// <summary>
/// BACnet-Client für bestätigte Dienste (ReadProperty, WriteProperty) und
/// WhoIs/IAm.
/// </summary>
/// <remarks>
/// <para>
/// Der Client ist die moderne Entsprechung des Legacy
/// <c>src_old/Bacnet/BACnetClient.cs</c>. Die Protokolllogik (NPDU/APDU,
/// Request-Aufbau, Verarbeitung der Antworten) ist unverändert übernommen.
/// Bewusst geändert wurden:
/// </para>
/// <list type="bullet">
/// <item>
/// <b>async/await statt blockierender Events:</b> Der Legacy nutzte
/// <c>IAsyncResult</c> + <c>ManualResetEvent</c> und blockierte im
/// <c>WaitForDone</c>. Der neue Client arbeitet mit
/// <see cref="TaskCompletionSource{TResult}"/> und echten
/// <see cref="CancellationToken"/>.
/// </item>
/// <item>
/// <b>Eigene Exception-Typen</b> statt <c>throw new Exception("…")</c>.
/// </item>
/// <item>
/// <b><see cref="Interlocked"/> für die Invoke-ID</b> - im Legacy war
/// <c>m_invoke_id++</c> nicht threadsicher.
/// </item>
/// <item>
/// <b>Kein <c>Trace</c></b>, sondern <see cref="ILogger"/>.
/// </item>
/// </list>
/// <para>
/// Der Client ist ein reiner BACnet-Client: Anfragen der Gegenstelle werden
/// protokolliert, aber nicht beantwortet.
/// </para>
/// </remarks>
public sealed class BacnetClient : IBacnetClient
{
    /// <summary>Standard-Hop-Count (unbegrenzt) laut BACnet.</summary>
    public const byte DefaultHopCount = 0xFF;

    private const ushort DefaultVendorId = 260;

    private readonly IBacnetTransport _transport;
    private readonly ILogger<BacnetClient> _logger;
    private readonly ConcurrentDictionary<byte, TaskCompletionSource<byte[]>> _pending = new();
    private readonly ConcurrentDictionary<byte, SegmentAssembly> _segments = new();

    private int _invokeId;
    private byte _lastSequenceNumber;
    private bool _disposed;

    /// <summary>
    /// Erzeugt den Client.
    /// </summary>
    /// <param name="transport">Der zu verwendende Transport.</param>
    /// <param name="logger">Logger.</param>
    public BacnetClient(IBacnetTransport transport, ILogger<BacnetClient> logger)
    {
        ArgumentNullException.ThrowIfNull(transport);
        ArgumentNullException.ThrowIfNull(logger);

        _transport = transport;
        _logger = logger;
    }

    /// <summary>
    /// Zeitfenster für die Antwort einer Gegenstelle (ms).
    /// </summary>
    /// <remarks>Im Legacy war dieser Wert fest 1000 ms.</remarks>
    public int Timeout { get; set; } = 1000;

    /// <summary>
    /// Anzahl der Versuche, bis eine Anfrage als fehlgeschlagen gilt.
    /// </summary>
    /// <remarks>Im Legacy war dieser Wert fest 3.</remarks>
    public int Retries { get; set; } = 3;

    /// <summary>
    /// Zeitfenster, bis ein Frame tatsächlich auf die Schnittstelle
    /// geschrieben wurde (ms).
    /// </summary>
    /// <remarks>
    /// Im Legacy war dieser Wert 30 000 ms - bei MS/TP kann der Master den
    /// Token erst nach längerer Zeit erhalten.
    /// </remarks>
    public int TransmitTimeout { get; set; } = 30000;

    /// <summary>
    /// Priorität beim Schreiben einer Eigenschaft.
    /// </summary>
    /// <remarks>
    /// 0 entspricht <c>BACNET_NO_PRIORITY</c> - das ist der Wert des Legacy.
    /// </remarks>
    public uint WritePriority { get; set; }

    /// <summary>
    /// Hersteller-Kennung, die in eigenen IAm-Antworten gemeldet wird.
    /// </summary>
    public ushort VendorId { get; set; } = DefaultVendorId;

    /// <summary>
    /// Wird ausgelöst, wenn eine Gegenstelle auf ein WhoIs geantwortet hat.
    /// </summary>
    public event EventHandler<BacnetIamEventArgs>? IamReceived;

    /// <summary>
    /// Startet den Client und den Transport.
    /// </summary>
    public void Start()
    {
        _transport.FrameReceived += OnFrameReceived;
        _transport.Start();

        _logger.LogInformation("BACnet-Client gestartet.");
    }

    /// <summary>
    /// Stoppt den Client und den Transport.
    /// </summary>
    public async Task StopAsync()
    {
        _transport.FrameReceived -= OnFrameReceived;

        // Alle wartenden Anfragen beenden, damit niemand ewig wartet.
        foreach (var key in _pending.Keys)
        {
            if (_pending.TryRemove(key, out var tcs))
            {
                tcs.TrySetException(new BacnetException("Der BACnet-Client wurde gestoppt."));
            }
        }

        _segments.Clear();

        await _transport.StopAsync().ConfigureAwait(false);

        _logger.LogInformation("BACnet-Client gestoppt.");
    }

    /// <summary>
    /// Fordert einen Neuaufbau der seriellen Schnittstelle an.
    /// </summary>
    public void RequestReconnect() => _transport.RequestReconnect();

    /// <summary>
    /// Sendet ein WhoIs an alle Geräte im Bus.
    /// </summary>
    /// <param name="lowLimit">Untere Grenze der Geräte-Instanz oder -1.</param>
    /// <param name="highLimit">Obere Grenze der Geräte-Instanz oder -1.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    public async Task WhoIsAsync(int lowLimit = -1, int highLimit = -1, CancellationToken cancellationToken = default)
    {
        var receiver = _transport.GetBroadcastAddress();

        var b = GetEncodeBuffer();
        NPDU.Encode(
            b,
            BacnetNpduControls.PriorityNormalMessage,
            receiver.RoutedSource,
            null,
            DefaultHopCount,
            BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
            0);
        APDU.EncodeUnconfirmedServiceRequest(
            b,
            BacnetPduTypes.PDU_TYPE_UNCONFIRMED_SERVICE_REQUEST,
            BacnetUnconfirmedServices.SERVICE_UNCONFIRMED_WHO_IS);
        Services.EncodeWhoIsBroadcast(b, lowLimit, highLimit);

        // WhoIs ist unbestätigt - es wird nicht auf eine Antwort gewartet.
        await _transport.SendAsync(
            b.buffer,
            _transport.HeaderLength,
            b.offset - _transport.HeaderLength,
            receiver,
            waitForTransmission: false,
            timeout: 0,
            cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("WhoIs gesendet ({Low}..{High}).", lowLimit, highLimit);
    }

    /// <summary>
    /// Liest eine Eigenschaft.
    /// </summary>
    /// <param name="address">Adresse der Gegenstelle.</param>
    /// <param name="objectId">Objekt, dessen Eigenschaft gelesen wird.</param>
    /// <param name="propertyId">Eigenschaft.</param>
    /// <param name="arrayIndex">Array-Index oder <c>BACNET_ARRAY_ALL</c>.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Die gelesenen Werte (bei Skalaren genau einer).</returns>
    /// <exception cref="BacnetTimeoutException">Keine Antwort innerhalb des Zeitfensters.</exception>
    /// <exception cref="BacnetDeviceErrorException">Die Gegenstelle meldete einen BACnet-Fehler.</exception>
    /// <exception cref="BacnetDeviceAbortException">Die Gegenstelle hat den Request abgebrochen.</exception>
    /// <exception cref="BacnetDecodeException">Die Antwort konnte nicht dekodiert werden.</exception>
    public async Task<IList<BacnetValue>> ReadPropertyAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        uint arrayIndex = ASN1.BACNET_ARRAY_ALL,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);

        var invokeId = NextInvokeId();

        var b = GetEncodeBuffer();
        NPDU.Encode(
            b,
            BacnetNpduControls.PriorityNormalMessage | BacnetNpduControls.ExpectingReply,
            address.RoutedSource,
            null,
            DefaultHopCount,
            BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
            0);
        APDU.EncodeConfirmedServiceRequest(
            b,
            BacnetPduTypes.PDU_TYPE_CONFIRMED_SERVICE_REQUEST,
            BacnetConfirmedServices.SERVICE_CONFIRMED_READ_PROPERTY,
            BacnetMaxSegments.MAX_SEG0,
            _transport.MaxAdpuLength,
            invokeId,
            0,
            0);
        Services.EncodeReadProperty(b, objectId, (uint)propertyId, arrayIndex);

        var content = await SendAndWaitAsync(address, invokeId, b, waitForTransmission: true, cancellationToken)
            .ConfigureAwait(false);

        if (Services.DecodeReadPropertyAcknowledge(
                content,
                0,
                content.Length,
                out _,
                out _,
                out var values) < 0)
        {
            throw new BacnetDecodeException("Die Antwort auf ReadProperty konnte nicht dekodiert werden.");
        }

        return values;
    }

    /// <summary>
    /// Schreibt eine Eigenschaft.
    /// </summary>
    /// <param name="address">Adresse der Gegenstelle.</param>
    /// <param name="objectId">Objekt, dessen Eigenschaft geschrieben wird.</param>
    /// <param name="propertyId">Eigenschaft.</param>
    /// <param name="values">Zu schreibende Werte.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <exception cref="BacnetTimeoutException">Keine Antwort innerhalb des Zeitfensters.</exception>
    /// <exception cref="BacnetDeviceErrorException">Die Gegenstelle meldete einen BACnet-Fehler.</exception>
    /// <exception cref="BacnetDeviceAbortException">Die Gegenstelle hat den Request abgebrochen.</exception>
    public async Task WritePropertyAsync(
        BacnetAddress address,
        BacnetObjectId objectId,
        BacnetPropertyIds propertyId,
        IEnumerable<BacnetValue> values,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(address);
        ArgumentNullException.ThrowIfNull(values);

        var invokeId = NextInvokeId();

        var b = GetEncodeBuffer();

        // Achtung: das Legacy setzte hier kein "ExpectingReply" - das wird
        // bewusst unverändert übernommen, damit die Bytes auf dem Bus identisch
        // bleiben.
        NPDU.Encode(
            b,
            BacnetNpduControls.PriorityNormalMessage,
            address.RoutedSource,
            null,
            DefaultHopCount,
            BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
            0);
        APDU.EncodeConfirmedServiceRequest(
            b,
            BacnetPduTypes.PDU_TYPE_CONFIRMED_SERVICE_REQUEST,
            BacnetConfirmedServices.SERVICE_CONFIRMED_WRITE_PROPERTY,
            BacnetMaxSegments.MAX_SEG0,
            _transport.MaxAdpuLength,
            invokeId,
            0,
            0);
        Services.EncodeWriteProperty(b, objectId, (uint)propertyId, ASN1.BACNET_ARRAY_ALL, WritePriority, values);

        await SendAndWaitAsync(address, invokeId, b, waitForTransmission: true, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Gibt den Puffer für die Kodierung eines Requests.
    /// </summary>
    /// <remarks>
    /// Der Puffer beginnt erst an Position <see cref="IBacnetTransport.HeaderLength"/>
    /// - so wie im Legacy. Der Transport braucht den Platz für seinen eigenen
    /// Header.
    /// </remarks>
    private EncodeBuffer GetEncodeBuffer() =>
        new(new byte[_transport.MaxBufferLength], _transport.HeaderLength);

    /// <summary>
    /// Vergibt die nächste Invoke-ID.
    /// </summary>
    /// <remarks>
    /// 0 ist reserviert ("automatisch vergeben"), deshalb startet die Zählung
    /// bei 1.
    /// </remarks>
    private byte NextInvokeId()
    {
        var id = (byte)(Interlocked.Increment(ref _invokeId) & 0xFF);
        return id == 0 ? (byte)(Interlocked.Increment(ref _invokeId) & 0xFF) : id;
    }

    /// <summary>
    /// Sendet einen Request und wartet auf die zugehörige Antwort.
    /// </summary>
    /// <param name="address">Zieladresse.</param>
    /// <param name="invokeId">Invoke-ID des Requests.</param>
    /// <param name="buffer">Kodierter Request.</param>
    /// <param name="waitForTransmission">Auf das tatsächliche Schreiben warten.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Der Inhalt der Antwort (ohne APDU-Header).</returns>
    private async Task<byte[]> SendAndWaitAsync(
        BacnetAddress address,
        byte invokeId,
        EncodeBuffer buffer,
        bool waitForTransmission,
        CancellationToken cancellationToken)
    {
        var tcs = new TaskCompletionSource<byte[]>(TaskCreationOptions.RunContinuationsAsynchronously);

        if (!_pending.TryAdd(invokeId, tcs))
        {
            throw new BacnetException(
                $"Für die Invoke-ID {invokeId} läuft bereits eine Anfrage.");
        }

        try
        {
            var attempts = Math.Max(1, Retries);

            for (var attempt = 1; attempt <= attempts; attempt++)
            {
                var sent = await _transport.SendAsync(
                    buffer.buffer,
                    _transport.HeaderLength,
                    buffer.offset - _transport.HeaderLength,
                    address,
                    waitForTransmission,
                    TransmitTimeout,
                    cancellationToken).ConfigureAwait(false);

                if (sent < 0)
                {
                    throw new BacnetException(
                        $"Der Request an {address} konnte nicht gesendet werden (Fehlercode {sent}).");
                }

                try
                {
                    return await tcs.Task.WaitAsync(TimeSpan.FromMilliseconds(Timeout), cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                    _logger.LogWarning(
                        "Keine Antwort von {Address} auf Invoke-ID {InvokeId} (Versuch {Attempt}/{Attempts}).",
                        address,
                        invokeId,
                        attempt,
                        attempts);

                    if (attempt == attempts)
                    {
                        throw new BacnetTimeoutException(
                            $"Keine Antwort von {address} innerhalb von {Timeout} ms.");
                    }
                }
            }

            throw new BacnetTimeoutException(
                $"Keine Antwort von {address} innerhalb von {Timeout} ms.");
        }
        finally
        {
            _pending.TryRemove(invokeId, out _);
        }
    }

    /// <summary>
    /// Verarbeitet einen empfangenen Frame.
    /// </summary>
    private void OnFrameReceived(object? sender, BacnetFrameReceivedEventArgs e)
    {
        try
        {
            ProcessFrame(e.Payload, e.RemoteAddress);
        }
        catch (Exception ex)
        {
            // Fehler dürfen nicht verschluckt werden - der Legacy hat hier
            // zwar nur "Trace.TraceError" geschrieben, der Gateway darf aber
            // keinen Frame verlieren, ohne es zu merken.
            _logger.LogError(ex, "Fehler beim Verarbeiten eines BACnet-Frames.");
        }
    }

    /// <summary>
    /// Zerlegt einen Frame in NPDU und APDU und leitet ihn weiter.
    /// </summary>
    private void ProcessFrame(byte[] payload, BacnetAddress remoteAddress)
    {
        if (payload.Length == 0)
        {
            return;
        }

        var npduLength = NPDU.Decode(
            payload,
            0,
            out var function,
            out _,
            out _,
            out _,
            out _,
            out _);

        if (npduLength <= 0)
        {
            _logger.LogDebug("Ungültige NPDU verworfen.");
            return;
        }

        if ((function & BacnetNpduControls.NetworkLayerMessage) == BacnetNpduControls.NetworkLayerMessage)
        {
            _logger.LogDebug("Network-Layer-Nachricht verworfen.");
            return;
        }

        var offset = npduLength;
        var apduType = APDU.GetDecodedType(payload, offset);

        switch (apduType & BacnetPduTypes.PDU_TYPE_MASK)
        {
            case BacnetPduTypes.PDU_TYPE_UNCONFIRMED_SERVICE_REQUEST:
                ProcessUnconfirmedServiceRequest(payload, offset, remoteAddress);
                break;

            case BacnetPduTypes.PDU_TYPE_SIMPLE_ACK:
                ProcessSimpleAck(payload, offset, remoteAddress);
                break;

            case BacnetPduTypes.PDU_TYPE_COMPLEX_ACK:
                ProcessComplexAck(payload, offset, remoteAddress);
                break;

            case BacnetPduTypes.PDU_TYPE_ERROR:
                ProcessError(payload, offset, remoteAddress);
                break;

            case BacnetPduTypes.PDU_TYPE_REJECT:
            case BacnetPduTypes.PDU_TYPE_ABORT:
                ProcessAbort(payload, offset, remoteAddress);
                break;

            case BacnetPduTypes.PDU_TYPE_CONFIRMED_SERVICE_REQUEST:
                // Der Gateway ist ein reiner Client und beantwortet keine
                // Anfragen der Gegenstelle.
                _logger.LogInformation(
                    "Anfrage der Gegenstelle {Address} wird nicht beantwortet (reiner Client).",
                    remoteAddress);
                break;

            default:
                _logger.LogDebug("Unbekannter APDU-Typ {Type} verworfen.", apduType);
                break;
        }
    }

    /// <summary>
    /// Verarbeitet eine unbestätigte Dienstanfrage (z. B. IAm).
    /// </summary>
    private void ProcessUnconfirmedServiceRequest(byte[] payload, int offset, BacnetAddress remoteAddress)
    {
        var headerLength = APDU.DecodeUnconfirmedServiceRequest(payload, offset, out _, out var service);
        offset += headerLength;

        if (service != BacnetUnconfirmedServices.SERVICE_UNCONFIRMED_I_AM)
        {
            _logger.LogDebug("Unbestätigter Dienst {Service} wird ignoriert.", service);
            return;
        }

        // Das Legacy prüfte auf ">= 0"; DecodeIamBroadcast liefert im
        // Erfolgsfall 0 (Legacy-Eigenheit) und -1 bei einem Fehler.
        if (Services.DecodeIamBroadcast(
                payload,
                offset,
                out var deviceId,
                out var maxApdu,
                out var segmentation,
                out var vendorId) < 0)
        {
            _logger.LogWarning("IAm von {Address} konnte nicht dekodiert werden.", remoteAddress);
            return;
        }

        _logger.LogInformation(
            "IAm von {Address}: Device-ID {DeviceId}, max. APDU {MaxApdu}.",
            remoteAddress,
            deviceId,
            maxApdu);

        IamReceived?.Invoke(
            this,
            new BacnetIamEventArgs(remoteAddress, deviceId, maxApdu, segmentation, vendorId));
    }

    /// <summary>
    /// Verarbeitet eine SimpleAck-Antwort (z. B. nach WriteProperty).
    /// </summary>
    private void ProcessSimpleAck(byte[] payload, int offset, BacnetAddress remoteAddress)
    {
        APDU.DecodeSimpleAck(payload, offset, out _, out _, out var invokeId);
        Complete(invokeId, remoteAddress, []);
    }

    /// <summary>
    /// Verarbeitet eine ComplexAck-Antwort.
    /// </summary>
    private void ProcessComplexAck(byte[] payload, int offset, BacnetAddress remoteAddress)
    {
        var headerLength = APDU.DecodeComplexAck(
            payload,
            offset,
            out var type,
            out _,
            out var invokeId,
            out var sequenceNumber,
            out var proposedWindowNumber);

        var contentOffset = offset + headerLength;
        var contentLength = payload.Length - contentOffset;

        if ((type & BacnetPduTypes.SEGMENTED_MESSAGE) != 0)
        {
            ProcessSegment(payload, contentOffset, contentLength, type, invokeId, sequenceNumber, proposedWindowNumber, remoteAddress);
            return;
        }

        var content = new byte[contentLength];
        Array.Copy(payload, contentOffset, content, 0, contentLength);
        Complete(invokeId, remoteAddress, content);
    }

    /// <summary>
    /// Verarbeitet ein Teilstück einer segmentierten Antwort.
    /// </summary>
    private void ProcessSegment(
        byte[] payload,
        int contentOffset,
        int contentLength,
        BacnetPduTypes type,
        byte invokeId,
        byte sequenceNumber,
        byte proposedWindowNumber,
        BacnetAddress remoteAddress)
    {
        var first = sequenceNumber == 0 && _lastSequenceNumber == 0;

        if (!first && sequenceNumber != (byte)((_lastSequenceNumber + 1) & 0xFF))
        {
            _logger.LogWarning(
                "Segmentfolge außer der Reihenfolge (erwartet {Expected}, erhalten {Actual}).",
                (byte)((_lastSequenceNumber + 1) & 0xFF),
                sequenceNumber);
            return;
        }

        _lastSequenceNumber = sequenceNumber;

        var moreFollows = (type & BacnetPduTypes.MORE_FOLLOWS) == BacnetPduTypes.MORE_FOLLOWS;
        if (!moreFollows)
        {
            _lastSequenceNumber = 0;
        }

        // Die Gegenstelle erwartet eine SegmentAck-Bestätigung - sonst
        // sendet sie keine weiteren Segmente.
        if (proposedWindowNumber > 0 && (sequenceNumber % proposedWindowNumber) == 0 || !moreFollows)
        {
            _ = SendSegmentAckAsync(invokeId, sequenceNumber, proposedWindowNumber, remoteAddress);
        }

        var assembly = _segments.GetOrAdd(invokeId, _ => new SegmentAssembly());

        lock (assembly)
        {
            var part = new byte[contentLength];
            Array.Copy(payload, contentOffset, part, 0, contentLength);
            assembly.Parts.Add(part);
        }

        if (moreFollows)
        {
            return;
        }

        byte[] content;
        lock (assembly)
        {
            content = assembly.Parts.SelectMany(p => p).ToArray();
            assembly.Parts.Clear();
        }

        _segments.TryRemove(invokeId, out _);
        Complete(invokeId, remoteAddress, content);
    }

    /// <summary>
    /// Sendet eine SegmentAck-Bestätigung.
    /// </summary>
    private async Task SendSegmentAckAsync(
        byte invokeId,
        byte sequenceNumber,
        byte proposedWindowNumber,
        BacnetAddress remoteAddress)
    {
        try
        {
            var b = GetEncodeBuffer();
            NPDU.Encode(
                b,
                BacnetNpduControls.PriorityNormalMessage,
                remoteAddress.RoutedSource,
                null,
                DefaultHopCount,
                BacnetNetworkMessageTypes.NETWORK_MESSAGE_WHO_IS_ROUTER_TO_NETWORK,
                0);
            APDU.EncodeSegmentAck(
                b,
                BacnetPduTypes.PDU_TYPE_SEGMENT_ACK,
                invokeId,
                sequenceNumber,
                proposedWindowNumber);

            await _transport.SendAsync(
                b.buffer,
                _transport.HeaderLength,
                b.offset - _transport.HeaderLength,
                remoteAddress,
                waitForTransmission: false,
                timeout: 0,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SegmentAck konnte nicht gesendet werden.");
        }
    }

    /// <summary>
    /// Verarbeitet eine Error-Antwort der Gegenstelle.
    /// </summary>
    private void ProcessError(byte[] payload, int offset, BacnetAddress remoteAddress)
    {
        var headerLength = APDU.DecodeError(payload, offset, out _, out _, out var invokeId);
        offset += headerLength;

        var length = payload.Length - offset;
        if (length < 0)
        {
            length = 0;
        }

        if (Services.DecodeError(payload, offset, length, out var errorClass, out var errorCode) < 0)
        {
            _logger.LogWarning("Error-PDU von {Address} konnte nicht dekodiert werden.", remoteAddress);
            Fail(invokeId, remoteAddress, new BacnetDeviceErrorException(
                BacnetErrorClasses.ERROR_CLASS_DEVICE,
                BacnetErrorCodes.ERROR_CODE_OTHER));
            return;
        }

        Fail(invokeId, remoteAddress, new BacnetDeviceErrorException(errorClass, errorCode));
    }

    /// <summary>
    /// Verarbeitet eine Abort- oder Reject-Antwort der Gegenstelle.
    /// </summary>
    private void ProcessAbort(byte[] payload, int offset, BacnetAddress remoteAddress)
    {
        APDU.DecodeAbort(payload, offset, out _, out var invokeId, out var reason);
        Fail(invokeId, remoteAddress, new BacnetDeviceAbortException(reason));
    }

    /// <summary>
    /// Schließt eine wartende Anfrage erfolgreich ab.
    /// </summary>
    private void Complete(byte invokeId, BacnetAddress remoteAddress, byte[] content)
    {
        if (_pending.TryRemove(invokeId, out var tcs))
        {
            tcs.TrySetResult(content);
            return;
        }

        _logger.LogDebug(
            "Antwort mit Invoke-ID {InvokeId} von {Address} ohne passende Anfrage - verworfen.",
            invokeId,
            remoteAddress);
    }

    /// <summary>
    /// Schließt eine wartende Anfrage mit einem Fehler ab.
    /// </summary>
    private void Fail(byte invokeId, BacnetAddress remoteAddress, Exception exception)
    {
        if (_pending.TryRemove(invokeId, out var tcs))
        {
            tcs.TrySetException(exception);
            return;
        }

        _logger.LogDebug(
            "Fehlerantwort mit Invoke-ID {InvokeId} von {Address} ohne passende Anfrage - verworfen.",
            invokeId,
            remoteAddress);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        _transport.FrameReceived -= OnFrameReceived;
        _transport.Dispose();
    }

    /// <summary>
    /// Sammelt die Teilstücke einer segmentierten Antwort.
    /// </summary>
    private sealed class SegmentAssembly
    {
        public List<byte[]> Parts { get; } = [];
    }
}
