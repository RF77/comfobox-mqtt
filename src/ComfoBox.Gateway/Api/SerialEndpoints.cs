using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Hardware;
using ComfoBox.Gateway.Security;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der seriellen Schnittstelle.
/// </summary>
/// <remarks>
/// <para>
/// Der Benutzer soll den seriellen Port **selbst auswählen** können - ohne
/// die Konfigurationsdatei zu bearbeiten und ohne den Gateway neu zu
/// starten.
/// </para>
/// <para>
/// Drei Endpunkte:
/// </para>
/// <list type="bullet">
/// <item><c>GET /api/config/serial-ports</c> - die gefundenen Ports;</item>
/// <item><c>POST /api/config/bacnet/test</c> - die Prüfung eines Ports;</item>
/// <item><c>PUT /api/config/bacnet</c> - die Umstellung (wirkt sofort).</item>
/// </list>
/// </remarks>
internal static class SerialEndpoints
{
    /// <summary>
    /// Registriert die Endpunkte der seriellen Schnittstelle.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapSerial(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Die Port-Liste ist eine harmlose Lese-Anfrage - sie darf auch ein
        // Benutzer ohne Administratorrechte abrufen (die Oberfläche zeigt den
        // Hinweis „BACnet nicht verbunden" auf der Startseite).
        app.MapGet("/api/config/serial-ports", (ISerialPortScanner scanner) =>
                Results.Ok(scanner.Scan()))
            .WithTags("Konfiguration")
            .WithName("GetSerialPorts")
            .Produces<IReadOnlyList<SerialPortInfo>>();

        // Die Prüfung öffnet den Port - das ist ein Eingriff in die Hardware
        // und deshalb dem Administrator vorbehalten.
        app.MapPost("/api/config/bacnet/test", (
                HttpContext context,
                SerialPortTestRequest request,
                BacnetSerialManager manager,
                ApiAccessGuard guard) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                var result = manager.Test(request?.Port, request?.Baudrate ?? 38400);

                return Results.Ok(result);
            })
            .WithTags("Konfiguration")
            .WithName("TestSerialPort")
            .Produces<TestResultDto>()
            .Produces<ApiError>(StatusCodes.Status401Unauthorized)
            .Produces<ApiError>(StatusCodes.Status403Forbidden);

        // Die Umstellung schreibt die Konfiguration **und** stellt den
        // BACnet-Stack sofort um - ein Neustart ist nicht nötig.
        app.MapPut("/api/config/bacnet", (
                HttpContext context,
                SerialPortUpdateRequest request,
                BacnetSerialManager manager,
                ApiAccessGuard guard) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                if (request is null)
                {
                    return Results.Json(
                        new ApiError { Error = "invalid_request", Message = "Es wurde keine Konfiguration übergeben." },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                try
                {
                    manager.Apply(request.Port, request.Baudrate);
                }
                catch (ArgumentException ex)
                {
                    return Results.Json(
                        new ApiError { Error = "invalid_request", Message = ex.Message },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.NoContent();
            })
            .WithTags("Konfiguration")
            .WithName("UpdateBacnetConfig")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized)
            .Produces<ApiError>(StatusCodes.Status403Forbidden);
    }
}
