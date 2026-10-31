using ComfoBox.Gateway.Api.Models;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Der SSE-Stream mit den Live-Werten.
/// </summary>
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
                CancellationToken cancellationToken) =>
            {
                await SseWriter.PrepareAsync(context, cancellationToken).ConfigureAwait(false);

                // Der Snapshot: alle aktuell bekannten Werte, einmalig.
                var snapshot = cache.Snapshot()
                    .Select(CachedValueDto.From)
                    .ToArray();

                await SseWriter.WriteAsync(context, "snapshot", snapshot, cancellationToken).ConfigureAwait(false);

                // Danach nur noch die Änderungen.
                await foreach (var value in cache.SubscribeAsync(cancellationToken).ConfigureAwait(false))
                {
                    await SseWriter.WriteAsync(
                        context,
                        "value",
                        CachedValueDto.From(value),
                        cancellationToken).ConfigureAwait(false);
                }
            })
            .WithTags("Ereignisse")
            .WithName("GetEvents");
    }
}
