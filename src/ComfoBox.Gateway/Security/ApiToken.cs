namespace ComfoBox.Gateway.Security;

/// <summary>
/// Ein API-Token für die Anmeldung an der REST-Schnittstelle.
/// </summary>
/// <remarks>
/// Gespeichert wird **nur** der Hash. Der Klartext des Tokens ist genau
/// einmal sichtbar - bei der Erzeugung.
/// </remarks>
/// <param name="Id">Die technische Id (hex, 16 Zeichen).</param>
/// <param name="Name">Der Anzeigename, z. B. <c>home-assistant</c>.</param>
/// <param name="Hash">Der PBKDF2-Hash des Klartext-Tokens.</param>
/// <param name="CreatedAt">Der Zeitpunkt der Erzeugung.</param>
/// <param name="IsAdmin">Darf das Token administrative Aufgaben ausführen?</param>
public sealed record ApiToken(
    string Id,
    string Name,
    string Hash,
    DateTimeOffset CreatedAt,
    bool IsAdmin);
