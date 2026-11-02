namespace ComfoBox.Bacnet;

/// <summary>
/// Die Auswertung einer MS/TP-Beobachtung.
/// </summary>
/// <param name="StartedAt">Der Beginn der Beobachtung.</param>
/// <param name="DurationSeconds">Die Dauer der Beobachtung in Sekunden.</param>
/// <param name="TotalEvents">Die Anzahl aller Ereignisse.</param>
/// <param name="Counters">Die Zähler je Ereignisart.</param>
/// <param name="StationsSeen">Die Stationen, die sich gemeldet haben.</param>
/// <param name="Events">Die Ereignisse (älteste zuerst).</param>
public sealed record MstpEventSnapshot(
    DateTimeOffset StartedAt,
    double DurationSeconds,
    long TotalEvents,
    IReadOnlyDictionary<MstpEventKind, long> Counters,
    IReadOnlyList<byte> StationsSeen,
    IReadOnlyList<MstpEvent> Events);

/// <summary>
/// Sammelt die Ereignisse des MS/TP-Zustandsautomaten.
/// </summary>
/// <remarks>
/// <para>
/// Der Schreiber (der Zustandsautomat) läuft im Hot-Path des Tokens.
/// Deshalb ist die Schreibseite bewusst so einfach wie möglich:
/// </para>
/// <list type="bullet">
/// <item>Die Ereignisse liegen in **parallelen Primitive-Arrays** - kein
/// <c>new</c>, keine Strukturkopie, keine Sperre.</item>
/// <item>Der Zeitstempel ist monoton (<see cref="Environment.TickCount64"/>);
/// die Umrechnung in UTC passiert erst beim Auslesen.</item>
/// <item>Kein Logging, keine Dateizugriffe, keine Netzwerkzugriffe.</item>
/// </list>
/// <para>
/// Die Auswertung (Log-Ausgabe, HTTP-Antwort) passiert auf einem anderen
/// Thread - der Zustandsautomat wird dabei **nie** unterbrochen.
/// </para>
/// </remarks>
public sealed class MstpEventRecorder : IMstpObserver
{
    /// <summary>
    /// Die Standardkapazität des Ringpuffers.
    /// </summary>
    public const int DefaultCapacity = 512;

    private readonly byte[] _kinds;
    private readonly byte[] _stations;
    private readonly byte[] _frameTypes;
    private readonly long[] _ticks;
    private readonly long[] _counters = new long[(int)MstpEventKind.Count];
    private readonly bool[] _stationsSeen = new bool[256];

    private int _writeIndex;
    private long _total;
    private long _startTicks;
    private DateTimeOffset _startedAt;

    /// <summary>
    /// Erzeugt den Rekorder.
    /// </summary>
    /// <param name="capacity">Die Kapazität des Ringpuffers.</param>
    public MstpEventRecorder(int capacity = DefaultCapacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 16);

        _kinds = new byte[capacity];
        _stations = new byte[capacity];
        _frameTypes = new byte[capacity];
        _ticks = new long[capacity];

        _startedAt = DateTimeOffset.UtcNow;
        _startTicks = Environment.TickCount64;
    }

    /// <summary>
    /// Beginnt eine neue Beobachtung: die Zähler und der Ringpuffer werden
    /// geleert.
    /// </summary>
    public void Reset()
    {
        Array.Clear(_kinds);
        Array.Clear(_stations);
        Array.Clear(_frameTypes);
        Array.Clear(_ticks);
        Array.Clear(_counters);
        Array.Clear(_stationsSeen);

        _writeIndex = 0;
        Volatile.Write(ref _total, 0);
        _startedAt = DateTimeOffset.UtcNow;
        _startTicks = Environment.TickCount64;
    }

    /// <inheritdoc />
    /// <remarks>
    /// Diese Methode läuft im Hot-Path des Zustandsautomaten. Sie darf
    /// weder allokieren noch loggen noch sperren.
    /// </remarks>
    public void OnMstpEvent(MstpEventKind kind, byte station, byte frameType)
    {
        var index = _writeIndex;

        _kinds[index] = (byte)kind;
        _stations[index] = station;
        _frameTypes[index] = frameType;
        _ticks[index] = Environment.TickCount64;

        _writeIndex = index + 1 == _kinds.Length ? 0 : index + 1;
        _counters[(int)kind]++;
        _stationsSeen[station] = true;

        Volatile.Write(ref _total, _total + 1);
    }

    /// <summary>
    /// Liest die aktuelle Beobachtung aus.
    /// </summary>
    /// <param name="maxEvents">Die Höchstzahl der zurückgegebenen Ereignisse.</param>
    /// <returns>Die Auswertung.</returns>
    public MstpEventSnapshot Snapshot(int maxEvents = 200)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxEvents);

        var total = Volatile.Read(ref _total);
        var writeIndex = Volatile.Read(ref _writeIndex);

        // Die Ereignisse liegen im Ringpuffer. Es wird nur der Teil
        // gelesen, der zwischen dem ältesten und dem neuesten Eintrag liegt.
        var available = (int)Math.Min(total, (long)_kinds.Length);
        var take = Math.Min(available, maxEvents);

        var events = new List<MstpEvent>(take);
        var first = writeIndex - take;

        for (var i = 0; i < take; i++)
        {
            var index = first + i;

            if (index < 0)
            {
                index += _kinds.Length;
            }

            events.Add(new MstpEvent(
                (MstpEventKind)_kinds[index],
                _stations[index],
                _frameTypes[index],
                ToUtc(_ticks[index])));
        }

        var counters = new Dictionary<MstpEventKind, long>();

        for (var kind = 0; kind < (int)MstpEventKind.Count; kind++)
        {
            counters[(MstpEventKind)kind] = Volatile.Read(ref _counters[kind]);
        }

        var stations = new List<byte>();

        for (var station = 0; station < _stationsSeen.Length; station++)
        {
            if (_stationsSeen[station])
            {
                stations.Add((byte)station);
            }
        }

        return new MstpEventSnapshot(
            _startedAt,
            (Environment.TickCount64 - _startTicks) / 1000d,
            total,
            counters,
            stations,
            events);
    }

    /// <summary>
    /// Rechnet einen monotonen Zeitstempel in UTC um.
    /// </summary>
    /// <param name="ticks">Der monotone Zeitstempel in Millisekunden.</param>
    /// <returns>Der Zeitpunkt in UTC.</returns>
    private DateTimeOffset ToUtc(long ticks)
    {
        var offsetTicks = (ticks - _startTicks) * TimeSpan.TicksPerMillisecond;

        return new DateTimeOffset(_startedAt.UtcTicks + offsetTicks, TimeSpan.Zero);
    }
}
