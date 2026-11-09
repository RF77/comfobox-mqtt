using ComfoBox.Gateway.Security;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Die Anfrage zur Erzeugung eines API-Tokens.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Scope"/> ist der Regler für die Rechte:
/// <c>read</c> (nur lesen), <c>control</c> (regeln) oder <c>admin</c>
/// (verwalten, „Advanced Mode").
/// </para>
/// <para>
/// <see cref="ExpiresInDays"/> steuert die Gültigkeitsdauer. <c>null</c> oder
/// <c>0</c> bedeutet: kein Ablaufdatum.
/// </para>
/// </remarks>
public sealed class CreateTokenRequest
{
    /// <summary>
    /// Der Anzeigename des Tokens, z. B. <c>home-assistant</c>.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Der Umfang: <c>read</c>, <c>control</c> oder <c>admin</c>.
    /// </summary>
    public string? Scope { get; set; }

    /// <summary>
    /// Die Gültigkeitsdauer in Tagen (<c>null</c> oder <c>0</c> = unbegrenzt).
    /// </summary>
    public int? ExpiresInDays { get; set; }

    /// <summary>
    /// Alte Schreibweise für „administratives Token" (<c>scope = admin</c>).
    /// </summary>
    /// <remarks>
    /// Bleibt gültig, damit ein älterer Aufrufer nicht bricht; <see cref="Scope"/>
    /// hat Vorrang.
    /// </remarks>
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
    /// Der Umfang des Tokens (<c>read</c>, <c>control</c>, <c>admin</c>).
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Der Zeitpunkt des Ablaufs (oder <c>null</c> für „unbegrenzt").
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

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
    /// Der Umfang des Tokens (<c>read</c>, <c>control</c>, <c>admin</c>).
    /// </summary>
    public required string Scope { get; init; }

    /// <summary>
    /// Der Zeitpunkt des Ablaufs (oder <c>null</c> für „unbegrenzt").
    /// </summary>
    public DateTimeOffset? ExpiresAt { get; init; }

    /// <summary>
    /// Ist das Token bereits abgelaufen?
    /// </summary>
    public required bool Expired { get; init; }

    /// <summary>
    /// Darf das Token administrative Aufgaben ausführen?
    /// </summary>
    public required bool IsAdmin { get; init; }

    /// <summary>
    /// Erzeugt die Beschreibung eines Tokens.
    /// </summary>
    /// <param name="token">Das Token.</param>
    /// <param name="now">Der aktuelle Zeitpunkt.</param>
    /// <returns>Die Beschreibung.</returns>
    public static TokenInfoDto From(ApiToken token, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(token);

        return new TokenInfoDto
        {
            Id = token.Id,
            Name = token.Name,
            CreatedAt = token.CreatedAt,
            Scope = ScopeName(token.Scope),
            ExpiresAt = token.ExpiresAt,
            Expired = token.IsExpired(now),
            IsAdmin = token.IsAdmin,
        };
    }

    /// <summary>
    /// Der Name eines Umfangs (für JSON und die Oberfläche).
    /// </summary>
    /// <param name="scope">Der Umfang.</param>
    /// <returns><c>read</c>, <c>control</c> oder <c>admin</c>.</returns>
    public static string ScopeName(TokenScope scope) => scope switch
    {
        TokenScope.Read => "read",
        TokenScope.Control => "control",
        _ => "admin",
    };
}

/// <summary>
/// Die Anfrage zum Ändern des Administrators.
/// </summary>
public sealed class UpdateAdminRequest
{
    /// <summary>
    /// Das aktuelle Passwort (Pflicht, sobald ein Administrator existiert).
    /// </summary>
    public string? CurrentPassword { get; set; }

    /// <summary>
    /// Der neue Benutzername (oder <c>null</c> für „unverändert").
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Das neue Passwort (oder <c>null</c> für „unverändert").
    /// </summary>
    public string? NewPassword { get; set; }
}

/// <summary>
/// Die Anfrage zum Umschalten der Anmelde-Pflicht.
/// </summary>
public sealed class SecuritySettingsRequest
{
    /// <summary>
    /// Muss sich die Oberfläche mit Benutzername und Passwort anmelden?
    /// </summary>
    public bool RequireAuthentication { get; set; }
}

/// <summary>
/// Der Zustand der Zugriffssicherheit.
/// </summary>
public sealed record SecuritySettingsDto
{
    /// <summary>
    /// Ist die Ersteinrichtung abgeschlossen?
    /// </summary>
    public required bool SetupCompleted { get; init; }

    /// <summary>
    /// Ist ein Administrator eingerichtet?
    /// </summary>
    public required bool HasAdmin { get; init; }

    /// <summary>
    /// Der Benutzername des Administrators (oder <c>null</c>).
    /// </summary>
    public string? AdminUsername { get; init; }

    /// <summary>
    /// Muss jede Anfrage authentifiziert sein?
    /// </summary>
    public required bool RequireAuthentication { get; init; }
}
