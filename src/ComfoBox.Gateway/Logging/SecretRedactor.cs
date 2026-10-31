using System.Text.RegularExpressions;

namespace ComfoBox.Gateway.Logging;

/// <summary>
/// Maskiert Geheimnisse, bevor ein Log-Eintrag gespeichert wird.
/// </summary>
/// <remarks>
/// Der Log-Ringpuffer ist über die Web-UI einsehbar. Passwörter, Tokens und
/// Connection-Strings dürfen dort **nie** erscheinen.
/// </remarks>
public static partial class SecretRedactor
{
    /// <summary>
    /// Die Platzhalter-Darstellung eines Geheimnisses.
    /// </summary>
    public const string Mask = "••••";

    private static readonly string[] SecretKeys =
    [
        "password",
        "passwd",
        "pwd",
        "token",
        "secret",
        "apikey",
        "api_key",
        "connectionstring",
        "connection_string",
        "authorization",
        "credential",
        "credentials",
    ];

    /// <summary>
    /// Maskiert den Wert einer Property, wenn der Name auf ein Geheimnis hindeutet.
    /// </summary>
    /// <param name="key">Der Name der Property.</param>
    /// <param name="value">Der Wert der Property.</param>
    public static string Redact(string key, string value)
        => IsSecretKey(key) ? Mask : RedactText(value);

    /// <summary>
    /// Prüft, ob ein Property-Name auf ein Geheimnis hindeutet.
    /// </summary>
    /// <param name="key">Der Name der Property.</param>
    public static bool IsSecretKey(string key)
    {
        if (string.IsNullOrEmpty(key))
        {
            return false;
        }

        foreach (var secret in SecretKeys)
        {
            if (key.Contains(secret, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Maskiert Passwörter in URLs und Query-Strings.
    /// </summary>
    /// <param name="text">Der zu maskierende Text.</param>
    public static string RedactText(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return text;
        }

        var result = UserInfoInUrl().Replace(text, $"$1{Mask}@");
        return QuerySecret().Replace(result, $"$1={Mask}");
    }

    [GeneratedRegex(@"(?<scheme>[a-zA-Z][a-zA-Z0-9+.\-]*://)(?<user>[^:/?#@\s]+):(?<secret>[^@/?#\s]+)@")]
    private static partial Regex UserInfoInUrl();

    [GeneratedRegex(@"(?<key>\b(?:password|passwd|pwd|token|secret|api[_-]?key)\b)\s*=\s*(?<value>[^&\s""']+)", RegexOptions.IgnoreCase)]
    private static partial Regex QuerySecret();
}
