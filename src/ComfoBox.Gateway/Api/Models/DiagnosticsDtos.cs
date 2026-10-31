using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Logging;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Ein Eintrag der Fehlerhistorie.
/// </summary>
public sealed record ErrorEntryDto
{
    /// <summary>
    /// Der Zeitpunkt des Fehlers.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Der Schweregrad: <c>info</c>, <c>warning</c> oder <c>error</c>.
    /// </summary>
    public required string Severity { get; init; }

    /// <summary>
    /// Die Quelle, z. B. <c>Bacnet</c>.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Der Fehlercode, z. B. <c>timeout</c>.
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Die Meldung.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Weitere Details (oder <c>null</c>).
    /// </summary>
    public string? Detail { get; init; }

    /// <summary>
    /// Erzeugt die Beschreibung eines Fehlers.
    /// </summary>
    /// <param name="entry">Der Fehler.</param>
    /// <returns>Die Beschreibung.</returns>
    public static ErrorEntryDto From(ErrorEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new ErrorEntryDto
        {
            Timestamp = entry.Timestamp,
            Severity = entry.Severity,
            Source = entry.Source,
            Code = entry.Code,
            Message = entry.Message,
            Detail = entry.Detail,
        };
    }
}

/// <summary>
/// Ein Eintrag des Log-Ringpuffers.
/// </summary>
public sealed record LogEntryDto
{
    /// <summary>
    /// Der Zeitpunkt des Eintrags.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Der Level, z. B. <c>Information</c>.
    /// </summary>
    public required string Level { get; init; }

    /// <summary>
    /// Die Logger-Kategorie, z. B. <c>ComfoBox.Bacnet.BacnetClient</c>.
    /// </summary>
    public required string Source { get; init; }

    /// <summary>
    /// Die gerenderte Meldung.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Der Stacktrace (oder <c>null</c>).
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Die strukturierten Properties des Eintrags.
    /// </summary>
    public required IReadOnlyDictionary<string, string> Properties { get; init; }

    /// <summary>
    /// Erzeugt die Beschreibung eines Log-Eintrags.
    /// </summary>
    /// <param name="entry">Der Eintrag.</param>
    /// <returns>Die Beschreibung.</returns>
    public static LogEntryDto From(LogEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new LogEntryDto
        {
            Timestamp = entry.Timestamp,
            Level = entry.Level,
            Source = entry.Source,
            Message = entry.Message,
            Exception = entry.Exception,
            Properties = entry.Properties,
        };
    }
}
