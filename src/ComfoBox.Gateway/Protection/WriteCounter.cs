using System.Text.Json;
using System.Text.Json.Serialization;

namespace ComfoBox.Gateway.Protection;

/// <summary>
/// Der Zähler der Schreibvorgänge im gleitenden 24-h-Fenster.
/// </summary>
public interface IWriteCounter
{
    /// <summary>
    /// Die Anzahl der Schreibvorgänge im aktuellen Fenster.
    /// </summary>
    int Count { get; }

    /// <summary>
    /// Die maximale Anzahl der Schreibvorgänge im Fenster.
    /// </summary>
    /// <remarks><c>0</c> bedeutet: kein Limit.</remarks>
    int Limit { get; }

    /// <summary>
    /// Ist das Limit erreicht?
    /// </summary>
    bool IsLimitExceeded { get; }

    /// <summary>
    /// Reserviert einen Schreibvorgang.
    /// </summary>
    /// <returns>
    /// <c>false</c>, wenn das Limit erreicht ist - der Schreibvorgang muss dann
    /// abgelehnt werden.
    /// </returns>
    bool TryAcquire();

    /// <summary>
    /// Gibbt eine Reservierung wieder frei (der Schreibvorgang ist fehlgeschlagen).
    /// </summary>
    void Release();

    /// <summary>
    /// Liefert die aktuellen Kennzahlen.
    /// </summary>
    WriteCounterStats GetStats();

    /// <summary>
    /// Setzt den Zähler zurück (nur für Tests und Setup).
    /// </summary>
    void Reset();
}

/// <summary>
/// Die Kennzahlen des Schreibzählers.
/// </summary>
/// <param name="Count">Die Anzahl im Fenster.</param>
/// <param name="Limit">Das Limit.</param>
/// <param name="Oldest">Der älteste Schreibvorgang im Fenster.</param>
/// <param name="Newest">Der neueste Schreibvorgang im Fenster.</param>
public sealed record WriteCounterStats(int Count, int Limit, DateTimeOffset? Oldest, DateTimeOffset? Newest);

/// <summary>
/// Der persistente 24-h-Schreibzähler (Befund A3).
/// </summary>
/// <remarks>
/// <para>
/// Der Zähler ist der EEPROM-Schutz der ComfoBox. Er wird in einer JSON-Datei
/// im Datenverzeichnis gespeichert und übersteht einen Neustart.
/// </para>
/// <para>
/// Der Zähler wird **nur bei erfolgreichen** Schreibvorgängen erhöht
/// (Befund A22): Der Writer reserviert vor dem Schreiben und gibt die
/// Reservierung bei einem Fehler wieder frei.
/// </para>
/// </remarks>
public sealed class WriteCounter : IWriteCounter
{
    /// <summary>
    /// Der Name der Zählerdatei im Datenverzeichnis.
    /// </summary>
    public const string FileName = "write-counter.json";

    /// <summary>
    /// Das Standardlimit (EEPROM-Schutz der ComfoBox).
    /// </summary>
    public const int DefaultLimit = 200;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly string _filePath;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _window;
    private readonly int _limit;
    private readonly List<long> _writes = [];
    private readonly object _sync = new();

    /// <summary>
    /// Erzeugt den Zähler.
    /// </summary>
    /// <param name="filePath">Der Pfad der Zählerdatei.</param>
    /// <param name="limit">Das Limit (0 = kein Limit).</param>
    /// <param name="timeProvider">Die Zeitquelle (für Tests austauschbar).</param>
    /// <param name="window">Die Länge des Fensters (Standard: 24 h).</param>
    public WriteCounter(
        string filePath,
        int limit = DefaultLimit,
        TimeProvider? timeProvider = null,
        TimeSpan? window = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        ArgumentOutOfRangeException.ThrowIfNegative(limit);

        _filePath = filePath;
        _limit = limit;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _window = window ?? TimeSpan.FromHours(24);

        Load();
    }

    /// <inheritdoc />
    public int Count
    {
        get
        {
            lock (_sync)
            {
                Prune();
                return _writes.Count;
            }
        }
    }

    /// <inheritdoc />
    public int Limit => _limit;

    /// <inheritdoc />
    public bool IsLimitExceeded
    {
        get
        {
            if (_limit <= 0)
            {
                return false;
            }

            lock (_sync)
            {
                Prune();
                return _writes.Count >= _limit;
            }
        }
    }

    /// <inheritdoc />
    public bool TryAcquire()
    {
        lock (_sync)
        {
            Prune();

            if (_limit > 0 && _writes.Count >= _limit)
            {
                return false;
            }

            _writes.Add(_timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
            Persist();
            return true;
        }
    }

    /// <inheritdoc />
    public void Release()
    {
        lock (_sync)
        {
            if (_writes.Count > 0)
            {
                _writes.RemoveAt(_writes.Count - 1);
                Persist();
            }
        }
    }

    /// <inheritdoc />
    public WriteCounterStats GetStats()
    {
        lock (_sync)
        {
            Prune();

            DateTimeOffset? oldest = null;
            DateTimeOffset? newest = null;

            if (_writes.Count > 0)
            {
                oldest = DateTimeOffset.FromUnixTimeMilliseconds(_writes[0]);
                newest = DateTimeOffset.FromUnixTimeMilliseconds(_writes[^1]);
            }

            return new WriteCounterStats(_writes.Count, _limit, oldest, newest);
        }
    }

    /// <inheritdoc />
    public void Reset()
    {
        lock (_sync)
        {
            _writes.Clear();
            Persist();
        }
    }

    /// <summary>
    /// Entfernt Einträge, die aus dem Fenster gefallen sind.
    /// </summary>
    private void Prune()
    {
        var cutoff = (_timeProvider.GetUtcNow() - _window).ToUnixTimeMilliseconds();

        var index = 0;
        while (index < _writes.Count && _writes[index] < cutoff)
        {
            index++;
        }

        if (index > 0)
        {
            _writes.RemoveRange(0, index);
        }
    }

    /// <summary>
    /// Lädt den Zähler aus der Datei.
    /// </summary>
    private void Load()
    {
        if (!File.Exists(_filePath))
        {
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var state = JsonSerializer.Deserialize<CounterState>(json, JsonOptions);
            if (state?.Writes is not null)
            {
                _writes.AddRange(state.Writes);
                Prune();
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Eine kaputte Zählerdatei darf den Start nicht verhindern - im
            // schlimmsten Fall wird der Zähler neu begonnen.
            _writes.Clear();
        }
    }

    /// <summary>
    /// Schreibt den Zähler **atomar** in die Datei.
    /// </summary>
    private void Persist()
    {
        var directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var state = new CounterState { Writes = [.. _writes] };
        var json = JsonSerializer.Serialize(state, JsonOptions);

        var temporary = _filePath + ".tmp";
        File.WriteAllText(temporary, json);
        File.Move(temporary, _filePath, overwrite: true);
    }

    /// <summary>
    /// Der Inhalt der Zählerdatei.
    /// </summary>
    private sealed class CounterState
    {
        /// <summary>
        /// Die Zeitpunkte der Schreibvorgänge (Unix-Millisekunden).
        /// </summary>
        public List<long> Writes { get; set; } = [];
    }
}
