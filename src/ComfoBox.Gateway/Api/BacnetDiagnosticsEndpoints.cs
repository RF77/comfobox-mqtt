using ComfoBox.Bacnet;
using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Bacnet;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der BACnet-Bus-Analyse.
/// </summary>
/// <remarks>
/// <para>
/// Die Analyse ist **lesend**: sie stellt keine Anfragen an die ComfoBox
/// und schreibt keine Werte. Sie beobachtet nur, ob ein Token durch den
/// MS/TP-Ring läuft.
/// </para>
/// <para>
/// <b>Timing-Hinweis:</b> der MS/TP-Token hat nur wenige Millisekunden
/// Zeit (95 ms bis zur Übernahme). Die Analyse protokolliert deshalb
/// <b>nichts</b> im Hot-Path des Zustandsautomaten - die Ereignisse
/// werden gesammelt und außerhalb ausgewertet.
/// </para>
/// </remarks>
internal static class BacnetDiagnosticsEndpoints
{
    /// <summary>
    /// Registriert die Analyse-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapBacnetDiagnostics(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Die aktuelle Auswertung - ohne etwas zu verändern.
        app.MapGet("/api/diagnostics/bacnet", (BacnetBusAnalyzer analyzer) =>
            {
                return Results.Ok(BacnetAnalysisDto.From(analyzer.Analysis()));
            })
            .WithTags("Diagnose")
            .WithName("GetBacnetAnalysis");

        // Eine neue Beobachtung starten (Zähler und Ringpuffer werden
        // geleert, das Polling pausiert optional).
        app.MapPost("/api/diagnostics/bacnet/observe", (
                BacnetBusAnalyzer analyzer,
                BacnetObservationRequest? request) =>
            {
                var duration = request?.DurationSeconds ?? 15;
                var pausePolling = request?.PausePolling ?? true;

                try
                {
                    analyzer.Start(duration, pausePolling);
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = ex.Message,
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.Ok(BacnetAnalysisDto.From(analyzer.Analysis()));
            })
            .WithTags("Diagnose")
            .WithName("StartBacnetObservation");

        // Die Beobachtung beenden - das Polling läuft wieder an.
        app.MapDelete("/api/diagnostics/bacnet/observe", (BacnetBusAnalyzer analyzer) =>
            {
                analyzer.Stop();

                return Results.Ok(BacnetAnalysisDto.From(analyzer.Analysis()));
            })
            .WithTags("Diagnose")
            .WithName("StopBacnetObservation");
    }
}
