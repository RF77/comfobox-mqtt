namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Der Zustand der Web-Sitzung.
/// </summary>
public sealed record SessionDto
{
    /// <summary>
    /// Ist der Benutzer angemeldet?
    /// </summary>
    public required bool Authenticated { get; init; }

    /// <summary>
    /// Der Benutzername des angemeldeten Benutzers (oder <c>null</c>).
    /// </summary>
    public string? UserName { get; init; }

    /// <summary>
    /// Ist die Ersteinrichtung abgeschlossen?
    /// </summary>
    public required bool SetupCompleted { get; init; }

    /// <summary>
    /// Müssen alle Anfragen authentifiziert werden?
    /// </summary>
    public required bool RequireAuthentication { get; init; }

    /// <summary>
    /// Der Benutzername des Administrators (oder <c>null</c>).
    /// </summary>
    public string? AdminUsername { get; init; }
}

/// <summary>
/// Die Anfrage zur Anmeldung.
/// </summary>
public sealed class LoginRequest
{
    /// <summary>
    /// Der Benutzername.
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Das Passwort im Klartext.
    /// </summary>
    public string? Password { get; set; }
}
