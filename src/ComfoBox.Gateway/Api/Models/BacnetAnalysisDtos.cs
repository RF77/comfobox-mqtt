using ComfoBox.Bacnet;
using ComfoBox.Gateway.Bacnet;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Die Anfrage, eine Bus-Analyse zu starten.
/// </summary>
public sealed record BacnetObservationRequest
{
    /// <summary>
    /// Die Dauer der Beobachtung in Sekunden (3 … 300).
    /// </summary>
    public int DurationSeconds { get; init; } = 15;

    /// <summary>
    /// Soll das Polling während der Beobachtung pausiert werden?
    /// </summary>
    /// <remarks>
    /// Empfohlen: ohne Pause konkurrieren die Wertabfragen des Gateways mit
    /// der Analyse um den Token.
    /// </remarks>
    public bool PausePolling { get; init; } = true;
}

/// <summary>
/// Ein beobachtetes Bus-Ereignis.
/// </summary>
/// <param name="Kind">Die Art des Ereignisses.</param>
/// <param name="Station">Die beteiligte Station.</param>
/// <param name="FrameType">Der MS/TP-Frame-Typ.</param>
/// <param name="Timestamp">Der Zeitpunkt (UTC).</param>
public sealed record BacnetEventDto(
    string Kind,
    byte Station,
    byte FrameType,
    DateTimeOffset Timestamp)
{
    /// <summary>
    /// Erzeugt die Beschreibung eines Ereignisses.
    /// </summary>
    /// <param name="evt">Das Ereignis.</param>
    /// <returns>Die Beschreibung.</returns>
    public static BacnetEventDto From(MstpEvent evt) => new(
        evt.Kind.ToString(),
        evt.Station,
        evt.FrameType,
        evt.TimestampUtc);
}

/// <summary>
/// Die Auswertung der Bus-Analyse.
/// </summary>
public sealed record BacnetAnalysisDto
{
    /// <summary>
    /// Der Beginn der Beobachtung.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// Die Dauer der Beobachtung in Sekunden.
    /// </summary>
    public required double DurationSeconds { get; init; }

    /// <summary>
    /// Läuft gerade eine Beobachtung?
    /// </summary>
    public required bool Observing { get; init; }

    /// <summary>
    /// Ist das Polling pausiert?
    /// </summary>
    public required bool PollingPaused { get; init; }

    /// <summary>
    /// Der konfigurierte serielle Port.
    /// </summary>
    public required string Port { get; init; }

    /// <summary>
    /// Die eingestellte Baudrate.
    /// </summary>
    public required int Baudrate { get; init; }

    /// <summary>
    /// Die MSTP-Adresse des Gateways.
    /// </summary>
    public required int ThisStation { get; init; }

    /// <summary>
    /// Die Anzahl der beobachteten Ereignisse.
    /// </summary>
    public required long TotalEvents { get; init; }

    /// <summary>
    /// Die Zähler je Ereignisart.
    /// </summary>
    public required Dictionary<string, long> Counters { get; init; }

    /// <summary>
    /// Die Stationen, die sich gemeldet haben.
    /// </summary>
    public required int[] StationsSeen { get; init; }

    /// <summary>
    /// Die Ereignisse (älteste zuerst).
    /// </summary>
    public required BacnetEventDto[] Events { get; init; }

    /// <summary>
    /// Die Bewertung in einem Satz.
    /// </summary>
    public required string Verdict { get; init; }

    /// <summary>
    /// Erzeugt die Beschreibung einer Analyse.
    /// </summary>
    /// <param name="analysis">Die Analyse.</param>
    /// <returns>Die Beschreibung.</returns>
    public static BacnetAnalysisDto From(BacnetAnalysis analysis)
    {
        ArgumentNullException.ThrowIfNull(analysis);

        var counters = new Dictionary<string, long>(StringComparer.Ordinal);

        foreach (var entry in analysis.Counters)
        {
            counters[entry.Key.ToString()] = entry.Value;
        }

        return new BacnetAnalysisDto
        {
            StartedAt = analysis.StartedAt,
            DurationSeconds = analysis.DurationSeconds,
            Observing = analysis.Observing,
            PollingPaused = analysis.PollingPaused,
            Port = analysis.Port,
            Baudrate = analysis.Baudrate,
            ThisStation = analysis.ThisStation,
            TotalEvents = analysis.TotalEvents,
            Counters = counters,
            StationsSeen = [.. analysis.StationsSeen],
            Events = [.. analysis.Events.Select(BacnetEventDto.From)],
            Verdict = analysis.Verdict,
        };
    }
}
