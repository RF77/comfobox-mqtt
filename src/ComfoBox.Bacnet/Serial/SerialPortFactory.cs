namespace ComfoBox.Bacnet.Serial;

/// <summary>
/// Standardimplementierung von <see cref="ISerialPortFactory"/>.
/// </summary>
/// <remarks>
/// Die Factory ist der einzige Ort, der <c>System.IO.Ports</c> an das
/// BACnet-Projekt bindet. Tests injizieren stattdessen eine eigene
/// Implementierung (siehe <c>ComfoBox.Bacnet.Tests</c>).
/// </remarks>
public sealed class SerialPortFactory : ISerialPortFactory
{
    private readonly BacnetSerialOptions _options;

    /// <summary>
    /// Erzeugt die Factory für die angegebenen seriellen Parameter.
    /// </summary>
    public SerialPortFactory(BacnetSerialOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options;
    }

    /// <inheritdoc />
    public ISerialPort Create() => new SerialPortAdapter(_options);
}
