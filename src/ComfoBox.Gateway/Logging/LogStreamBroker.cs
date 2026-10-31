using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ComfoBox.Gateway.Logging;

/// <summary>
/// Die Verteilung der neuen Log-Einträge an die SSE-Abonnenten.
/// </summary>
/// <remarks>
/// <para>
/// Der Broker hört auf dem Ringpuffer und schreibt jeden neuen Eintrag in
/// die Kanäle der Abonnenten. Ein langsamer Client darf den Log-Pfad **nicht**
/// blockieren - deshalb ist der Kanal begrenzt und verwirft die ältesten
/// Einträge (<c>DropOldest</c>).
/// </para>
/// <para>
/// Die Anzahl der gleichzeitigen Streams ist begrenzt: ein Client, der den
/// Stream abbricht und wieder öffnet, darf keine unbegrenzten Kanäle
/// hinterlassen.
/// </para>
/// </remarks>
public sealed class LogStreamBroker : IDisposable
{
    /// <summary>
    /// Die maximale Anzahl gleichzeitiger Log-Streams.
    /// </summary>
    public const int MaxStreams = 5;

    /// <summary>
    /// Die Kapazität des Kanals je Abonnent.
    /// </summary>
    public const int ChannelCapacity = 256;

    private readonly InMemoryLogSink _sink;
    private readonly List<Channel<LogEntry>> _subscribers = [];
    private readonly object _sync = new();

    /// <summary>
    /// Erzeugt den Broker.
    /// </summary>
    /// <param name="sink">Der Ringpuffer.</param>
    public LogStreamBroker(InMemoryLogSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);

        _sink = sink;
        _sink.EntryAdded += OnEntryAdded;
    }

    /// <summary>
    /// Die Anzahl der aktiven Streams.
    /// </summary>
    public int ActiveStreams
    {
        get
        {
            lock (_sync)
            {
                return _subscribers.Count;
            }
        }
    }

    /// <summary>
    /// Meldet einen neuen Abonnenten an.
    /// </summary>
    /// <param name="stream">Der Strom (bei <c>false</c> <c>null</c>).</param>
    /// <returns>
    /// <c>false</c>, wenn die maximale Anzahl Streams erreicht ist.
    /// </returns>
    /// <remarks>
    /// Der Abonnent meldet sich automatisch ab, wenn die Enumeration endet
    /// (der Client hat die Verbindung getrennt).
    /// </remarks>
    public bool TrySubscribe(out IAsyncEnumerable<LogEntry>? stream)
    {
        var channel = Channel.CreateBounded<LogEntry>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true,
            SingleWriter = false,
        });

        lock (_sync)
        {
            if (_subscribers.Count >= MaxStreams)
            {
                stream = null;
                return false;
            }

            _subscribers.Add(channel);
        }

        stream = ReadAsync(channel);
        return true;
    }

    /// <summary>
    /// Liest die Einträge eines Abonnenten.
    /// </summary>
    private async IAsyncEnumerable<LogEntry> ReadAsync(
        Channel<LogEntry> channel,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        try
        {
            while (await channel.Reader.WaitToReadAsync(cancellationToken).ConfigureAwait(false))
            {
                while (channel.Reader.TryRead(out var entry))
                {
                    yield return entry;
                }
            }
        }
        finally
        {
            lock (_sync)
            {
                _subscribers.Remove(channel);
            }
        }
    }

    /// <summary>
    /// Verteilt einen neuen Eintrag an alle Abonnenten.
    /// </summary>
    private void OnEntryAdded(object? sender, LogEntry entry)
    {
        lock (_sync)
        {
            foreach (var subscriber in _subscribers)
            {
                // Nicht blockierend: ein langsamer Client verliert Einträge,
                // statt den Log-Pfad aufzuhalten.
                subscriber.Writer.TryWrite(entry);
            }
        }
    }

    /// <summary>
    /// Beendet alle Streams und meldet den Broker ab.
    /// </summary>
    public void Dispose()
    {
        _sink.EntryAdded -= OnEntryAdded;

        lock (_sync)
        {
            foreach (var subscriber in _subscribers)
            {
                subscriber.Writer.TryComplete();
            }

            _subscribers.Clear();
        }
    }
}
