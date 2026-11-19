using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Der SSE-Stream mit den Live-Werten.
/// </summary>
/// <remarks>
/// Der Stream ist die Push-Schnittstelle des Gateways: die Web-UI **und** die
/// Home-Assistant-Integration beziehen ihre Werte von hier, statt den
/// vollständigen Katalog im Poll-Takt abzufragen.
/// </remarks>
internal static class EventsEndpoints
{
    /// <summary>
    /// Registriert den SSE-Endpunkt.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapEvents(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/events", async (
                HttpContext context,
                IValueCache cache,
                IOptions<ApiOptions> options,
                CancellationToken cancellationToken) =>
            {
                try
                {
                    await SseWriter.PrepareAsync(context, cancellationToken).ConfigureAwait(false);

                    // Der Snapshot: alle aktuell bekannten Werte, einmalig.
                    var snapshot = cache.Snapshot()
                        .Select(CachedValueDto.From)
                        .ToArray();

                    await SseWriter.WriteAsync(context, "snapshot", snapshot, cancellationToken).ConfigureAwait(false);

                    var heartbeat = TimeSpan.FromSeconds(options.Value.SseHeartbeatSeconds);
                    if (heartbeat > TimeSpan.Zero)
                    {
                        await StreamWithHeartbeatAsync(context, cache, heartbeat, cancellationToken)
                            .ConfigureAwait(false);
                        return;
                    }

                    // Danach nur noch die Änderungen.
                    await foreach (var value in cache.SubscribeAsync(cancellationToken).ConfigureAwait(false))
                    {
                        await SseWriter.WriteAsync(
                            context,
                            "value",
                            CachedValueDto.From(value),
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Der Client hat die Verbindung geschlossen (oder der Host
                    // fährt herunter). Der Abonnent wird in `ValueCache`
                    // abgemeldet - hier ist nichts zu tun.
                }
                catch (IOException)
                {
                    // Die Gegenstelle ist weg, ohne den Request abzubrechen
                    // (abgerissene TCP-Verbindung).
                }
            })
            .WithTags("Ereignisse")
            .WithName("GetEvents");
    }

    /// <summary>
    /// Schreibt die Wertänderungen und dazwischen ein Lebenszeichen.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <param name="cache">Der Wert-Cache.</param>
    /// <param name="heartbeat">Der Abstand der Lebenszeichen.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Aufgabe, die den Stream abschließt.</returns>
    /// <remarks>
    /// Das Lebenszeichen darf kein Ereignis verschlucken: das Warten auf den
    /// nächsten Wert läuft **weiter**, während der Timer abläuft. Deshalb wird
    /// das eine Warten nicht abgebrochen, sondern gegen den Timer „gerannt“.
    /// </remarks>
    private static async Task StreamWithHeartbeatAsync(
        HttpContext context,
        IValueCache cache,
        TimeSpan heartbeat,
        CancellationToken cancellationToken)
    {
        var enumerator = cache
            .SubscribeAsync(cancellationToken)
            .GetAsyncEnumerator(cancellationToken);

        try
        {
            var pending = enumerator.MoveNextAsync().AsTask();

            while (!cancellationToken.IsCancellationRequested)
            {
                var tick = Task.Delay(heartbeat, cancellationToken);
                var finished = await Task.WhenAny(pending, tick).ConfigureAwait(false);

                if (finished == pending)
                {
                    if (!await pending.ConfigureAwait(false))
                    {
                        // Der Cache hat den Abonnenten beendet (Shutdown).
                        break;
                    }

                    var value = enumerator.Current;
                    pending = enumerator.MoveNextAsync().AsTask();

                    await SseWriter.WriteAsync(
                        context,
                        "value",
                        CachedValueDto.From(value),
                        cancellationToken).ConfigureAwait(false);

                    continue;
                }

                // Der Timer ist abgelaufen (`await` wirft bei Abbruch).
                await tick.ConfigureAwait(false);
                await SseWriter.WriteCommentAsync(context, "ping", cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            await enumerator.DisposeAsync().ConfigureAwait(false);
        }
    }
}
