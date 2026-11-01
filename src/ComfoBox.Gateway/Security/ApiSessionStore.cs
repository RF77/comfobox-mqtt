using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace ComfoBox.Gateway.Security;

/// <summary>
/// Die serverseitigen Sitzungen der Web-Oberfläche.
/// </summary>
/// <remarks>
/// <para>
/// Die Sitzungen liegen **serverseitig** - im Browser steht nur ein
/// zufälliges Sitzungskennzeichen (HttpOnly-Cookie). Eine Sitzung kann
/// deshalb jederzeit widerrufen werden (z. B. beim Abmelden).
/// </para>
/// <para>
/// <b>Warum kein JWT?</b> Ein JWT kann nicht widerrufen werden - ein
/// abgestelltes Gerät bliebe angemeldet, bis der Token abgelaufen ist.
/// </para>
/// </remarks>
public sealed class ApiSessionStore
{
    private readonly ConcurrentDictionary<string, Session> _sessions = new(StringComparer.Ordinal);
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _lifetime;

    /// <summary>
    /// Erzeugt den Speicher.
    /// </summary>
    /// <param name="timeProvider">Die Zeitquelle (für Tests austauschbar).</param>
    /// <param name="lifetimeMinutes">Die Lebensdauer einer Sitzung in Minuten.</param>
    public ApiSessionStore(TimeProvider? timeProvider = null, int lifetimeMinutes = 480)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(lifetimeMinutes);

        _timeProvider = timeProvider ?? TimeProvider.System;
        _lifetime = TimeSpan.FromMinutes(lifetimeMinutes);
    }

    /// <summary>
    /// Die Lebensdauer einer Sitzung.
    /// </summary>
    public TimeSpan Lifetime => _lifetime;

    /// <summary>
    /// Erzeugt eine neue Sitzung.
    /// </summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <returns>Das Sitzungskennzeichen (der Klartext des Cookies).</returns>
    public string Create(string userName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userName);

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        _sessions[token] = new Session(userName, _timeProvider.GetUtcNow());

        return token;
    }

    /// <summary>
    /// Prüft eine Sitzung und liefert den Benutzernamen.
    /// </summary>
    /// <param name="token">Das Sitzungskennzeichen (oder <c>null</c>).</param>
    /// <returns>Der Benutzername - oder <c>null</c>, wenn die Sitzung unbekannt oder abgelaufen ist.</returns>
    public string? Validate(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        if (!_sessions.TryGetValue(token, out var session))
        {
            return null;
        }

        if (_timeProvider.GetUtcNow() - session.CreatedAt > _lifetime)
        {
            _sessions.TryRemove(token, out _);
            return null;
        }

        return session.UserName;
    }

    /// <summary>
    /// Beendet eine Sitzung.
    /// </summary>
    /// <param name="token">Das Sitzungskennzeichen.</param>
    /// <returns><c>true</c>, wenn eine Sitzung beendet wurde.</returns>
    public bool Remove(string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return false;
        }

        return _sessions.TryRemove(token, out _);
    }

    /// <summary>
    /// Beendet alle Sitzungen (z. B. wenn sich das Administrator-Passwort ändert).
    /// </summary>
    public void Clear() => _sessions.Clear();

    /// <summary>
    /// Eine Sitzung.
    /// </summary>
    /// <param name="UserName">Der Benutzername.</param>
    /// <param name="CreatedAt">Der Zeitpunkt der Erzeugung.</param>
    private sealed record Session(string UserName, DateTimeOffset CreatedAt);
}
