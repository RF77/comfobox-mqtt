using System.Globalization;
using Serilog.Events;

namespace ComfoBox.Gateway.Logging;

/// <summary>
/// Ein Log-Eintrag im Ringpuffer.
/// </summary>
public sealed record LogEntry
{
    /// <summary>
    /// Der Zeitpunkt des Eintrags.
    /// </summary>
    public required DateTimeOffset Timestamp { get; init; }

    /// <summary>
    /// Der Level, z. B. <c>Information</c> oder <c>Warning</c>.
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
    /// Der Stacktrace, wenn eine Ausnahme aufgetreten ist.
    /// </summary>
    public string? Exception { get; init; }

    /// <summary>
    /// Die strukturierten Properties des Eintrags.
    /// </summary>
    public IReadOnlyDictionary<string, string> Properties { get; init; } =
        new Dictionary<string, string>(StringComparer.Ordinal);

    /// <summary>
    /// Erzeugt einen Eintrag aus einem Serilog-Ereignis.
    /// </summary>
    /// <param name="logEvent">Das Serilog-Ereignis.</param>
    /// <remarks>
    /// Geheimnisse werden **vor** dem Speichern maskiert - der Ringpuffer ist
    /// in der Web-UI sichtbar.
    /// </remarks>
    public static LogEntry From(LogEvent logEvent)
    {
        ArgumentNullException.ThrowIfNull(logEvent);

        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in logEvent.Properties)
        {
            properties[property.Key] = SecretRedactor.Redact(property.Key, RenderValue(property.Value));
        }

        // Serilog 4 kennt am LogEvent keine LoggerName-Eigenschaft - die
        // Logger-Kategorie steckt in der Property "SourceContext".
        var source = logEvent.Properties.TryGetValue("SourceContext", out var context)
            ? RenderValue(context)
            : string.Empty;

        return new LogEntry
        {
            Timestamp = logEvent.Timestamp,
            Level = logEvent.Level.ToString(),
            Source = source,
            Message = SecretRedactor.RedactText(logEvent.RenderMessage(CultureInfo.InvariantCulture)),
            Exception = logEvent.Exception is null
                ? null
                : SecretRedactor.RedactText(logEvent.Exception.ToString()),
            Properties = properties,
        };
    }

    /// <summary>
    /// Rendert einen Property-Wert ohne Anführungszeichen.
    /// </summary>
    /// <remarks>
    /// <c>ScalarValue.ToString()</c> gibt Strings in Anführungszeichen
    /// zurück ("\"broker:1883\"") - das sieht in der Web-UI kaputt aus.
    /// </remarks>
    private static string RenderValue(object? value) => value switch
    {
        ScalarValue scalar => scalar.Value?.ToString() ?? string.Empty,
        null => string.Empty,
        _ => value.ToString() ?? string.Empty,
    };
}
