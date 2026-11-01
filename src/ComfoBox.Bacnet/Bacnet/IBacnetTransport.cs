namespace ComfoBox.Bacnet;

/// <summary>
/// Transport-Schnittstelle des BACnet-Stacks.
/// </summary>
/// <remarks>
/// <para>
/// Die Schnittstelle bildet den Legacy-Transport
/// (<c>src_old/Bacnet/BACnetTransport.cs</c>, <c>IBacnetTransport</c>) ab. Da
/// ausschließlich MS/TP über eine serielle Schnittstelle unterstützt wird,
/// sind die UDP-, Pipe- und PTP-Varianten bewusst entfallen (Aufgabe 1.6).
/// </para>
/// <para>
/// Anders als im Legacy ist <see cref="SendAsync"/> asynchron: der Legacy
/// blockierte im Send-Aufruf bis zum Timeout (bis zu 30 s) und blockierte
/// damit den aufrufenden Thread.
/// </para>
/// </remarks>
public interface IBacnetTransport : IDisposable
{
    /// <summary>
    /// Wird ausgelöst, wenn ein Frame von der Gegenseite empfangen wurde.
    /// </summary>
    event EventHandler<BacnetFrameReceivedEventArgs>? FrameReceived;

    /// <summary>
    /// Art des Transports.
    /// </summary>
    BacnetAddressTypes Type { get; }

    /// <summary>
    /// Länge des Transport-Headers, der den Nutzdaten vorangestellt ist.
    /// </summary>
    int HeaderLength { get; }

    /// <summary>
    /// Maximale Puffergröße für einen Frame.
    /// </summary>
    int MaxBufferLength { get; }

    /// <summary>
    /// Maximale APDU-Länge der Gegenstelle.
    /// </summary>
    BacnetMaxAdpu MaxAdpuLength { get; }

    /// <summary>
    /// Anzahl der Frames, die pro Token gesendet werden dürfen.
    /// </summary>
    byte MaxInfoFrames { get; set; }

    /// <summary>
    /// Liefert die Broadcast-Adresse dieses Transports.
    /// </summary>
    BacnetAddress GetBroadcastAddress();

    /// <summary>
    /// Startet den Transport.
    /// </summary>
    void Start();

    /// <summary>
    /// Stoppt den Transport und gibt die Schnittstelle frei.
    /// </summary>
    Task StopAsync();

    /// <summary>
    /// Fordert einen Neuaufbau der Schnittstelle an.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Der Port wird geschlossen - die Hintergrundschleife öffnet ihn mit
    /// Backoff neu. Das ist der Weg, wenn der Adapter neu eingesteckt wurde
    /// oder die ComfoBox neu gestartet wurde.
    /// </para>
    /// <para>
    /// Der Aufruf ist threadsicher und kann jederzeit erfolgen.
    /// </para>
    /// </remarks>
    void RequestReconnect();

    /// <summary>
    /// Sendet Nutzdaten an die Gegenstelle.
    /// </summary>
    /// <param name="buffer">Puffer, aus dem gelesen wird.</param>
    /// <param name="offset">Anfang der Nutzdaten im Puffer.</param>
    /// <param name="dataLength">Länge der Nutzdaten.</param>
    /// <param name="address">Zieladresse.</param>
    /// <param name="waitForTransmission">
    /// Wenn <see langword="true"/>, wird gewartet, bis der Frame tatsächlich
    /// auf die Schnittstelle geschrieben wurde.
    /// </param>
    /// <param name="timeout">Wartezeit in Millisekunden.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Anzahl der gesendeten Bytes oder ein negativer Fehlercode.</returns>
    Task<int> SendAsync(
        byte[] buffer,
        int offset,
        int dataLength,
        BacnetAddress address,
        bool waitForTransmission,
        int timeout,
        CancellationToken cancellationToken);

    /// <summary>
    /// Wartet, bis alle anstehenden Frames übertragen wurden.
    /// </summary>
    /// <param name="timeout">Wartezeit in Millisekunden.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns><see langword="true"/>, wenn die Warteschlange leer wurde.</returns>
    Task<bool> WaitForAllTransmitsAsync(int timeout, CancellationToken cancellationToken);
}
