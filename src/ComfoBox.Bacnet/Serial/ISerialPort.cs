namespace ComfoBox.Bacnet.Serial;

/// <summary>
/// Abstraktion über einen seriellen Port.
/// </summary>
/// <remarks>
/// Der Legacy-Stack hatte dafür <c>IBacnetSerialTransport</c> in
/// <c>src_old/Bacnet/BACnetTransport.cs</c>. Die Schnittstelle ist bewusst
/// schmal gehalten, damit der MSTP-Zustandsautomat ohne echte Hardware
/// getestet werden kann (der Adapter liefert dann Bytes aus dem Speicher).
/// </remarks>
public interface ISerialPort : IDisposable
{
    /// <summary>Ist der Port aktuell geöffnet?</summary>
    bool IsOpen { get; }

    /// <summary>Öffnet den Port. Wirft bei einem Fehler.</summary>
    void Open();

    /// <summary>Schließt den Port. Fehler werden verschluckt.</summary>
    void Close();

    /// <summary>
    /// Schreibt Daten auf den Port.
    /// </summary>
    void Write(byte[] buffer, int offset, int length);

    /// <summary>
    /// Liest Daten vom Port und wartet höchstens <paramref name="timeoutMs"/> Millisekunden.
    /// </summary>
    /// <remarks>
    /// Der Aufruf blockiert - der MSTP-Zustandsautomat arbeitet auf einem
    /// eigenen Hintergrund-Thread (siehe <c>bacnet.instructions.md</c> § 4).
    /// </remarks>
    SerialReadResult Read(byte[] buffer, int offset, int length, int timeoutMs);

    /// <summary>Anzahl der Bytes, die aktuell im Empfangspuffer liegen.</summary>
    int BytesToRead { get; }
}
