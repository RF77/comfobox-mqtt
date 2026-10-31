using ComfoBox.Bacnet.Serialize;

namespace ComfoBox.Bacnet;

/// <summary>
/// Ein Frame, der vom MS/TP-Zustandsautomat auf die serielle Schnittstelle
/// geschrieben werden soll.
/// </summary>
internal sealed class MstpOutboundFrame
{
    /// <summary>
    /// Erzeugt einen Frame.
    /// </summary>
    /// <param name="frameType">Frame-Typ (Token, Poll-For-Master, BACnet-Daten …).</param>
    /// <param name="destinationAddress">MAC-Adresse des Empfängers.</param>
    /// <param name="data">
    /// Puffer mit reserviertem Platz für den MS/TP-Header (8 Byte) und die
    /// Nutzdaten. <see langword="null"/> für Frames ohne Nutzdaten (Token, PFM …).
    /// </param>
    /// <param name="dataLength">Länge der Nutzdaten.</param>
    public MstpOutboundFrame(
        BacnetMstpFrameTypes frameType,
        byte destinationAddress,
        byte[]? data,
        int dataLength)
    {
        FrameType = frameType;
        DestinationAddress = destinationAddress;
        Data = data;
        DataLength = dataLength;
    }

    /// <summary>Frame-Typ.</summary>
    public BacnetMstpFrameTypes FrameType { get; }

    /// <summary>MAC-Adresse des Empfängers.</summary>
    public byte DestinationAddress { get; }

    /// <summary>Puffer (Header-Platz + Nutzdaten) oder <see langword="null"/>.</summary>
    public byte[]? Data { get; }

    /// <summary>Länge der Nutzdaten.</summary>
    public int DataLength { get; }

    /// <summary>
    /// Wird signalisiert, sobald der Frame tatsächlich auf die Schnittstelle
    /// geschrieben wurde.
    /// </summary>
    public TaskCompletionSource<bool> Sent { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}
