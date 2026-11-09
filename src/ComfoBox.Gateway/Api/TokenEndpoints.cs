using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Security;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der API-Token.
/// </summary>
internal static class TokenEndpoints
{
    /// <summary>
    /// Registriert die Token-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapTokens(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/security/tokens", (
                HttpContext context,
                ApiSecurityStore store,
                ApiAccessGuard guard,
                TimeProvider timeProvider) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                // Nur die Metadaten - die Hashes werden nie ausgegeben.
                var now = timeProvider.GetUtcNow();
                var tokens = store.Tokens.Select(token => TokenInfoDto.From(token, now)).ToArray();
                return Results.Ok(tokens);
            })
            .WithTags("Sicherheit")
            .WithName("GetTokens");

        app.MapPost("/api/security/tokens", (
                HttpContext context,
                CreateTokenRequest request,
                ApiSecurityStore store,
                ApiAccessGuard guard) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                var name = string.IsNullOrWhiteSpace(request?.Name)
                    ? "api-token"
                    : request!.Name.Trim();

                // Der Umfang ist der Regler für die Rechte. Die alte
                // Schreibweise `isAdmin` bleibt gültig.
                var scope = ParseScope(request?.Scope, request?.IsAdmin ?? false);

                if (scope is null)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Der Umfang muss 'read', 'control' oder 'admin' sein.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var days = request?.ExpiresInDays ?? 0;

                if (days is < 0 or > 3650)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Die Gültigkeitsdauer muss zwischen 0 und 3650 Tagen liegen (0 = unbegrenzt).",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var lifetime = days == 0 ? (TimeSpan?)null : TimeSpan.FromDays(days);

                var (token, plaintext) = store.CreateToken(name, scope.Value, lifetime);

                // Der Klartext ist **nur** in dieser Antwort sichtbar.
                return Results.Created(
                    $"/api/security/tokens/{token.Id}",
                    new CreatedTokenDto
                    {
                        Id = token.Id,
                        Name = token.Name,
                        Token = plaintext,
                        CreatedAt = token.CreatedAt,
                        Scope = TokenInfoDto.ScopeName(token.Scope),
                        ExpiresAt = token.ExpiresAt,
                        IsAdmin = token.IsAdmin,
                    });
            })
            .WithTags("Sicherheit")
            .WithName("CreateToken")
            .Produces<CreatedTokenDto>(StatusCodes.Status201Created)
            .Produces<ApiError>(StatusCodes.Status400BadRequest)
            .Produces<ApiError>(StatusCodes.Status401Unauthorized)
            .Produces<ApiError>(StatusCodes.Status403Forbidden);

        app.MapDelete("/api/security/tokens/{id}", (
                HttpContext context,
                string id,
                ApiSecurityStore store,
                ApiAccessGuard guard) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                if (!store.RemoveToken(id))
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "token_not_found",
                            Message = $"Es gibt kein Token mit der Id '{id}'.",
                        },
                        statusCode: StatusCodes.Status404NotFound);
                }

                return Results.NoContent();
            })
            .WithTags("Sicherheit")
            .WithName("DeleteToken")
            .Produces(StatusCodes.Status204NoContent)
            .Produces<ApiError>(StatusCodes.Status404NotFound);
    }

    /// <summary>
    /// Übersetzt den Umfang aus der Anfrage.
    /// </summary>
    /// <param name="scope">Der Text (<c>read</c>, <c>control</c>, <c>admin</c>).</param>
    /// <param name="legacyIsAdmin">Die alte Schreibweise.</param>
    /// <returns>Der Umfang oder <c>null</c>, wenn der Text unbekannt ist.</returns>
    private static TokenScope? ParseScope(string? scope, bool legacyIsAdmin)
    {
        if (string.IsNullOrWhiteSpace(scope))
        {
            return legacyIsAdmin ? TokenScope.Admin : TokenScope.Control;
        }

        return scope.Trim().ToLowerInvariant() switch
        {
            "read" or "readonly" or "read-only" => TokenScope.Read,
            "control" or "write" => TokenScope.Control,
            "admin" or "advanced" => TokenScope.Admin,
            _ => null,
        };
    }
}
