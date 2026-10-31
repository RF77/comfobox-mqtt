namespace ComfoBox.Bacnet.Serial;

/// <summary>
/// Erzeugt serielle Ports.
/// </summary>
/// <remarks>
/// Die Schnittstelle erlaubt es, dem Transport in Tests eine Attrappe
/// statt eines echten <c>System.IO.Ports.SerialPort</c> zu geben.
/// </remarks>
public interface ISerialPortFactory
{
    /// <summary>
    /// Erzeugt einen neuen seriellen Port. Der Port ist zunächst geschlossen.
    /// </summary>
    ISerialPort Create();
}
