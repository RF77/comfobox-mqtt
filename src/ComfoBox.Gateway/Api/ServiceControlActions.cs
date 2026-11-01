using ComfoBox.Bacnet;
using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Betriebssteuerung: Neustart und Verbindungen neu aufbauen.
/// </summary>
/// <remarks>
/// <para>
/// Der Neustart läuft **in-process**: der Host beendet sich kontrolliert, der
/// Supervisor startet ihn neu (→ ADR-007).
/// </para>
/// <para>
/// Die Reconnects bauen nur die Verbindung neu auf - sie beenden **nichts**.
/// </para>
/// </remarks>
internal static class ServiceControlActions
{
    /// <summary>
    /// Registriert die Reconnect-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapReconnect(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/service/reconnect/bacnet", async (
                HttpContext context,
                IBacnetClient client,
                IHealthReporter health,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var logger = loggerFactory.CreateLogger("ComfoBox.Gateway.Api");

                try
                {
                    // Eine WhoIs-Runde: der Controller antwortet mit IAm, und
                    // der Gateway kennt wieder, ob die Heizung da ist.
                    await client.WhoIsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);

                    logger.LogInformation("Eine BACnet-WhoIs-Runde wurde ausgelöst.");

                    return Results.Accepted(value: new { status = "reconnecting" });
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    logger.LogWarning(ex, "Die BACnet-WhoIs-Runde ist fehlgeschlagen.");
                    health.Report("Bacnet", HealthState.Degraded, $"Der BACnet-Bus ist nicht erreichbar: {ex.Message}");

                    return Results.Json(
                        new ApiError
                        {
                            Error = "bus_error",
                            Message = "Der BACnet-Bus ist nicht erreichbar.",
                        },
                        statusCode: StatusCodes.Status502BadGateway);
                }
            })
            .WithTags("Betrieb")
            .WithName("ReconnectBacnet")
            .Produces(StatusCodes.Status202Accepted)
            .Produces<ApiError>(StatusCodes.Status502BadGateway);

        app.MapPost("/api/service/reconnect/mqtt", async (
                HttpContext context,
                MqttConnectionManager manager,
                IHealthReporter health,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var logger = loggerFactory.CreateLogger("ComfoBox.Gateway.Api");

                var count = await manager.RequestReconnectAsync(cancellationToken).ConfigureAwait(false);

                logger.LogInformation("Ein MQTT-Reconnect wurde für {Count} Broker ausgelöst.", count);
                health.Report("Mqtt", HealthState.Degraded, "Die MQTT-Verbindung wird neu aufgebaut.");

                return Results.Accepted(value: new { status = "reconnecting", brokers = count });
            })
            .WithTags("Betrieb")
            .WithName("ReconnectMqtt")
            .Produces(StatusCodes.Status202Accepted);
    }
}
