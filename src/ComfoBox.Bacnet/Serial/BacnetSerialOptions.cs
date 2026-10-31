namespace ComfoBox.Bacnet.Serial;

/// <summary>
/// Serielle Parameter für den MS/TP-Bus.
/// </summary>
public sealed class BacnetSerialOptions
{
    /// <summary>
    /// Name des seriellen Ports (z. B. <c>COM3</c> oder <c>/dev/serial/by-id/usb-…</c>).
    /// </summary>
    /// <remarks>
    /// Unter Linux ist der Pfad <c>/dev/ttyUSB0</c> nicht stabil - der
    /// udev-Symlink unter <c>/dev/serial/by-id/</c> ist zu verwenden.
    /// </remarks>
    public string PortName { get; init; } = string.Empty;

    /// <summary>
    /// Baudrate. Der ComfoBox muss auf <c>38400</c> umgestellt sein.
    /// </summary>
    public int BaudRate { get; init; } = 38400;

    /// <summary>
    /// Lese-Timeout in Millisekunden.
    /// </summary>
    public int ReadTimeoutMs { get; init; } = 1000;

    /// <summary>
    /// Schreib-Timeout in Millisekunden.
    /// </summary>
    public int WriteTimeoutMs { get; init; } = 1000;
}
