namespace ComfoBox.Gateway.Security;

/// <summary>
/// Ein API-Token für die Anmeldung an der REST-Schnittstelle.
/// </summary>
/// <remarks>
/// <para>
/// Gespeichert wird **nur** der Hash. Der Klartext des Tokens ist genau
/// einmal sichtbar - bei der Erzeugung.
/// </para>
/// <para>
/// Ein Token kann <b>ablaufen</b> (<see cref="ExpiresAt"/>) und trägt einen
/// <b>Umfang</b> (<see cref="Scope"/>): nur lesen, regeln oder verwalten.
/// </para>
/// </remarks>
/// <param name="Id">Die technische Id (hex, 16 Zeichen).</param>
/// <param name="Name">Der Anzeigename, z. B. <c>home-assistant</c>.</param>
/// <param name="Hash">Der PBKDF2-Hash des Klartext-Tokens.</param>
/// <param name="CreatedAt">Der Zeitpunkt der Erzeugung.</param>
/// <param name="Scope">Der Umfang des Tokens.</param>
/// <param name="ExpiresAt">
/// Der Zeitpunkt des Ablaufs (oder <c>null</c> für „unbegrenzt").
/// </param>
public sealed record ApiToken(
    string Id,
    string Name,
    string Hash,
    DateTimeOffset CreatedAt,
    TokenScope Scope,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>
    /// Darf das Token administrative Aufgaben ausführen?
    /// </summary>
    public bool IsAdmin => Scope == TokenScope.Admin;

    /// <summary>
    /// Darf das Token Werte schreiben?
    /// </summary>
    public bool CanWrite => Scope >= TokenScope.Control;

    /// <summary>
    /// Ist das Token zu dem angegebenen Zeitpunkt abgelaufen?
    /// </summary>
    /// <param name="now">Der zu prüfende Zeitpunkt.</param>
    /// <returns><c>true</c>, wenn das Token abgelaufen ist.</returns>
    public bool IsExpired(DateTimeOffset now) => ExpiresAt is { } expiry && expiry <= now;
}
