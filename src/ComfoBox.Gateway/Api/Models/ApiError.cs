namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Die Fehlerantwort der REST-Schnittstelle.
/// </summary>
/// <remarks>
/// Der Feldname <c>error</c> ist ein stabiler Fehlercode - die Web-UI und die
/// Home-Assistant-Integration stützen sich darauf.
/// </remarks>
public sealed record ApiError
{
    /// <summary>
    /// Der stabile Fehlercode, z. B. <c>value_out_of_range</c>.
    /// </summary>
    public required string Error { get; init; }

    /// <summary>
    /// Die Meldung (deutsch, für die Anzeige).
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Die Id des betroffenen Items (bei Item-Fehlern).
    /// </summary>
    public string? ItemId { get; init; }

    /// <summary>
    /// Die Anzahl der Schreibvorgänge im 24-h-Fenster.
    /// </summary>
    public int? WritesLast24h { get; init; }

    /// <summary>
    /// Das Limit der Schreibvorgänge im 24-h-Fenster.
    /// </summary>
    public int? MaxWritesPer24h { get; init; }

    /// <summary>
    /// Die untere Grenze des zulässigen Bereichs.
    /// </summary>
    public double? Min { get; init; }

    /// <summary>
    /// Die obere Grenze des zulässigen Bereichs.
    /// </summary>
    public double? Max { get; init; }

    /// <summary>
    /// Der Hinweis für die Oberfläche, z. B. <c>no_iam</c>.
    /// </summary>
    public string? Hint { get; init; }
}
