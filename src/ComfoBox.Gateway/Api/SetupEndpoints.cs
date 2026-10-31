using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Security;
using ComfoBox.Gateway.Setup;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte des Getting-Started-Assistenten.
/// </summary>
/// <remarks>
/// Nach der Ersteinrichtung sind alle diese Endpunkte geschützt - sie können
/// die Konfiguration und damit auch das Administrator-Passwort überschreiben.
/// </remarks>
internal static class SetupEndpoints
{
    /// <summary>
    /// Registriert die Setup-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapSetup(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/setup/state", (HttpContext context, SetupService setup, ApiAccessGuard guard) =>
            {
                var denied = guard.RequireSetupAccess(context);
                return denied ?? Results.Ok(setup.GetState());
            })
            .WithTags("Einrichtung")
            .WithName("GetSetupState");

        app.MapGet("/api/setup/ports", (HttpContext context, SetupService setup, ApiAccessGuard guard) =>
            {
                var denied = guard.RequireSetupAccess(context);
                return denied ?? Results.Ok(setup.GetPorts());
            })
            .WithTags("Einrichtung")
            .WithName("GetSetupPorts");

        app.MapGet("/api/setup/ports/suggest", (HttpContext context, SetupService setup, ApiAccessGuard guard) =>
            {
                var denied = guard.RequireSetupAccess(context);
                return denied ?? Results.Ok(setup.SuggestPort());
            })
            .WithTags("Einrichtung")
            .WithName("SuggestSetupPort");

        app.MapPost("/api/setup/bacnet/test", async (
                HttpContext context,
                BacnetTestRequest request,
                SetupService setup,
                ApiAccessGuard guard,
                CancellationToken cancellationToken) =>
            {
                var denied = guard.RequireSetupAccess(context);
                if (denied is not null)
                {
                    return denied;
                }

                var result = await setup
                    .TestBacnetAsync(request?.Port, request?.Baudrate ?? 38400, cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(result);
            })
            .WithTags("Einrichtung")
            .WithName("TestSetupBacnet")
            .Produces<TestResultDto>();

        app.MapPost("/api/setup/mqtt/test", async (
                HttpContext context,
                MqttTestRequest request,
                SetupService setup,
                ApiAccessGuard guard,
                CancellationToken cancellationToken) =>
            {
                var denied = guard.RequireSetupAccess(context);
                if (denied is not null)
                {
                    return denied;
                }

                var result = await setup
                    .TestMqttAsync(
                        request?.Host,
                        request?.Port ?? 1883,
                        request?.Username,
                        request?.Password,
                        request?.UseTls ?? false,
                        cancellationToken)
                    .ConfigureAwait(false);

                return Results.Ok(result);
            })
            .WithTags("Einrichtung")
            .WithName("TestSetupMqtt")
            .Produces<TestResultDto>();

        app.MapPut("/api/setup/config", (
                HttpContext context,
                SetupConfigRequest request,
                SetupService setup,
                ApiAccessGuard guard) =>
            {
                var denied = guard.RequireSetupAccess(context);
                if (denied is not null)
                {
                    return denied;
                }

                try
                {
                    setup.WriteConfig(request);
                }
                catch (ArgumentException ex)
                {
                    return Results.Json(
                        new ApiError { Error = "invalid_request", Message = ex.Message },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                return Results.NoContent();
            })
            .WithTags("Einrichtung")
            .WithName("WriteSetupConfig")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status400BadRequest);

        app.MapPost("/api/setup/apply", (
                HttpContext context,
                SetupService setup,
                IRestartService restart,
                ApiAccessGuard guard) =>
            {
                var denied = guard.RequireSetupAccess(context);
                if (denied is not null)
                {
                    return denied;
                }

                setup.CompleteSetup();

                // Der Neustart setzt die neue Konfiguration in Kraft.
                restart.RequestRestartAsync(CancellationToken.None).ConfigureAwait(false);

                return Results.Accepted(value: new { status = "restarting" });
            })
            .WithTags("Einrichtung")
            .WithName("ApplySetup")
            .Produces(StatusCodes.Status202Accepted);
    }
}
