using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace ComfoBox.Gateway.Security;

/// <summary>
/// Das Hashen von Passwörtern und API-Token.
/// </summary>
/// <remarks>
/// <para>
/// PBKDF2-HMAC-SHA256 mit <see cref="DefaultIterations"/> Iterationen. Das
/// gespeicherte Format ist:
/// </para>
/// <code>pbkdf2-sha256$600000$&lt;Salt-Base64&gt;$&lt;Hash-Base64&gt;</code>
/// </remarks>
public static class PasswordHasher
{
    /// <summary>
    /// Der Algorithmus-Name im gespeicherten Format.
    /// </summary>
    public const string Algorithm = "pbkdf2-sha256";

    /// <summary>
    /// Die Anzahl der Iterationen.
    /// </summary>
    public const int DefaultIterations = 600_000;

    /// <summary>
    /// Die Länge des Salts in Bytes.
    /// </summary>
    public const int SaltSize = 16;

    /// <summary>
    /// Die Länge des abgeleiteten Hashes in Bytes.
    /// </summary>
    public const int HashSize = 32;

    /// <summary>
    /// Erzeugt den Hash eines Passworts.
    /// </summary>
    /// <param name="password">Das Klartext-Passwort.</param>
    /// <param name="iterations">Die Anzahl der Iterationen.</param>
    /// <returns>Der Hash im Format <c>pbkdf2-sha256$…</c>.</returns>
    public static string Hash(string password, int iterations = DefaultIterations)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(iterations);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            HashSize);

        return string.Join(
            '$',
            Algorithm,
            iterations.ToString(CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <summary>
    /// Prüft ein Passwort gegen einen gespeicherten Hash.
    /// </summary>
    /// <param name="password">Das Klartext-Passwort.</param>
    /// <param name="encoded">Der gespeicherte Hash.</param>
    /// <returns>
    /// <c>true</c>, wenn das Passwort zum Hash passt. Ein kaputter oder
    /// unbekannter Hash ergibt <c>false</c> - **keine** Ausnahme.
    /// </returns>
    public static bool Verify(string? password, string? encoded)
    {
        if (string.IsNullOrEmpty(password) || string.IsNullOrEmpty(encoded))
        {
            return false;
        }

        var parts = encoded.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Algorithm, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var iterations)
            || iterations <= 0)
        {
            return false;
        }

        byte[] salt;
        byte[] expected;

        try
        {
            salt = Convert.FromBase64String(parts[2]);
            expected = Convert.FromBase64String(parts[3]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actual = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            iterations,
            HashAlgorithmName.SHA256,
            expected.Length);

        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
