using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Der SSE-Stream mit den Live-Werten.
/// </summary>
/// <remarks>
/// <para>
/// Der Stream ist die Push-Schnittstelle des Gateways: die Web-UI **und** die
/// Home-Assistant-Integration beziehen ihre Werte von hier, statt den
/// vollständigen Katalog im Poll-Takt abzufragen.
/// </para>
/// <para>
/// **Ein Strom je Client, nicht je Entität.** Ein Aufruf von
/// <c>GET /api/events</c> legt genau einen Abonnenten an; alle Werte kommen
/// über dieselbe Verbindung.
/// </para>
/// <para>
/// **Gesammelt statt einzeln.** Ein Poll-Zyklus liest die Items nacheinander
/// über den Bus - 35 Änderungen (Sekundärzyklus: 160) entstehen in kurzer
/// Folge, gehören aber sachlich zusammen. Der Stream schickt sie deshalb als
/// **ein** Ereignis (<c>event: values</c>, Array) und wartet dafür ein kurzes
/// Ruhefenster ab (siehe <see cref="ApiOptions.SseBatchQuietMs"/>).
/// </para>
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
                TimeProvider timeProvider,
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

                    var batch = new BatchWindow(
                        TimeSpan.FromMilliseconds(options.Value.SseBatchQuietMs),
                        TimeSpan.FromMilliseconds(options.Value.SseBatchMaxDelayMs));

                    var heartbeat = TimeSpan.FromSeconds(options.Value.SseHeartbeatSeconds);

                    await PumpAsync(context, enumerator, pending, batch, heartbeat, timeProvider, cancellationToken)
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
    /// Schreibt die Wertänderungen gesammelt - und dazwischen ein Lebenszeichen.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <param name="enumerator">Der laufende Abonnent.</param>
    /// <param name="pending">Der bereits gestartete erste Schritt des Iterators.</param>
    /// <param name="batch">Die Fenster des Sammelns.</param>
    /// <param name="heartbeat">Der Abstand der Lebenszeichen (<c>0</c> = aus).</param>
    /// <param name="timeProvider">Die Zeitquelle.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Aufgabe, die den Stream abschließt.</returns>
    /// <remarks>
    /// <para>
    /// Das Warten auf den nächsten Wert läuft **weiter**, während ein Fenster
    /// abläuft - es wird gegen den Timer „gerannt“, nicht abgebrochen. Sonst
    /// ginge bei jeder Unterbrechung ein Wert verloren.
    /// </para>
    /// <para>
    /// Mehrfach gemeldete Ids innerhalb eines Fensters überschreiben sich: der
    /// Client braucht nur den letzten Stand.
    /// </para>
    /// </remarks>
    private static async Task PumpAsync(
        HttpContext context,
        IAsyncEnumerator<CachedValue> enumerator,
        Task<bool> pending,
        BatchWindow batch,
        TimeSpan heartbeat,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        var collected = new Dictionary<string, CachedValue>(StringComparer.Ordinal);
        long? first = null;
        long? last = null;

        while (!cancellationToken.IsCancellationRequested)
        {
            var wait = NextWait(collected.Count > 0, first, last, batch, heartbeat, timeProvider);

            if (wait is not null)
            {
                if (wait.Value <= TimeSpan.Zero)
                {
                    // Das Fenster ist abgelaufen (oder sofortiges Senden ist
                    // eingestellt) - ohne auf einen weiteren Wert zu warten.
                    await FlushAsync(context, collected, cancellationToken).ConfigureAwait(false);
                    collected.Clear();
                    first = null;
                    last = null;
                    continue;
                }

                var delay = Task.Delay(wait.Value, cancellationToken);
                if (await Task.WhenAny(pending, delay).ConfigureAwait(false) != pending)
                {
                    // Der Timer ist abgelaufen (`await` wirft bei Abbruch).
                    await delay.ConfigureAwait(false);

                    await FlushAsync(context, collected, cancellationToken).ConfigureAwait(false);
                    collected.Clear();
                    first = null;
                    last = null;
                    continue;
                }
            }

            if (!await pending.ConfigureAwait(false))
            {
                // Der Cache hat den Abonnenten beendet (Shutdown).
                return;
            }

            var value = enumerator.Current;
            pending = enumerator.MoveNextAsync().AsTask();

            // Der letzte Stand einer Id gewinnt.
            collected[value.Id] = value;

            var now = timeProvider.GetTimestamp();
            first ??= now;
            last = now;
        }
    }

    /// <summary>
    /// Wie lange bis zum nächsten Abschicken gewartet wird.
    /// </summary>
    /// <param name="hasValues">Liegen Werte im Sammelfenster?</param>
    /// <param name="first">Der Zeitstempel des ersten gesammelten Werts.</param>
    /// <param name="last">Der Zeitstempel des zuletzt gesammelten Werts.</param>
    /// <param name="batch">Die Fenster des Sammelns.</param>
    /// <param name="heartbeat">Der Abstand der Lebenszeichen.</param>
    /// <param name="timeProvider">Die Zeitquelle.</param>
    /// <returns>Die Wartezeit oder <c>null</c> für „unbegrenzt“.</returns>
    /// <remarks>
    /// Ohne Werte zählt nur das Lebenszeichen; mit Werten ist das Minimum aus
    /// Ruhezeit, Obergrenze und Lebenszeichen maßgeblich. Die Obergrenze
    /// begrenzt die Latenz, das Lebenszeichen wirkt hier als „spätestens“.
    /// </remarks>
    private static TimeSpan? NextWait(
        bool hasValues,
        long? first,
        long? last,
        BatchWindow batch,
        TimeSpan heartbeat,
        TimeProvider timeProvider)
    {
        TimeSpan? wait = heartbeat > TimeSpan.Zero ? heartbeat : null;

        if (!hasValues)
        {
            return wait;
        }

        var now = timeProvider.GetTimestamp();
        var quiet = batch.Quiet - timeProvider.GetElapsedTime(last!.Value, now);
        var maximum = batch.MaxDelay - timeProvider.GetElapsedTime(first!.Value, now);

        var due = quiet < maximum ? quiet : maximum;
        if (due < TimeSpan.Zero)
        {
            due = TimeSpan.Zero;
        }

        return wait is null || due < wait.Value ? due : wait;
    }

    /// <summary>
    /// Schickt die gesammelten Werte - oder ein Lebenszeichen.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <param name="collected">Die gesammelten Werte.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Aufgabe, die das Schreiben abschließt.</returns>
    private static async Task FlushAsync(
        HttpContext context,
        Dictionary<string, CachedValue> collected,
        CancellationToken cancellationToken)
    {
        if (collected.Count == 0)
        {
            await SseWriter.WriteCommentAsync(context, "ping", cancellationToken).ConfigureAwait(false);
            return;
        }

        var values = collected.Values
            .Select(CachedValueDto.From)
            .ToArray();

        await SseWriter.WriteAsync(context, "values", values, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Die Fenster des Sammelns.
    /// </summary>
    /// <param name="Quiet">Ruhezeit nach dem letzten Wert.</param>
    /// <param name="MaxDelay">Die längste Wartezeit des ältesten Werts.</param>
    private readonly record struct BatchWindow(TimeSpan Quiet, TimeSpan MaxDelay);
}
