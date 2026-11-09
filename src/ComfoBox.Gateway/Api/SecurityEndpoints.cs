using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Security;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der Zugriffssicherheit (Administrator und Anmelde-Pflicht).
/// </summary>
/// <remarks>
/// Die Einrichtung des ersten Administrators läuft über den Assistenten
/// (<c>/api/setup/*</c>). Hier geht es um die <b>spätere</b> Verwaltung:
/// Passwort ändern und die Anmeldung ein- oder ausschalten.
/// </remarks>
internal static class SecurityEndpoints
{
    /// <summary>
    /// Registriert die Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapSecurity(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/security/settings", (
                HttpContext context,
                ApiSecurityStore store,
                ApiAccessGuard guard) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                return Results.Ok(new SecuritySettingsDto
                {
                    SetupCompleted = store.SetupCompleted,
                    HasAdmin = store.HasAdmin,
                    AdminUsername = store.AdminUsername,
                    RequireAuthentication = guard.AuthenticationRequired,
                });
            })
            .WithTags("Sicherheit")
            .WithName("GetSecuritySettings")
            .Produces<SecuritySettingsDto>();

        app.MapPut("/api/security/settings", (
                HttpContext context,
                SecuritySettingsRequest request,
                ApiSecurityStore store,
                ApiAccessGuard guard,
                ApiSessionStore sessions,
                ILoggerFactory loggerFactory) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                if (request is null)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Es wurde keine Einstellung übergeben.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // Ohne Administrator wäre eine Anmelde-Pflicht eine
                // Aussperrung - das wird abgelehnt.
                if (request.RequireAuthentication && !store.HasAdmin)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "admin_missing",
                            Message = "Es ist kein Administrator eingerichtet. Bitte zuerst Benutzername und Passwort festlegen.",
                        },
                        statusCode: StatusCodes.Status409Conflict);
                }

                store.SetRequireAuthentication(request.RequireAuthentication);

                if (!request.RequireAuthentication)
                {
                    // Ohne Anmelde-Pflicht sind offene Sitzungen sinnlos.
                    sessions.Clear();
                }

                var logger = loggerFactory.CreateLogger("ComfoBox.Gateway.Api");
                logger.LogInformation(
                    "Die Anmelde-Pflicht wurde auf {Required} gesetzt.",
                    request.RequireAuthentication ? "an" : "aus");

                return Results.NoContent();
            })
            .WithTags("Sicherheit")
            .WithName("UpdateSecuritySettings")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status403Forbidden)
            .Produces<ApiError>(StatusCodes.Status409Conflict);

        app.MapPut("/api/security/admin", (
                HttpContext context,
                UpdateAdminRequest request,
                ApiSecurityStore store,
                ApiAccessGuard guard,
                ApiSessionStore sessions,
                ILoggerFactory loggerFactory) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                if (request is null)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Es wurden keine Zugangsdaten übergeben.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // Wer ein bestehendes Passwort ändert, muss es kennen. Ein
                // gestohlenes Sitzungs-Cookie genügt damit nicht.
                if (store.HasAdmin
                    && !store.VerifyAdmin(store.AdminUsername, request.CurrentPassword))
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_credentials",
                            Message = "Das aktuelle Passwort ist falsch.",
                        },
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var username = string.IsNullOrWhiteSpace(request.Username)
                    ? store.AdminUsername
                    : request.Username.Trim();

                if (string.IsNullOrWhiteSpace(username) || username.Length < 3)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Der Benutzername muss mindestens 3 Zeichen lang sein.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var passwordChanged = !string.IsNullOrEmpty(request.NewPassword);

                // Ein Benutzername ohne Passwort ist nur dann sinnvoll, wenn
                // bereits ein Passwort gesetzt ist.
                if (!passwordChanged && !store.HasAdmin)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Es wurde kein neues Passwort übergeben.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (passwordChanged)
                {
                    var hints = PasswordPolicy.Validate(request.NewPassword);

                    if (hints.Count > 0)
                    {
                        return Results.Json(
                            new ApiError
                            {
                                Error = "weak_password",
                                Message = "Das Passwort entspricht nicht den Regeln: " + string.Join(" ", hints),
                            },
                            statusCode: StatusCodes.Status422UnprocessableEntity);
                    }
                }

                var hash = passwordChanged
                    ? PasswordHasher.Hash(request.NewPassword!)
                    : store.AdminPasswordHash!;

                store.SetAdmin(username, hash);

                // Nach einer Passwort-Änderung sind alle Sitzungen ungültig -
                // ein abgestelltes Gerät bleibt nicht angemeldet.
                if (passwordChanged)
                {
                    sessions.Clear();
                }

                var logger = loggerFactory.CreateLogger("ComfoBox.Gateway.Api");
                logger.LogInformation(
                    "Der Administrator wurde geändert (Benutzer {UserName}, Passwort geändert: {PasswordChanged}).",
                    username,
                    passwordChanged);

                return Results.NoContent();
            })
            .WithTags("Sicherheit")
            .WithName("UpdateAdmin")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized)
            .Produces<ApiError>(StatusCodes.Status422UnprocessableEntity);
    }
}
