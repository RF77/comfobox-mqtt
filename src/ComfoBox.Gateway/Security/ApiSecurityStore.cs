using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using ComfoBox.Gateway.Configuration;

namespace ComfoBox.Gateway.Security;

/// <summary>
/// Der Zugriff auf den Sicherheitszustand: Setup-Flag, Administrator und
/// API-Token.
/// </summary>
/// <remarks>
/// <para>
/// Alles liegt im Abschnitt <c>Api</c> der <c>config.json</c>. Gespeichert
/// werden **nur** Hashes - der Klartext eines Tokens ist genau einmal
/// sichtbar.
/// </para>
/// <para>
/// <b>Geschwindigkeitsproblem:</b> PBKDF2 mit 600.000 Iterationen braucht
/// mehrere hundert Millisekunden. Eine Verifikation wird deshalb für kurze
/// Zeit zwischengespeichert. Der Cache wird bei jeder Änderung des
/// Token-Bestands verworfen - ein widerrufener Token wirkt dadurch
/// <b>sofort</b> nicht mehr.
/// </para>
/// </remarks>
public sealed class ApiSecurityStore
{
    /// <summary>
    /// Der Name des Abschnitts in <c>config.json</c>.
    /// </summary>
    public const string SectionName = "Api";

    /// <summary>
    /// Das Präfix eines API-Tokens.
    /// </summary>
    public const string TokenPrefix = "cbk_";

    private const string TokensKey = nameof(Tokens);
    private const string AdminUsernameKey = nameof(AdminUsername);
    private const string AdminPasswordHashKey = nameof(AdminPasswordHash);
    private const string SetupCompletedKey = nameof(SetupCompleted);

    private readonly ConfigStore _config;
    private readonly TimeProvider _timeProvider;

    private readonly ConcurrentDictionary<string, CachedVerification> _verified = new(StringComparer.Ordinal);

    /// <summary>
    /// Erzeugt den Speicher.
    /// </summary>
    /// <param name="config">Der Zugriff auf die Konfigurationsdatei.</param>
    /// <param name="timeProvider">Die Zeitquelle (für Tests austauschbar).</param>
    public ApiSecurityStore(ConfigStore config, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(config);

        _config = config;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Ist die Ersteinrichtung abgeschlossen?
    /// </summary>
    public bool SetupCompleted => GetBoolean(SectionName, SetupCompletedKey);

    /// <summary>
    /// Der Name des Administrators (oder <c>null</c>).
    /// </summary>
    public string? AdminUsername => GetString(SectionName, AdminUsernameKey);

    /// <summary>
    /// Der PBKDF2-Hash des Administrator-Passworts (oder <c>null</c>).
    /// </summary>
    public string? AdminPasswordHash => GetString(SectionName, AdminPasswordHashKey);

    /// <summary>
    /// Die vorhandenen API-Token (ohne Klartext).
    /// </summary>
    public IReadOnlyList<ApiToken> Tokens
    {
        get
        {
            var section = ReadSection();
            if (section[TokensKey] is not JsonArray array)
            {
                return [];
            }

            var tokens = new List<ApiToken>(array.Count);
            foreach (var node in array)
            {
                if (node is JsonObject entry)
                {
                    tokens.Add(ReadToken(entry));
                }
            }

            return tokens;
        }
    }

    /// <summary>
    /// Erzeugt ein neues API-Token.
    /// </summary>
    /// <param name="name">Der Anzeigename.</param>
    /// <param name="isAdmin">Darf das Token administrative Aufgaben ausführen?</param>
    /// <returns>
    /// Das Token (ohne Klartext) und der **einmalig** sichtbare Klartext.
    /// </returns>
    public (ApiToken Token, string Plaintext) CreateToken(string name, bool isAdmin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var id = RandomNumberGenerator.GetBytes(8);
        var idText = Convert.ToHexString(id).ToLowerInvariant();
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');

        var plaintext = $"{TokenPrefix}{idText}_{secret}";
        var token = new ApiToken(
            idText,
            name,
            PasswordHasher.Hash(plaintext),
            _timeProvider.GetUtcNow(),
            isAdmin);

        _config.Mutate(config =>
        {
            var section = EnsureSection(config);
            var tokens = section[TokensKey] as JsonArray ?? new JsonArray();
            tokens.Add(WriteToken(token));
            section[TokensKey] = tokens;
        });

        return (token, plaintext);
    }

    /// <summary>
    /// Widerruft ein Token.
    /// </summary>
    /// <param name="id">Die Id des Tokens.</param>
    /// <returns><c>true</c>, wenn ein Token entfernt wurde.</returns>
    public bool RemoveToken(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);

        var removed = false;

        _config.Mutate(config =>
        {
            var section = EnsureSection(config);
            if (section[TokensKey] is not JsonArray array)
            {
                return;
            }

            for (var i = array.Count - 1; i >= 0; i--)
            {
                if (array[i] is JsonObject entry
                    && string.Equals(entry["Id"]?.GetValue<string>(), id, StringComparison.Ordinal))
                {
                    array.RemoveAt(i);
                    removed = true;
                }
            }
        });

        if (removed)
        {
            _verified.Clear();
        }

        return removed;
    }

