using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Security;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der Betriebssteuerung.
/// </summary>
internal static class ServiceEndpoints
{
    /// <summary>
    /// Registriert die Dienst-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapService(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost("/api/service/restart", async (
                HttpContext context,
                IRestartService restart,
                ApiAccessGuard guard,
                CancellationToken cancellationToken) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                // Der Neustart läuft **in-process**: der Host beendet sich
                // kontrolliert, der Supervisor startet ihn neu. Ein
                // Docker-Socket ist dafür nicht nötig (ADR-007).
                await restart.RequestRestartAsync(cancellationToken).ConfigureAwait(false);

                return Results.Accepted(value: new { status = "restarting" });
            })
            .WithTags("Betrieb")
            .WithName("RestartService")
            .Produces(StatusCodes.Status202Accepted)
            .Produces(StatusCodes.Status401Unauthorized)
            .Produces(StatusCodes.Status403Forbidden);
    }
}
