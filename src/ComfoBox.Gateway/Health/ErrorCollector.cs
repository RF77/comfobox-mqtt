namespace ComfoBox.Gateway.Health;

/// <summary>
/// Ein aufgetretener Fehler.
/// </summary>
/// <param name="Timestamp">Der Zeitpunkt.</param>
/// <param name="Severity">Der Schweregrad, z. B. <c>warning</c> oder <c>error</c>.</param>
/// <param name="Source">Die Quelle, z. B. <c>Bacnet</c>.</param>
/// <param name="Code">Der Fehlercode, z. B. <c>timeout</c>.</param>
/// <param name="Message">Die Meldung.</param>
/// <param name="Detail">Weitere Details (oder <c>null</c>).</param>
public sealed record ErrorEntry(
    DateTimeOffset Timestamp,
    string Severity,
    string Source,
    string Code,
    string Message,
    string? Detail);

/// <summary>
/// Die Fehler-Senke: die letzten Fehler des Gateways.
/// </summary>
/// <remarks>
/// <para>
/// Jeder Fehler landet in **drei** Kanälen: Log, <c>ComfoBox/Status/Errors</c>
/// und die UI. Dieser Puffer ist die Grundlage für die beiden letzten.
/// </para>
/// <para>
/// Abgrenzung: der Fehler-Puffer (50 Einträge) und der Log-Puffer
/// (1000 Einträge) sind zwei verschiedene Dinge.
/// </para>
/// </remarks>
public interface IErrorCollector
{
    /// <summary>
    /// Nimmt einen Fehler auf.
    /// </summary>
    /// <param name="entry">Der Fehler.</param>
    void Report(ErrorEntry entry);

    /// <summary>
    /// Nimmt einen Fehler auf.
    /// </summary>
    /// <param name="source">Die Quelle, z. B. <c>Bacnet</c>.</param>
    /// <param name="code">Der Fehlercode, z. B. <c>timeout</c>.</param>
    /// <param name="message">Die Meldung.</param>
    /// <param name="exception">Die Ausnahme (oder <c>null</c>).</param>
    /// <param name="severity">Der Schweregrad (Standard: <c>error</c>).</param>
    void Report(string source, string code, string message, Exception? exception = null, string severity = "error");

    /// <summary>
    /// Liefert die letzten Fehler, neueste zuerst.
    /// </summary>
    ErrorEntry[] Snapshot();

    /// <summary>
    /// Leert die Senke.
    /// </summary>
    void Clear();
}

/// <summary>
/// Der Fehler-Ringpuffer des Gateways.
/// </summary>
public sealed class ErrorCollector : IErrorCollector
{
    /// <summary>
    /// Die Anzahl der gehaltenen Fehler.
    /// </summary>
    public const int Capacity = 50;

    private readonly ErrorEntry?[] _buffer = new ErrorEntry?[Capacity];
    private readonly TimeProvider _timeProvider;
    private int _writeIndex;
    private long _count;

    /// <summary>
    /// Erzeugt die Fehler-Senke.
    /// </summary>
    /// <param name="timeProvider">Die Zeitquelle (für Tests austauschbar).</param>
    public ErrorCollector(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public void Report(ErrorEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var index = (int)(Interlocked.Increment(ref _writeIndex) - 1);
        if (index >= 0)
        {
            Volatile.Write(ref _buffer[index % Capacity], entry);
        }

        Interlocked.Increment(ref _count);
    }

    /// <inheritdoc />
    public void Report(string source, string code, string message, Exception? exception = null, string severity = "error")
        => Report(new ErrorEntry(
            _timeProvider.GetUtcNow(),
            severity,
            source,
            code,
            message,
            exception?.ToString()));

    /// <inheritdoc />
    public ErrorEntry[] Snapshot()
    {
        var total = Interlocked.Read(ref _count);
        if (total == 0)
        {
            return [];
        }

        var size = (int)Math.Min(total, Capacity);
        var result = new ErrorEntry[size];

        var newest = Volatile.Read(ref _writeIndex) - 1;
        for (var i = 0; i < size; i++)
        {
            var index = ((newest - i) % Capacity + Capacity) % Capacity;
            result[i] = Volatile.Read(ref _buffer[index])!;
        }

        return result;
    }

    /// <inheritdoc />
    public void Clear()
    {
        Array.Clear(_buffer);
        Interlocked.Exchange(ref _writeIndex, 0);
        Interlocked.Exchange(ref _count, 0);
    }
}
