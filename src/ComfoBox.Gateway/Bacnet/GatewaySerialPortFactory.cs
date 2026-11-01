using ComfoBox.Bacnet.Serial;

namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Die Fabrik des Gateways für den seriellen Port.
/// </summary>
/// <remarks>
/// <para>
/// Anders als die <see cref="SerialPortFactory"/> des BACnet-Projekts liest
/// diese Fabrik ihre Parameter **bei jedem Aufruf** aus den
/// <see cref="SerialPortSettings"/>. Damit bekommt jeder Neuaufbau des
/// Ports die aktuellen Werte - ein Portwechsel ist ohne Neustart möglich.
/// </para>
/// </remarks>
public sealed class GatewaySerialPortFactory : ISerialPortFactory
{
    private readonly SerialPortSettings _settings;
    private readonly int _timeoutMs;

    /// <summary>
    /// Erzeugt die Fabrik.
    /// </summary>
    /// <param name="settings">Die aktuellen Parameter der Schnittstelle.</param>
    /// <param name="timeoutMs">Das Lese-/Schreib-Timeout in Millisekunden.</param>
    public GatewaySerialPortFactory(SerialPortSettings settings, int timeoutMs)
    {
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentOutOfRangeException.ThrowIfNegative(timeoutMs);

        _settings = settings;
        _timeoutMs = timeoutMs;
    }

    /// <inheritdoc />
    public ISerialPort Create()
    {
        var options = new BacnetSerialOptions
        {
            PortName = _settings.PortName,
            BaudRate = _settings.BaudRate,
            ReadTimeoutMs = _timeoutMs,
            WriteTimeoutMs = _timeoutMs,
        };

        return new SerialPortAdapter(options);
    }
}
