using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Health;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Fehlerbehandlung der API.
/// </summary>
/// <remarks>
/// Jeder abgelehnte Schreibvorgang landet in **drei** Kanälen: Log,
/// <c>ComfoBox/Status/Errors</c> und die UI. Dieser Helfer sorgt dafür, dass
/// die API denselben Fehlerkanal benutzt wie der MQTT-Pfad.
/// </remarks>
public static class ApiErrors
{
    /// <summary>
    /// Erzeugt eine Fehlerantwort und meldet den Fehler.
    /// </summary>
    /// <param name="errors">Die Fehler-Senke (oder <c>null</c>).</param>
    /// <param name="error">Die Fehlerbeschreibung.</param>
    /// <param name="statusCode">Der HTTP-Statuscode.</param>
    /// <param name="report">
    /// Soll der Fehler gemeldet werden? <c>false</c>, wenn der Fehler bereits
    /// an anderer Stelle gemeldet wurde (z. B. vom BACnet-Gerät).
    /// </param>
    /// <param name="itemId">Die Id des betroffenen Items (oder <c>null</c>).</param>
    /// <returns>Die Antwort.</returns>
    public static IResult Failure(
        IErrorCollector? errors,
        ApiError error,
        int statusCode,
        bool report = true,
        string? itemId = null)
    {
        ArgumentNullException.ThrowIfNull(error);

        if (report && errors is not null)
        {
            errors.Report(
                "Api",
                error.Error,
                itemId is null ? error.Message : $"{itemId}: {error.Message}",
                null,
                SeverityOf(error.Error));
        }

        return Results.Json(error, statusCode: statusCode);
    }

    /// <summary>
    /// Der Schweregrad eines Fehlercodes.
    /// </summary>
    /// <param name="code">Der Fehlercode.</param>
    /// <returns><c>warning</c> für Benutzerfehler, sonst <c>error</c>.</returns>
    /// <remarks>
    /// Benutzerfehler verschlechern den Health-Status **nicht** - der Gateway
    /// läuft ja.
    /// </remarks>
    private static string SeverityOf(string code) => code switch
    {
        "invalid_request" or "invalid_value" or "invalid_type" or "value_out_of_range" => "info",
        "read_only" or "expert_mode_required" or "write_limit_exceeded" => "warning",
        _ => "error",
    };
}
