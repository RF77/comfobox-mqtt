using System.Text.Json;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Das Schreiben von Server-Sent Events.
/// </summary>
public static class SseWriter
{
    /// <summary>
    /// Schreibt ein Ereignis in den SSE-Stream.
    /// </summary>
    /// <typeparam name="T">Der Typ der Nutzlast.</typeparam>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <param name="eventName">Der Name des Ereignisses, z. B. <c>value</c>.</param>
    /// <param name="payload">Die Nutzlast.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Aufgabe, die das Schreiben abschließt.</returns>
    public static async Task WriteAsync<T>(
        HttpContext context,
        string eventName,
        T payload,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        var json = JsonSerializer.Serialize(payload, ApiJson.Options);
        await context.Response
            .WriteAsync($"event: {eventName}\ndata: {json}\n\n", cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// Setzt die Header für einen SSE-Stream und schickt sie sofort.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Aufgabe, die das Schreiben abschließt.</returns>
    /// <remarks>
    /// Der Flush ist wichtig: ohne Daten wartet der Client auf den ersten
    /// Eintrag - und sieht die Kopfzeilen erst dann. Der Browser feuert sein
    /// <c>onopen</c> aber erst mit den Kopfzeilen.
    /// </remarks>
    public static async Task PrepareAsync(HttpContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);

        context.Response.Headers.ContentType = "text/event-stream";
        context.Response.Headers.CacheControl = "no-cache";
        context.Response.Headers.Connection = "keep-alive";

        // Es gibt keinen Reverse-Proxy (ADR-007), aber falls doch einer
        // davorsteht, würde er den Stream puffern.
        context.Response.Headers["X-Accel-Buffering"] = "no";

        await context.Response.Body.FlushAsync(cancellationToken).ConfigureAwait(false);
    }
}
