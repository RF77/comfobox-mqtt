using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Security;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der Web-Anmeldung.
/// </summary>
/// <remarks>
/// <para>
/// Die Oberfläche meldet sich mit Benutzername und Passwort an und erhält ein
/// <b>HttpOnly-Cookie</b> mit einem zufälligen Sitzungskennzeichen. Die
/// Sitzung liegt serverseitig und kann deshalb widerrufen werden (→ ADR-012).
/// </para>
/// <para>
/// <b>PBKDF2 ist teuer</b> (600.000 Iterationen ≈ 0,3 s) - die Oberfläche
/// muss deshalb sofort eine Rückmeldung geben, dass die Anmeldung läuft.
/// </para>
/// </remarks>
internal static class AuthEndpoints
{
    /// <summary>
    /// Der Name des Sitzungs-Cookies.
    /// </summary>
    public const string CookieName = "cbk_session";

    /// <summary>
    /// Registriert die Anmelde-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapAuth(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/auth/session", (
                HttpContext context,
                ApiSessionStore sessions,
                ApiSecurityStore security,
                ApiAccessGuard guard) =>
            {
                var identity = context.Request.Cookies[CookieName];
                var userName = sessions.Validate(identity);

                return Results.Ok(new SessionDto
                {
                    Authenticated = userName is not null,
                    UserName = userName,
                    SetupCompleted = security.SetupCompleted,
                    RequireAuthentication = guard.AuthenticationRequired,
                    AdminUsername = security.AdminUsername,
                });
            })
            .WithTags("Sicherheit")
            .WithName("GetSession")
            .Produces<SessionDto>();

        app.MapPost("/api/auth/login", (
                HttpContext context,
                LoginRequest request,
                ApiSessionStore sessions,
                ApiSecurityStore security,
                ApiAccessGuard guard,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("ComfoBox.Gateway.Api");

                if (request is null || string.IsNullOrWhiteSpace(request.Username))
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Es wurden Benutzername und Passwort erwartet.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                // Vor der Ersteinrichtung gibt es noch keinen Administrator -
                // dann ist die Anmeldung noch nicht nötig (der Assistent läuft
                // ohne Sitzung).
                if (!security.SetupCompleted)
                {
                    return Results.Ok(new SessionDto
                    {
                        Authenticated = false,
                        UserName = null,
                        SetupCompleted = false,
                        RequireAuthentication = guard.AuthenticationRequired,
                        AdminUsername = null,
                    });
                }

                if (!security.VerifyAdmin(request.Username, request.Password))
                {
                    logger.LogWarning(
                        "Eine Anmeldung mit dem Benutzernamen {UserName} ist fehlgeschlagen.",
                        request.Username);

                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_credentials",
                            Message = "Benutzername oder Passwort ist falsch.",
                        },
                        statusCode: StatusCodes.Status401Unauthorized);
                }

                var token = sessions.Create(request.Username.Trim());

                // HttpOnly: das JavaScript der Seite kann das Kennzeichen nicht
                // lesen. SameSite=Strict: der Cookie wandert nur bei eigenen
                // Anfragen mit - ein fremder kann ihn nicht mitbenutzen.
                context.Response.Cookies.Append(
                    CookieName,
                    token,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Secure = context.Request.IsHttps,
                        IsEssential = true,
                        Expires = DateTimeOffset.UtcNow.Add(sessions.Lifetime),
                    });

                logger.LogInformation("Der Benutzer {UserName} hat sich angemeldet.", request.Username);

                return Results.Ok(new SessionDto
                {
                    Authenticated = true,
                    UserName = request.Username.Trim(),
                    SetupCompleted = true,
                    RequireAuthentication = guard.AuthenticationRequired,
                    AdminUsername = security.AdminUsername,
                });
            })
            .WithTags("Sicherheit")
            .WithName("Login")
            .Produces<SessionDto>()
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized);

        app.MapPost("/api/auth/logout", (
                HttpContext context,
                ApiSessionStore sessions) =>
            {
                var token = context.Request.Cookies[CookieName];
                sessions.Remove(token);

                context.Response.Cookies.Delete(
                    CookieName,
                    new CookieOptions
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.Strict,
                        Secure = context.Request.IsHttps,
                        IsEssential = true,
                    });

                return Results.NoContent();
            })
            .WithTags("Sicherheit")
            .WithName("Logout")
            .Produces(StatusCodes.Status204NoContent);
    }
}