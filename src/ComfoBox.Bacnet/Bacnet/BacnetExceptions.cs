namespace ComfoBox.Bacnet;

/// <summary>
/// Basisklasse aller BACnet-Fehler.
/// </summary>
/// <remarks>
/// Der Legacy-Stack hat alle Fehler als <see cref="Exception"/> mit
/// Textnachrichten geworfen ("Abort from device: 3", "Error from device: …").
/// Damit konnte die aufrufende Schicht Fehler nicht unterscheiden. Der neue
/// Stack wirft typisierte Exceptions.
/// </remarks>
public class BacnetException : Exception
{
    /// <summary>
    /// Erzeugt eine BACnet-Ausnahme.
    /// </summary>
    public BacnetException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Erzeugt eine BACnet-Ausnahme mit innerer Ausnahme.
    /// </summary>
    public BacnetException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Die Gegenstelle hat innerhalb des Zeitfensters nicht geantwortet.
/// </summary>
public sealed class BacnetTimeoutException : BacnetException
{
    /// <summary>
    /// Erzeugt eine Timeout-Ausnahme.
    /// </summary>
    public BacnetTimeoutException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Erzeugt eine Timeout-Ausnahme mit innerer Ausnahme.
    /// </summary>
    public BacnetTimeoutException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

/// <summary>
/// Die Gegenstelle hat mit einem BACnet-Error-PDU geantwortet.
/// </summary>
public sealed class BacnetDeviceErrorException : BacnetException
{
    /// <summary>
    /// Erzeugt eine Fehler-Ausnahme der Gegenstelle.
    /// </summary>
    /// <param name="errorClass">Fehlerklasse laut BACnet.</param>
    /// <param name="errorCode">Fehlercode laut BACnet.</param>
    public BacnetDeviceErrorException(BacnetErrorClasses errorClass, BacnetErrorCodes errorCode)
        : base($"BACnet-Fehler der Gegenstelle: {errorClass} / {errorCode}.")
    {
        ErrorClass = errorClass;
        ErrorCode = errorCode;
    }

    /// <summary>Fehlerklasse laut BACnet.</summary>
    public BacnetErrorClasses ErrorClass { get; }

    /// <summary>Fehlercode laut BACnet.</summary>
    public BacnetErrorCodes ErrorCode { get; }
}

/// <summary>
/// Die Gegenstelle hat den Request mit einem Abort-PDU abgebrochen.
/// </summary>
public sealed class BacnetDeviceAbortException : BacnetException
{
    /// <summary>
    /// Erzeugt eine Abort-Ausnahme der Gegenstelle.
    /// </summary>
    /// <param name="reason">Grund des Abbruchs laut BACnet.</param>
    public BacnetDeviceAbortException(byte reason)
        : base($"BACnet-Abbruch der Gegenstelle (Grund {reason}).")
    {
        Reason = reason;
    }

    /// <summary>Grund des Abbruchs laut BACnet.</summary>
    public byte Reason { get; }
}

/// <summary>
/// Eine empfangene Antwort konnte nicht dekodiert werden.
/// </summary>
public sealed class BacnetDecodeException : BacnetException
{
    /// <summary>
    /// Erzeugt eine Dekodier-Ausnahme.
    /// </summary>
    public BacnetDecodeException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Erzeugt eine Dekodier-Ausnahme mit innerer Ausnahme.
    /// </summary>
    public BacnetDecodeException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
