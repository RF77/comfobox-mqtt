using ComfoBox.Gateway.Security;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Die Anfrage zur Erzeugung eines API-Tokens.
/// </summary>
public sealed class CreateTokenRequest
{
    /// <summary>
    /// Der Anzeigename des Tokens, z. B. <c>home-assistant</c>.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Darf das Token administrative Aufgaben ausführen?
    /// </summary>
    public bool IsAdmin { get; set; }
}

/// <summary>
/// Das Ergebnis der Token-Erzeugung.
/// </summary>
/// <remarks>
/// <c>token</c> ist der **einzige** Moment, in dem der Klartext zu sehen ist.
/// </remarks>
public sealed record CreatedTokenDto
{
    /// <summary>
    /// Die technische Id des Tokens.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Der Anzeigename des Tokens.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Der Klartext des Tokens - **nur jetzt** sichtbar.
    /// </summary>
    public required string Token { get; init; }

    /// <summary>
    /// Der Zeitpunkt der Erzeugung.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Darf das Token administrative Aufgaben ausführen?
    /// </summary>
    public required bool IsAdmin { get; init; }
}

/// <summary>
/// Die Beschreibung eines Tokens (ohne Klartext).
/// </summary>
public sealed record TokenInfoDto
{
    /// <summary>
    /// Die technische Id des Tokens.
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Der Anzeigename des Tokens.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Der Zeitpunkt der Erzeugung.
    /// </summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>
    /// Darf das Token administrative Aufgaben ausführen?
    /// </summary>
    public required bool IsAdmin { get; init; }

    /// <summary>
    /// Erzeugt die Beschreibung eines Tokens.
    /// </summary>
    /// <param name="token">Das Token.</param>
    /// <returns>Die Beschreibung.</returns>
    public static TokenInfoDto From(ApiToken token)
    {
        ArgumentNullException.ThrowIfNull(token);

        return new TokenInfoDto
        {
            Id = token.Id,
            Name = token.Name,
            CreatedAt = token.CreatedAt,
            IsAdmin = token.IsAdmin,
        };
    }
}
