using System.Text;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Api.Models;

namespace ComfoBox.Gateway.Security;

/// <summary>
/// Die Identität des Aufrufers einer API-Anfrage.
/// </summary>
/// <param name="IsAuthenticated">Ist der Aufrufer authentifiziert?</param>
/// <param name="Name">Der Anzeigename (Token-Name oder Benutzername).</param>
/// <param name="IsAdmin">Darf der Aufrufer administrative Aufgaben ausführen?</param>
/// <param name="TokenId">Die Id des verwendeten Tokens (oder <c>null</c>).</param>
public sealed record ApiIdentity(bool IsAuthenticated, string? Name, bool IsAdmin, string? TokenId)
{
    /// <summary>
    /// Der anonyme Aufrufer.
    /// </summary>
    public static ApiIdentity Anonymous { get; } = new(false, null, false, null);
}

/// <summary>
/// Die Zugriffskontrolle der REST-Schnittstelle.
/// </summary>
/// <remarks>
/// <para>
/// Die Identität wird aus dem <c>Authorization</c>-Header ermittelt:
/// </para>
/// <list type="bullet">
/// <item><c>Bearer &lt;token&gt;</c> - ein API-Token aus <c>config.json</c>;</item>
/// <item><c>Basic &lt;base64&gt;</c> - der Administrator (Benutzername/Passwort).</item>
/// </list>
/// <para>
/// <b>Warum kein ASP.NET-Authentifizierungs-Framework?</b> Die Reihenfolge der
/// Middleware (Authentication → Authorization → Endpunkt) ist eine typische
/// Fehlerquelle. Die Prüfung passiert deshalb **explizit im Endpunkt** -
/// damit ist das Verhalten eindeutig und direkt testbar.
/// </para>
/// </remarks>
public sealed class ApiAccessGuard
{
    private const string IdentityKey = "ComfoBox.ApiIdentity";

    private readonly ApiSecurityStore _store;
    private readonly ApiSessionStore _sessions;
    private readonly ApiOptions _options;

    /// <summary>
    /// Erzeugt die Zugriffskontrolle.
    /// </summary>
    /// <param name="store">Der Sicherheitsspeicher.</param>
    /// <param name="sessions">Der Sitzungsspeicher (Cookie-Sitzungen).</param>
    /// <param name="options">Die API-Einstellungen.</param>
    public ApiAccessGuard(
        ApiSecurityStore store,
        ApiSessionStore sessions,
        ApiOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(options);

        _store = store;
        _sessions = sessions;
        _options = options;
    }

    /// <summary>
    /// Ermittelt die Identität des Aufrufers (das Ergebnis wird pro Anfrage zwischengespeichert).
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <returns>Die Identität.</returns>
    public ApiIdentity GetIdentity(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (context.Items.TryGetValue(IdentityKey, out var cached) && cached is ApiIdentity identity)
        {
            return identity;
        }

        identity = Parse(context);
        context.Items[IdentityKey] = identity;
        return identity;
    }

    /// <summary>
    /// Prüft, ob der Aufrufer ein **gültiges** Zugangsmittel vorlegt.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <returns>
    /// <c>null</c>, wenn alles in Ordnung ist - sonst die Fehlerantwort
    /// (<c>401</c>).
    /// </returns>
    /// <remarks>
    /// <para>
    /// Diese Prüfung läuft für **jede** Anfrage unter <c>/api</c> - auch für
    /// die offenen Endpunkte (Status, Items, Diagnose).
    /// </para>
    /// <para>
    /// <b>Warum?</b> „Offen" heißt: es ist <i>kein</i> Zugangsmittel
    /// nötig. Wer **aber** eines vorlegt und es ist falsch, muss eine
    /// Rückmeldung bekommen - sonst hält die Home-Assistant-Integration einen
    /// falschen Token für gültig und meldet erst Monate später, dass nichts
    /// mehr geht. Das entspricht der HTTP-Semantik: eine ungültige Identität
    /// wird abgelehnt, auch wenn die Ressource ohnehin offen ist.
    /// </para>
    /// <para>
    /// Ohne <c>Authorization</c>-Header (also im LAN-Modus oder mit dem
    /// Sitzungs-Cookie der Oberfläche) wird nichts beanstandet.
    /// </para>
    /// </remarks>
    public IResult? RequireValidCredential(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var header = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            // Kein Zugangsmittel vorgelegt - das ist im LAN-Modus erlaubt.
            return null;
        }

        var identity = GetIdentity(context);

