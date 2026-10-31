using ComfoBox.Bacnet.Serial;

namespace ComfoBox.Bacnet;

/// <summary>
/// Optionen für den MS/TP-Transport.
/// </summary>
public sealed class BacnetMstpOptions
{
    /// <summary>
    /// MSTP-This-Station: die MAC-Adresse dieses Knotens (0..254).
    /// </summary>
    /// <remarks>
    /// Laut Gerätedokumentation ist der Wert <c>3</c> (<c>BacnetClientId</c>).
    /// Der Wert 255 ist als Broadcast reserviert und hier nicht zulässig.
    /// </remarks>
    public short ThisStation { get; init; } = 3;

    /// <summary>
    /// Höchste MAC-Adresse im Netzsegment. Bestimmt die Ring-Wraps.
    /// </summary>
    public byte MaxMaster { get; init; } = 127;

    /// <summary>
    /// Anzahl der Frames, die pro Token gesendet werden dürfen.
    /// </summary>
    public byte MaxInfoFrames { get; init; } = 1;

    /// <summary>
    /// Anzahl der Tokens, nach denen eine Poll-For-Master-Runde läuft.
    /// </summary>
    public byte MaxPoll { get; init; } = 50;

    /// <summary>
    /// Frame-Protokollierung auf Debug-Level.
    /// </summary>
    public bool StateLogging { get; init; }

    /// <summary>
    /// Serielle Parameter (Portname, Baudrate, Timeouts).
    /// </summary>
    public BacnetSerialOptions Serial { get; init; } = new();

    /// <summary>
    /// Wartezeit vor dem ersten Reconnect-Versuch nach einem Portfehler.
    /// </summary>
    public TimeSpan ReconnectDelay { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Obergrenze für den exponentiellen Reconnect-Backoff.
    /// </summary>
    public TimeSpan MaxReconnectDelay { get; init; } = TimeSpan.FromSeconds(30);
}
