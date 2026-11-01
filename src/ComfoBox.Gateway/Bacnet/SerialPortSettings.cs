namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Die zur Laufzeit geltenden Parameter der seriellen Schnittstelle.
/// </summary>
/// <remarks>
/// <para>
/// Diese Klasse ist der einzige Ort, an dem der **aktuelle** Port und die
/// aktuelle Baudrate stehen. Die Werte kommen beim Start aus der
/// Konfiguration und können später zur Laufzeit geändert werden (Portwahl
/// in der Oberfläche) - ohne dass der Gateway neu gestartet werden muss.
/// </para>
/// <para>
/// Der MS/TP-Transport liest die Werte beim Öffnen des Ports - der Port wird
/// also mit genau den Parametern geöffnet, die hier stehen.
/// </para>
/// </remarks>
public sealed class SerialPortSettings
{
    private string _portName;
    private int _baudRate;

    /// <summary>
    /// Erzeugt die Einstellungen.
    /// </summary>
    /// <param name="portName">Der Name des Ports, z. B. <c>COM3</c>.</param>
    /// <param name="baudRate">Die Baudrate.</param>
    public SerialPortSettings(string portName, int baudRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        ArgumentOutOfRangeException.ThrowIfNegative(baudRate);

        _portName = portName;
        _baudRate = baudRate;
    }

    /// <summary>
    /// Der Name des Ports, z. B. <c>COM3</c> oder <c>/dev/ttyUSB0</c>.
    /// </summary>
    public string PortName => Volatile.Read(ref _portName);

    /// <summary>
    /// Die Baudrate des Ports.
    /// </summary>
    public int BaudRate => Volatile.Read(ref _baudRate);

    /// <summary>
    /// Setzt die neuen Parameter.
    /// </summary>
    /// <param name="portName">Der Name des Ports.</param>
    /// <param name="baudRate">Die Baudrate.</param>
    public void Update(string portName, int baudRate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(portName);
        ArgumentOutOfRangeException.ThrowIfNegative(baudRate);

        Volatile.Write(ref _portName, portName);
        Volatile.Write(ref _baudRate, baudRate);
    }
}
