namespace ComfoBox.Gateway.Security;

/// <summary>
/// Die Regeln für das Passwort des Administrators.
/// </summary>
/// <remarks>
/// <para>
/// Dieselben Regeln gelten in der Oberfläche (Assistent) und im Gateway:
/// mindestens 12 Zeichen und mindestens 3 von 4 Zeichenklassen
/// (Großbuchstaben, Kleinbuchstaben, Ziffern, Symbole).
/// </para>
/// <para>
/// Die Regeln stehen **serverseitig**, damit ein API-Aufrufer sie nicht
/// umgehen kann - die Oberfläche prüft nur zusätzlich, damit der Benutzer
/// das Ergebnis sofort sieht.
/// </para>
/// </remarks>
public static class PasswordPolicy
{
    /// <summary>
    /// Die Mindestlänge des Passworts.
    /// </summary>
    public const int MinimumLength = 12;

    /// <summary>
    /// Die Anzahl der Zeichenklassen, die mindestens vorkommen müssen.
    /// </summary>
    public const int MinimumClasses = 3;

    /// <summary>
    /// Die Anzahl der Zeichenklassen, die es überhaupt gibt.
    /// </summary>
    public const int ClassCount = 4;

    /// <summary>
    /// Prüft das Passwort und liefert die offenen Hinweise.
    /// </summary>
    /// <param name="password">Das Passwort (oder <c>null</c>).</param>
    /// <returns>
    /// Die Hinweise - leer, wenn das Passwort den Regeln entspricht.
    /// </returns>
    public static IReadOnlyList<string> Validate(string? password)
    {
        var hints = new List<string>();

        if (string.IsNullOrEmpty(password))
        {
            hints.Add("Das Passwort darf nicht leer sein.");
            return hints;
        }

        if (password.Length < MinimumLength)
        {
            hints.Add($"Mindestens {MinimumLength} Zeichen.");
        }

        var classes = CountClasses(password);

        if (classes < MinimumClasses)
        {
            hints.Add($"Mindestens {MinimumClasses} von {ClassCount} Zeichenklassen (Groß-, Kleinbuchstaben, Ziffern, Symbole).");
        }

        return hints;
    }

    /// <summary>
    /// Entspricht das Passwort den Regeln?
    /// </summary>
    /// <param name="password">Das Passwort (oder <c>null</c>).</param>
    /// <returns><c>true</c>, wenn das Passwort gültig ist.</returns>
    public static bool IsValid(string? password) => Validate(password).Count == 0;

    /// <summary>
    /// Zählt die vorkommenden Zeichenklassen.
    /// </summary>
    /// <param name="password">Das Passwort.</param>
    /// <returns>
    /// Die Anzahl der Klassen: Großbuchstaben, Kleinbuchstaben, Ziffern, Symbole.
    /// </returns>
    public static int CountClasses(string password)
    {
        ArgumentNullException.ThrowIfNull(password);

        var classes = 0;

        if (password.Any(char.IsUpper))
        {
            classes++;
        }

        if (password.Any(char.IsLower))
        {
            classes++;
        }

        if (password.Any(char.IsDigit))
        {
            classes++;
        }

        if (password.Any(c => !char.IsLetterOrDigit(c) && !char.IsWhiteSpace(c)))
        {
            classes++;
        }

        return classes;
    }
}
