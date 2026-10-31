using System.Globalization;
using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Logging;
using Serilog.Events;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der Diagnose-Logs.
/// </summary>
internal static class LogEndpoints
{
    /// <summary>
    /// Die maximale Anzahl der Einträge in einer Antwort.
    /// </summary>
    public const int MaxLimit = 1000;

    /// <summary>
    /// Der Standardwert für <c>limit</c>.
    /// </summary>
    public const int DefaultLimit = 200;

    /// <summary>
    /// Die Anzahl der Einträge im Initial-Snapshot des SSE-Streams.
    /// </summary>
    public const int SnapshotSize = 50;

    /// <summary>
    /// Registriert die Log-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapLogs(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/diagnostics/logs", (HttpContext context, InMemoryLogSink sink) =>
            {
                if (!TryReadLimit(context, out var limit, out var limitError))
                {
                    return limitError;
                }

                if (!TryReadLevel(context, out var minimumLevel, out var levelError))
                {
                    return levelError;
                }

                if (!TryReadSince(context, out var since, out var sinceError))
                {
                    return sinceError;
                }

                var order = context.Request.Query["order"].ToString();
                var ascending = string.Equals(order, "asc", StringComparison.OrdinalIgnoreCase);

                if (!string.IsNullOrEmpty(order)
                    && !ascending
                    && !string.Equals(order, "desc", StringComparison.OrdinalIgnoreCase))
                {
                    return Results.Json(
                        new ApiError { Error = "invalid_request", Message = "Der Wert von 'order' muss 'asc' oder 'desc' sein." },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var source = context.Request.Query["source"].ToString();
                var search = context.Request.Query["search"].ToString();

                var entries = sink.Snapshot(sink.Capacity)
                    .Where(entry => MatchesLevel(entry, minimumLevel))
                    .Where(entry => MatchesSource(entry, source))
                    .Where(entry => MatchesSearch(entry, search))
                    .Where(entry => since is null || entry.Timestamp >= since.Value)
                    .ToArray();

                if (ascending)
                {
                    Array.Reverse(entries);
                }

                var result = entries
                    .Take(limit)
                    .Select(LogEntryDto.From)
                    .ToArray();

                return Results.Ok(result);
            })
            .WithTags("Diagnose")
            .WithName("GetLogs")
            .Produces<LogEntryDto[]>();

        app.MapGet("/api/diagnostics/logs/stream", async (
                HttpContext context,
                InMemoryLogSink sink,
                LogStreamBroker broker,
                CancellationToken cancellationToken) =>
            {
                if (!broker.TrySubscribe(out var stream))
                {
                    context.Response.StatusCode = StatusCodes.Status429TooManyRequests;
                    await context.Response.WriteAsJsonAsync(
                        new ApiError
                        {
                            Error = "too_many_streams",
                            Message = $"Es sind bereits {LogStreamBroker.MaxStreams} Log-Streams aktiv.",
                        },
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                await SseWriter.PrepareAsync(context, cancellationToken).ConfigureAwait(false);

                // Der Initial-Snapshot: die letzten Einträge, älter zuerst.
                var snapshot = sink.Snapshot(SnapshotSize);
                Array.Reverse(snapshot);

                foreach (var entry in snapshot)
                {
                    await SseWriter.WriteAsync(
                        context,
                        "log",
                        LogEntryDto.From(entry),
                        cancellationToken).ConfigureAwait(false);
                }

                // Danach nur die neuen Einträge (ereignisgetrieben, kein Polling).
                await foreach (var entry in stream!.WithCancellation(cancellationToken).ConfigureAwait(false))
                {
                    await SseWriter.WriteAsync(
                        context,
                        "log",
                        LogEntryDto.From(entry),
                        cancellationToken).ConfigureAwait(false);
                }
            })
            .WithTags("Diagnose")
            .WithName("StreamLogs");

        app.MapDelete("/api/diagnostics/logs", (InMemoryLogSink sink) =>
            {
                sink.Clear();
                return Results.NoContent();
            })
            .WithTags("Diagnose")
            .WithName("ClearLogs")
            .Produces(StatusCodes.Status204NoContent);
    }

    /// <summary>
    /// Liest den Parameter <c>limit</c>.
    /// </summary>
    private static bool TryReadLimit(HttpContext context, out int limit, out IResult error)
    {
        limit = DefaultLimit;
        error = Results.Empty;

        var raw = context.Request.Query["limit"].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) || value < 1)
        {
            error = Results.Json(
                new ApiError { Error = "invalid_request", Message = "Der Wert von 'limit' muss eine positive Zahl sein." },
                statusCode: StatusCodes.Status400BadRequest);
            return false;
        }

        // Obergrenze: keine unbegrenzte Antwort.
        limit = Math.Min(value, MaxLimit);
        return true;
    }

    /// <summary>
    /// Liest den Parameter <c>level</c> (minimaler Level).
    /// </summary>
    private static bool TryReadLevel(HttpContext context, out LogEventLevel? minimumLevel, out IResult error)
    {
        minimumLevel = null;
        error = Results.Empty;

        var raw = context.Request.Query["level"].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!Enum.TryParse<LogEventLevel>(raw, ignoreCase: true, out var parsed))
        {
            error = Results.Json(
                new ApiError { Error = "invalid_request", Message = $"Unbekanntes Log-Level '{raw}'." },
                statusCode: StatusCodes.Status400BadRequest);
            return false;
        }

        minimumLevel = parsed;
        return true;
    }

    /// <summary>
    /// Liest den Parameter <c>since</c>.
    /// </summary>
    private static bool TryReadSince(HttpContext context, out DateTimeOffset? since, out IResult error)
    {
        since = null;
        error = Results.Empty;

        var raw = context.Request.Query["since"].ToString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!DateTimeOffset.TryParse(
                raw,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out var parsed))
        {
            error = Results.Json(
                new ApiError { Error = "invalid_request", Message = $"Ungültiger Zeitstempel '{raw}'." },
                statusCode: StatusCodes.Status400BadRequest);
            return false;
        }

        since = parsed;
        return true;
    }

    /// <summary>
    /// Entspricht der Eintrag dem Mindest-Level?
    /// </summary>
    private static bool MatchesLevel(LogEntry entry, LogEventLevel? minimumLevel)
    {
        if (minimumLevel is null)
        {
            return true;
        }

        return Enum.TryParse<LogEventLevel>(entry.Level, ignoreCase: true, out var level)
            && level >= minimumLevel.Value;
    }

    /// <summary>
    /// Entspricht der Eintrag der Quelle?
    /// </summary>
    private static bool MatchesSource(LogEntry entry, string source)
        => string.IsNullOrWhiteSpace(source)
            || entry.Source.Contains(source, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Entspricht der Eintrag dem Suchtext?
    /// </summary>
    private static bool MatchesSearch(LogEntry entry, string search)
    {
        if (string.IsNullOrWhiteSpace(search))
        {
            return true;
        }

        if (entry.Message.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (entry.Exception is not null
            && entry.Exception.Contains(search, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var property in entry.Properties)
        {
            if (property.Value.Contains(search, StringComparison.OrdinalIgnoreCase)
                || property.Key.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
