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
                ApiAccessGuard guard) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                // Nur die Metadaten - die Hashes werden nie ausgegeben.
                var tokens = store.Tokens.Select(TokenInfoDto.From).ToArray();
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

                var (token, plaintext) = store.CreateToken(name, request?.IsAdmin ?? false);

                // Der Klartext ist **nur** in dieser Antwort sichtbar.
                return Results.Created(
                    $"/api/security/tokens/{token.Id}",
                    new CreatedTokenDto
                    {
                        Id = token.Id,
                        Name = token.Name,
                        Token = plaintext,
                        CreatedAt = token.CreatedAt,
                        IsAdmin = token.IsAdmin,
                    });
            })
            .WithTags("Sicherheit")
            .WithName("CreateToken")
            .Produces<CreatedTokenDto>(StatusCodes.Status201Created)
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
}
