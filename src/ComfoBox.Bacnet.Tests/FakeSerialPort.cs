using ComfoBox.Bacnet.Serial;

namespace ComfoBox.Bacnet.Tests;

/// <summary>
/// In-Memory-Attrappe für einen seriellen Port.
/// </summary>
/// <remarks>
/// Damit lässt sich der MS/TP-Zustandsautomat ohne Hardware testen: Die
/// eingehenden Bytes werden aus einem Puffer geliefert, die geschriebenen
/// Bytes werden gesammelt.
/// </remarks>
public sealed class FakeSerialPort : ISerialPort
{
    private readonly Queue<byte> _inbound = new();
    private readonly List<byte> _outbound = [];
    private readonly object _sync = new();

    /// <summary>Wird beim Öffnen geworfen (simuliert einen fehlenden Port).</summary>
    public Exception? OpenException { get; set; }

    /// <summary>Der Port meldet beim Lesen einen Fehler (Befund A8).</summary>
    public bool FailOnRead { get; set; }

    /// <summary>Anzahl der Öffnungsversuche.</summary>
    public int OpenCount { get; private set; }

    /// <summary>Anzahl der Schließvorgänge.</summary>
    public int CloseCount { get; private set; }

    /// <summary>Anzahl der Lesevorgänge (Spin-Loop-Indikator).</summary>
    public int ReadCount { get; private set; }

    /// <inheritdoc />
    public string PortName { get; init; } = "FAKE";

    /// <inheritdoc />
    /// <remarks>
    /// Der Port gilt als **offen**, bis er geschlossen wird - so verhält sich
    /// der Attrapp wie ein echter Port, der schon geöffnet ist.
    /// </remarks>
    public bool IsOpen { get; private set; } = true;

    /// <summary>Alle geschriebenen Bytes.</summary>
    public byte[] Written
    {
        get
        {
            lock (_sync)
            {
                return _outbound.ToArray();
            }
        }
    }

    /// <summary>
    /// Legt Bytes in den Empfangspuffer.
    /// </summary>
    public void Enqueue(params byte[] data)
    {
        lock (_sync)
        {
            foreach (var b in data)
            {
                _inbound.Enqueue(b);
            }
        }
    }

    /// <inheritdoc />
    public void Open()
    {
        OpenCount++;

        if (OpenException is not null)
        {
            throw OpenException;
        }

        IsOpen = true;
    }

    /// <inheritdoc />
    public void Close()
    {
        CloseCount++;
        IsOpen = false;
    }

    /// <inheritdoc />
    public void Write(byte[] buffer, int offset, int length)
    {
        lock (_sync)
        {
            for (var i = 0; i < length; i++)
            {
                _outbound.Add(buffer[offset + i]);
            }
        }
    }

    /// <inheritdoc />
    public SerialReadResult Read(byte[] buffer, int offset, int length, int timeoutMs)
    {
        ReadCount++;

        if (FailOnRead)
        {
            return SerialReadResult.Error;
        }

        // Ein geschlossener Port meldet „Closed" - der echte Adapter
        // verhält sich genauso (IOException beim Lesen).
        if (!IsOpen)
        {
            return SerialReadResult.Closed;
        }

        lock (_sync)
        {
            if (_inbound.Count == 0)
            {
                return SerialReadResult.Timeout;
            }

            var read = 0;
            while (read < length && _inbound.Count > 0)
            {
                buffer[offset + read] = _inbound.Dequeue();
                read++;
            }

            return SerialReadResult.Data(read);
        }
    }

    /// <inheritdoc />
    public int BytesToRead
    {
        get
        {
            lock (_sync)
            {
                return _inbound.Count;
            }
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        Close();
    }
}
