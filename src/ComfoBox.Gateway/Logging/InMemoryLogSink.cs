using Serilog.Core;
using Serilog.Events;

namespace ComfoBox.Gateway.Logging;

/// <summary>
/// Serilog-Sink, die die letzten Log-Einträge im Speicher hält (Ringpuffer).
/// </summary>
/// <remarks>
/// <para>
/// Der Puffer ist die Grundlage für den Log-Viewer der Web-UI
/// (Diagnoseseite → Reiter „Protokoll"). Er hat einen festen Speicherbedarf
/// und wächst nicht.
/// </para>
/// <para>
/// Warum ein Array und keine <see cref="System.Collections.Generic.Queue{T}"/>?
/// Eine Queue kopiert beim Verwerfen des ältesten Elements. Ein festes Array mit
/// Modulo-Index hat konstanten Speicherbedarf und keine Allokation im Betrieb.
/// </para>
/// </remarks>
public sealed class InMemoryLogSink : ILogEventSink, IDisposable
{
    private readonly LogEntry?[] _buffer;
    private readonly LogEventLevel _minimumLevel;

    private int _writeIndex;
    private long _count;

    /// <summary>
    /// Erzeugt den Ringpuffer.
    /// </summary>
    /// <param name="capacity">Die Kapazität. <c>0</c> schaltet den Puffer ab.</param>
    /// <param name="minimumLevel">Der minimale Level, der gespeichert wird.</param>
    public InMemoryLogSink(int capacity, LogEventLevel minimumLevel)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(capacity);

        _buffer = new LogEntry?[capacity];
        _minimumLevel = minimumLevel;
    }

    /// <summary>
    /// Die Kapazität des Puffers.
    /// </summary>
    public int Capacity => _buffer.Length;

    /// <summary>
    /// Die Anzahl der bisher geschriebenen Einträge (kann größer sein als die Kapazität).
    /// </summary>
    public long Count => Interlocked.Read(ref _count);

    /// <summary>
    /// Wird ausgelöst, wenn ein neuer Eintrag aufgenommen wurde.
    /// </summary>
    /// <remarks>
    /// Der Live-Tail der Web-UI (<c>GET /api/diagnostics/logs/stream</c>)
    /// hört auf diesem Ereignis - so ist der Stream **ereignisgetrieben**
    /// und kein Polling.
    /// </remarks>
    public event EventHandler<LogEntry>? EntryAdded;

    /// <summary>
    /// Nimmt ein Log-Ereignis auf.
    /// </summary>
    /// <param name="logEvent">Das Ereignis.</param>
    /// <remarks>
    /// Ein Fehler im Sink darf den Gateway **nie** stoppen - deshalb wird
    /// jede Ausnahme geschluckt.
    /// </remarks>
    public void Emit(LogEvent logEvent)
    {
        try
        {
            if (_buffer.Length == 0 || logEvent.Level < _minimumLevel)
            {
                return;
            }

            var entry = LogEntry.From(logEvent);

            // Slot reservieren; bei Überlauf überschreibt der neueste Eintrag
            // den ältesten (Ringpuffer-Verhalten).
            var index = (int)(Interlocked.Increment(ref _writeIndex) - 1);
            if (index >= 0)
            {
                Volatile.Write(ref _buffer[index % _buffer.Length], entry);
            }

            Interlocked.Increment(ref _count);

            EntryAdded?.Invoke(this, entry);
        }
        catch (Exception)
        {
            // Ein kaputter Log-Eintrag darf den Prozess nicht beenden.
        }
    }

    /// <summary>
    /// Liefert die Einträge, neueste zuerst.
    /// </summary>
    /// <param name="limit">Die maximale Anzahl der Einträge.</param>
    public LogEntry[] Snapshot(int limit)
    {
        if (limit <= 0)
        {
            return [];
        }

        var total = Interlocked.Read(ref _count);
        if (total == 0 || _buffer.Length == 0)
        {
            return [];
        }

        var size = (int)Math.Min(total, _buffer.Length);
        var take = Math.Min(limit, size);
        var result = new LogEntry[take];

        var newest = Volatile.Read(ref _writeIndex) - 1;
        for (var i = 0; i < take; i++)
        {
            var index = ((newest - i) % _buffer.Length + _buffer.Length) % _buffer.Length;
            result[i] = Volatile.Read(ref _buffer[index])!;
        }

        return result;
    }

    /// <summary>
    /// Leert den Puffer.
    /// </summary>
    public void Clear()
    {
        Array.Clear(_buffer);
        Interlocked.Exchange(ref _writeIndex, 0);
        Interlocked.Exchange(ref _count, 0);
    }

    /// <summary>
    /// Der Puffer hält nichts - er wird nicht freigegeben.
    /// </summary>
    public void Dispose()
    {
    }
}