        return identity.IsAuthenticated
            ? null
            : Unauthorized(context, identity);
    }

    /// <summary>
    /// Prüft, ob der Aufrufer administrative Rechte hat.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <returns>
    /// <c>null</c>, wenn der Zugriff erlaubt ist - sonst die Fehlerantwort.
    /// </returns>
    public IResult? RequireAdmin(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var identity = GetIdentity(context);

        if (identity.IsAdmin)
        {
            return null;
        }

        // Vor der Ersteinrichtung gibt es noch keinen Administrator - die
        // Ersteinrichtung muss den ersten Token erzeugen dürfen.
        if (!_store.SetupCompleted)
        {
            return null;
        }

        // LAN-Modus: der Gateway vertraut dem Netz (dokumentierter Betrieb).
        if (!_options.RequireAuthentication)
        {
            return null;
        }

        return Unauthorized(context, identity);
    }

    /// <summary>
    /// Prüft, ob der Aufrufer auf die Setup-Endpunkte zugreifen darf.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <returns>
    /// <c>null</c>, wenn der Zugriff erlaubt ist - sonst die Fehlerantwort.
    /// </returns>
    /// <remarks>
    /// Nach der Ersteinrichtung sind die Setup-Endpunkte **immer**
    /// geschützt: sie können die Konfiguration (und damit auch das
    /// Administrator-Passwort) überschreiben.
    /// </remarks>
    public IResult? RequireSetupAccess(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!_store.SetupCompleted)
        {
            return null;
        }

        var identity = GetIdentity(context);
        return identity.IsAdmin ? null : Unauthorized(context, identity);
    }

    /// <summary>
    /// Ermittelt die Identität aus dem Authorization-Header oder dem Sitzungs-Cookie.
    /// </summary>
    /// <remarks>
    /// Drei Identitäten sind möglich:
    /// <list type="bullet">
    /// <item><c>Bearer &lt;token&gt;</c> - ein API-Token aus <c>config.json</c>;</item>
    /// <item><c>Basic &lt;base64&gt;</c> - der Administrator (Benutzername/Passwort);</item>
    /// <item>das Sitzungs-Cookie - die Web-Oberfläche (→ ADR-012).</item>
    /// </list>
    /// </remarks>
    private ApiIdentity Parse(HttpContext context)
    {
        var header = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(header))
        {
            return ParseSession(context);
        }

        if (header.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            var token = header["Bearer ".Length..].Trim();
            var apiToken = _store.VerifyToken(token);

            return apiToken is null
                ? ApiIdentity.Anonymous
                : new ApiIdentity(true, apiToken.Name, apiToken.IsAdmin, apiToken.Id);
        }

        if (header.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase))
        {
            return ParseBasic(header["Basic ".Length..].Trim());
        }

        return ApiIdentity.Anonymous;
    }

    /// <summary>
    /// Ermittelt die Identität aus dem Sitzungs-Cookie.
    /// </summary>
    /// <param name="context">Der HTTP-Kontext.</param>
    /// <returns>Die Identität.</returns>
    private ApiIdentity ParseSession(HttpContext context)
    {
        var token = context.Request.Cookies["cbk_session"];
        var userName = _sessions.Validate(token);

        return userName is null
            ? ApiIdentity.Anonymous
            : new ApiIdentity(true, userName, true, null);
    }

    /// <summary>
    /// Prüft die Basic-Authentifizierung (Administrator).
    /// </summary>
    private ApiIdentity ParseBasic(string encoded)
    {
        string decoded;

        try
        {
            decoded = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
        }
        catch (FormatException)
        {
            return ApiIdentity.Anonymous;
        }

        var separator = decoded.IndexOf(':', StringComparison.Ordinal);
        if (separator < 0)
        {
            return ApiIdentity.Anonymous;
        }

        var user = decoded[..separator];
        var password = decoded[(separator + 1)..];

        return _store.VerifyAdmin(user, password)
            ? new ApiIdentity(true, user, true, null)
            : ApiIdentity.Anonymous;
    }

    /// <summary>
    /// Erzeugt die Fehlerantwort für einen fehlgeschlagenen Zugriff.
    /// </summary>
    private static IResult Unauthorized(HttpContext context, ApiIdentity identity)
    {
        if (identity.IsAuthenticated)
        {
            return Results.Json(
                new ApiError
                {
                    Error = "forbidden",
                    Message = "Für diese Aktion sind Administratorrechte erforderlich.",
                },
                statusCode: StatusCodes.Status403Forbidden);
        }

        context.Response.Headers.WWWAuthenticate = "Bearer";
        return Results.Json(
            new ApiError
            {
                Error = "unauthorized",
                Message = "Die Anfrage ist nicht authentifiziert. Erwartet wird 'Authorization: Bearer <Token>'.",
            },
            statusCode: StatusCodes.Status401Unauthorized);
    }
}
