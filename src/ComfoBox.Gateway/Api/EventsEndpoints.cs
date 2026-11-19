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
                // Der Abonnent wird beim **ersten** Schritt des Iterators
                // synchron registriert. Deshalb entsteht er, bevor der
                // Snapshot geschrieben wird: sonst ginge eine Änderung
                // verloren, die genau zwischen Snapshot und Registrierung
                // passiert - der Client sähe den alten Wert bis zur nächsten
                // Änderung.
                var enumerator = cache
                    .SubscribeAsync(cancellationToken)
                    .GetAsyncEnumerator(cancellationToken);

                try
                {
                    var pending = enumerator.MoveNextAsync().AsTask();

                    await SseWriter.PrepareAsync(context, cancellationToken).ConfigureAwait(false);

                    // Der Snapshot: alle aktuell bekannten Werte, einmalig.
                    var snapshot = cache.Snapshot()
                        .Select(CachedValueDto.From)
                        .ToArray();

                    await SseWriter.WriteAsync(context, "snapshot", snapshot, cancellationToken)
                        .ConfigureAwait(false);

                    var heartbeat = TimeSpan.FromSeconds(options.Value.SseHeartbeatSeconds);

                    await PumpAsync(context, enumerator, pending, heartbeat, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    // Der Client hat die Verbindung geschlossen (oder der Host
                    // fährt herunter). Der Abonnent wird beim Entsorgen des
                    // Iterators abgemeldet.
                }
                catch (IOException)
                {
                    // Die Gegenstelle ist weg, ohne den Request abzubrechen
                    // (abgerissene TCP-Verbindung).
                }
                finally
                {
                    await enumerator.DisposeAsync().ConfigureAwait(false);
                }
            })
            .WithTags("Ereignisse")
            .WithName("GetEvents");
    }

    /// <summary>
    /// Schreibt die Wertänderungen - und dazwischen ein Lebenszeichen.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <param name="enumerator">Der laufende Abonnent.</param>
    /// <param name="pending">Der bereits gestartete erste Schritt des Iterators.</param>
    /// <param name="heartbeat">Der Abstand der Lebenszeichen (<c>0</c> = aus).</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Aufgabe, die den Stream abschließt.</returns>
    /// <remarks>
    /// Das Lebenszeichen darf kein Ereignis verschlucken: das Warten auf den
    /// nächsten Wert läuft **weiter**, während der Timer abläuft - es wird
    /// gegen ihn „gerannt“, nicht abgebrochen.
    /// </remarks>
    private static async Task PumpAsync(
        HttpContext context,
        IAsyncEnumerator<CachedValue> enumerator,
        Task<bool> pending,
        TimeSpan heartbeat,
        CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var valueAvailable = pending;

            if (heartbeat > TimeSpan.Zero)
            {
                var tick = Task.Delay(heartbeat, cancellationToken);
                var finished = await Task.WhenAny(pending, tick).ConfigureAwait(false);

                if (finished != pending)
                {
                    // Der Timer ist abgelaufen (`await` wirft bei Abbruch).
                    await tick.ConfigureAwait(false);
                    await SseWriter.WriteCommentAsync(context, "ping", cancellationToken)
                        .ConfigureAwait(false);
                    continue;
                }
            }

            if (!await valueAvailable.ConfigureAwait(false))
            {
                // Der Cache hat den Abonnenten beendet (Shutdown).
                return;
            }

            var value = enumerator.Current;
            pending = enumerator.MoveNextAsync().AsTask();

            await SseWriter.WriteAsync(context, "value", CachedValueDto.From(value), cancellationToken)
                .ConfigureAwait(false);
        }
    }
}
