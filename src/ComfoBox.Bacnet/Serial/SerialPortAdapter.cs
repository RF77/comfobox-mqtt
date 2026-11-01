using System.IO.Ports;

namespace ComfoBox.Bacnet.Serial;

/// <summary>
/// Implementierung von <see cref="ISerialPort"/> auf Basis von
/// <see cref="System.IO.Ports.SerialPort"/>.
/// </summary>
/// <remarks>
/// Der Lesevorgang blockiert - der MS/TP-Zustandsautomat arbeitet auf einem
/// eigenen Hintergrund-Thread (siehe <c>bacnet.instructions.md</c> § 4). Das
/// Verhalten entspricht dem Legacy-Transport
/// (<c>src_old/Bacnet/BACnetTransport.cs</c>, <c>BacnetSerialPortTransport</c>):
/// <see cref="System.IO.Ports.SerialPort.ReadTimeout"/> wird vor jedem Lesevorgang
/// gesetzt, eine <see cref="TimeoutException"/> gilt als Zeitüberschreitung.
/// </remarks>
public sealed class SerialPortAdapter : ISerialPort
{
    private readonly SerialPort _port;

    /// <summary>
    /// Erzeugt einen Adapter für die angegebenen seriellen Parameter.
    /// </summary>
    public SerialPortAdapter(BacnetSerialOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        // Parität/Framesize/Stopbits sind laut Gerätedokumentation fest:
        // 8 Datenbits, keine Parität, 1 Stoppbit.
        _port = new SerialPort(options.PortName, options.BaudRate, Parity.None, 8, StopBits.One)
        {
            Handshake = Handshake.None,

            // Manche CH340-Adapter reagieren empfindlich auf DTR/RTS.
            DtrEnable = false,
            RtsEnable = false,
            ReadTimeout = options.ReadTimeoutMs,
            WriteTimeout = options.WriteTimeoutMs,
        };
    }

    /// <inheritdoc />
    public string PortName => _port.PortName;

    /// <inheritdoc />
    public bool IsOpen => _port.IsOpen;

    /// <inheritdoc />
    public void Open() => _port.Open();

    /// <inheritdoc />
    public void Close()
    {
        try
        {
            if (_port.IsOpen)
            {
                _port.Close();
            }
        }
        catch (IOException)
        {
            // Der Port ist bereits weg - für das Schließen unerheblich.
        }
        catch (UnauthorizedAccessException)
        {
            // Port wurde parallel entfernt.
        }
    }

    /// <inheritdoc />
    public void Write(byte[] buffer, int offset, int length) => _port.Write(buffer, offset, length);

    /// <inheritdoc />
    public SerialReadResult Read(byte[] buffer, int offset, int length, int timeoutMs)
    {
        if (!_port.IsOpen)
        {
            return SerialReadResult.Closed;
        }

        _port.ReadTimeout = timeoutMs;

        try
        {
            int read = _port.Read(buffer, offset, length);
            return read > 0 ? SerialReadResult.Data(read) : SerialReadResult.Closed;
        }
        catch (TimeoutException)
        {
            return SerialReadResult.Timeout;
        }
        catch (InvalidOperationException)
        {
            // Port wurde zwischenzeitlich geschlossen.
            return SerialReadResult.Closed;
        }
        catch (IOException)
        {
            // Adapter gezogen, Port weg oder Lesefehler.
            return SerialReadResult.Error;
        }
    }

    /// <inheritdoc />
    public int BytesToRead => _port.IsOpen ? _port.BytesToRead : 0;

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            _port.Dispose();
        }
        catch (IOException)
        {
            // Beim Dispose ist der Fehler unerheblich.
        }
    }
}