    /// <summary>
    /// Setzt den Administrator (Benutzername und gehashtes Passwort).
    /// </summary>
    /// <param name="username">Der Benutzername.</param>
    /// <param name="passwordHash">Der PBKDF2-Hash des Passworts.</param>
    public void SetAdmin(string username, string passwordHash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);

        _config.Mutate(config =>
        {
            var section = EnsureSection(config);
            section[AdminUsernameKey] = username;
            section[AdminPasswordHashKey] = passwordHash;
        });

        _verified.Clear();
    }

    /// <summary>
    /// Setzt das Flag „Ersteinrichtung abgeschlossen".
    /// </summary>
    /// <param name="completed">Der neue Zustand.</param>
    public void SetSetupCompleted(bool completed)
    {
        _config.Mutate(config =>
        {
            var section = EnsureSection(config);
            section[SetupCompletedKey] = completed;
        });
    }

    /// <summary>
    /// Prüft ein Klartext-Token.
    /// </summary>
    /// <param name="plaintext">Das presented Token.</param>
    /// <returns>
    /// Das Token oder <c>null</c>, wenn es unbekannt oder widerrufen ist.
    /// </returns>
    public ApiToken? VerifyToken(string? plaintext)
    {
        if (string.IsNullOrWhiteSpace(plaintext) || !plaintext.StartsWith(TokenPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var id = ExtractId(plaintext);
        if (id is null)
        {
            return null;
        }

        var token = FindToken(id);
        if (token is null)
        {
            return null;
        }

        var fingerprint = Fingerprint(plaintext);

        if (_verified.TryGetValue(id, out var cached)
            && cached.Version == _config.Version
            && CryptographicOperations.FixedTimeEquals(cached.Fingerprint, fingerprint))
        {
            return token;
        }

        if (!PasswordHasher.Verify(plaintext, token.Hash))
        {
            return null;
        }

        _verified[id] = new CachedVerification(_config.Version, fingerprint);
        return token;
    }

    /// <summary>
    /// Prüft die Administrator-Zugangsdaten.
    /// </summary>
    /// <param name="username">Der Benutzername.</param>
    /// <param name="password">Das Klartext-Passwort.</param>
    /// <returns><c>true</c>, wenn die Zugangsdaten stimmen.</returns>
    public bool VerifyAdmin(string? username, string? password)
    {
        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            return false;
        }

        var expectedUser = AdminUsername;
        var expectedHash = AdminPasswordHash;

        if (string.IsNullOrEmpty(expectedUser) || string.IsNullOrEmpty(expectedHash))
        {
            return false;
        }

        if (!string.Equals(username, expectedUser, StringComparison.Ordinal))
        {
            return false;
        }

        return PasswordHasher.Verify(password, expectedHash);
    }

    /// <summary>
    /// Findet ein Token über seine Id.
    /// </summary>
    /// <param name="id">Die Id.</param>
    /// <returns>Das Token oder <c>null</c>.</returns>
    public ApiToken? FindToken(string id)
    {
        foreach (var token in Tokens)
        {
            if (string.Equals(token.Id, id, StringComparison.Ordinal))
            {
                return token;
            }
        }

        return null;
    }

    /// <summary>
    /// Liest den Abschnitt „Api" aus der Konfiguration.
    /// </summary>
    private JsonObject ReadSection()
    {
        var config = _config.Read();
        return config[SectionName] as JsonObject ?? new JsonObject();
    }

    /// <summary>
    /// Holt (oder erzeugt) den Abschnitt „Api" in einer zu schreibenden Konfiguration.
    /// </summary>
    private static JsonObject EnsureSection(JsonObject config)
    {
        if (config[SectionName] is JsonObject existing)
        {
            return existing;
        }

        var section = new JsonObject();
        config[SectionName] = section;
        return section;
    }

    /// <summary>
    /// Liest einen booleschen Wert aus einem Abschnitt.
    /// </summary>
    private bool GetBoolean(string section, string key)
    {
        var value = ReadSection()[key];
        return value is not null && value.GetValueKind() == System.Text.Json.JsonValueKind.True;
    }

    /// <summary>
    /// Liest einen Textwert aus einem Abschnitt.
    /// </summary>
    private string? GetString(string section, string key)
    {
        var value = ReadSection()[key];
        return value is null ? null : value.GetValue<string>();
    }

    /// <summary>
    /// Liest ein Token aus einem JSON-Objekt.
    /// </summary>
    private static ApiToken ReadToken(JsonObject entry) => new(
        entry["Id"]?.GetValue<string>() ?? string.Empty,
        entry["Name"]?.GetValue<string>() ?? string.Empty,
        entry["Hash"]?.GetValue<string>() ?? string.Empty,
        entry["CreatedAt"]?.GetValue<DateTimeOffset>() ?? DateTimeOffset.MinValue,
        entry["IsAdmin"]?.GetValue<bool>() ?? false);

    /// <summary>
    /// Schreibt ein Token als JSON-Objekt.
    /// </summary>
    private static JsonObject WriteToken(ApiToken token) => new()
    {
        ["Id"] = token.Id,
        ["Name"] = token.Name,
        ["Hash"] = token.Hash,
        ["CreatedAt"] = token.CreatedAt,
        ["IsAdmin"] = token.IsAdmin,
    };

    /// <summary>
    /// Ermittelt die Token-ID aus dem Klartext-Token.
    /// </summary>
    private static string? ExtractId(string plaintext)
    {
        // Format: cbk_<id>_<secret>
        var start = plaintext.IndexOf('_', StringComparison.Ordinal);
        if (start < 0)
        {
            return null;
        }

        var end = plaintext.IndexOf('_', start + 1);
        if (end < 0)
        {
            return null;
        }

        var id = plaintext.Substring(start + 1, end - start - 1);
        return id.Length == 0 ? null : id;
    }

    /// <summary>
    /// Der Fingerabdruck eines Klartext-Tokens (SHA-256) - nur für den Cache.
    /// </summary>
    private static byte[] Fingerprint(string plaintext)
        => SHA256.HashData(Encoding.UTF8.GetBytes(plaintext));

    /// <summary>
    /// Ein zwischengespeichertes Prüfergebnis.
    /// </summary>
    /// <param name="Version">Die Version des Token-Bestands.</param>
    /// <param name="Fingerprint">Der Fingerabdruck des geprüften Tokens.</param>
    private sealed record CachedVerification(long Version, byte[] Fingerprint);
}
