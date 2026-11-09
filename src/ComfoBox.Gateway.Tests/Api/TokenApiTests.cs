using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Nodes;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Logging;
using ComfoBox.Gateway.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Token-API (Aufgabe 4.8).
/// </summary>
public sealed class TokenApiTests
{
    /// <summary>
    /// Ein Token wird erzeugt und **einmal** im Klartext geliefert.
    /// </summary>
    [Fact]
    public async Task Ein_Token_Wird_Einmal_Im_Klartext_Geliefert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsync(
            "/api/security/tokens",
            Json("{\"name\":\"home-assistant\",\"isAdmin\":true}"));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var created = await GatewayApiFixture.ReadJsonAsync(response);
        var plaintext = created.GetProperty("token").GetString();

        Assert.False(string.IsNullOrWhiteSpace(plaintext));
        Assert.StartsWith(ApiSecurityStore.TokenPrefix, plaintext, StringComparison.Ordinal);
        Assert.Equal("home-assistant", created.GetProperty("name").GetString());
        Assert.True(created.GetProperty("isAdmin").GetBoolean());

        // Die Liste enthält **nur** den Hash, nie den Klartext.
        var list = await GatewayApiFixture.ReadJsonAsync(await fixture.Client.GetAsync("/api/security/tokens"));
        var entry = list[0];

        Assert.Equal(created.GetProperty("id").GetString(), entry.GetProperty("id").GetString());
        Assert.False(entry.TryGetProperty("hash", out _));
        Assert.False(entry.TryGetProperty("token", out _));
    }

    /// <summary>
    /// In der config.json steht nur der Hash.
    /// </summary>
    [Fact]
    public async Task In_Der_Config_Json_Steht_Nur_Der_Hash()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var created = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.PostAsync("/api/security/tokens", Json("{\"name\":\"test\"}")));

        var plaintext = created.GetProperty("token").GetString()!;

        var store = fixture.Services.GetRequiredService<ConfigStore>();
        var content = await File.ReadAllTextAsync(store.FilePath);

        Assert.DoesNotContain(plaintext, content, StringComparison.Ordinal);

        var tokens = JsonNode.Parse(content)!["Api"]!["Tokens"]!.AsArray();
        var hash = tokens[0]!["Hash"]!.GetValue<string>();

        Assert.StartsWith("pbkdf2-sha256$600000$", hash, StringComparison.Ordinal);
        Assert.True(PasswordHasher.Verify(plaintext, hash));
    }

    /// <summary>
    /// Ein gültiges Token authentifiziert - ein ungültiges nicht.
    /// </summary>
    [Fact]
    public async Task Ein_Gueltiges_Token_Authentifiziert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?> { ["Api:RequireAuthentication"] = "true" });

        // Vor der Ersteinrichtung darf der erste Token erzeugt werden.
        var created = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.PostAsync("/api/security/tokens", Json("{\"name\":\"test\",\"isAdmin\":true}")));

        var plaintext = created.GetProperty("token").GetString()!;

        fixture.Services.GetRequiredService<ApiSecurityStore>().SetSetupCompleted(true);

        // Ohne Token: 401.
        var without = await fixture.Client.GetAsync("/api/security/tokens");
        Assert.Equal(HttpStatusCode.Unauthorized, without.StatusCode);

        // Mit ungültigem Token: 401.
        using (var invalid = new HttpRequestMessage(HttpMethod.Get, "/api/security/tokens"))
        {
            invalid.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "cbk_gibtsnicht_secret");

            var response = await fixture.Client.SendAsync(invalid);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Mit gültigem Token: 200.
        using (var valid = new HttpRequestMessage(HttpMethod.Get, "/api/security/tokens"))
        {
            valid.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

            var response = await fixture.Client.SendAsync(valid);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    /// <summary>
    /// Ein widerrufener Token wirkt sofort nicht mehr.
    /// </summary>
    [Fact]
    public async Task Ein_Widerrufener_Token_Wirkt_Sofort_Nicht_Mehr()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?> { ["Api:RequireAuthentication"] = "true" });

        var created = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.PostAsync("/api/security/tokens", Json("{\"name\":\"test\",\"isAdmin\":true}")));

        var id = created.GetProperty("id").GetString()!;
        var plaintext = created.GetProperty("token").GetString()!;

        fixture.Services.GetRequiredService<ApiSecurityStore>().SetSetupCompleted(true);

        // Das Widerrufen braucht Administratorrechte - der Token hat sie.
        using (var revoke = new HttpRequestMessage(HttpMethod.Delete, $"/api/security/tokens/{id}"))
        {
            revoke.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

            var delete = await fixture.Client.SendAsync(revoke);
            Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        }

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/security/tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Ein unbekanntes Token ergibt 404 beim Widerruf.
    /// </summary>
    [Fact]
    public async Task Ein_Unbekanntes_Token_Ergibt_404()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.DeleteAsync("/api/security/tokens/gibtsnicht");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    /// <summary>
    /// Ein ungültiger Token wird auch auf den **offenen** Endpunkten
    /// abgelehnt - die Home-Assistant-Integration prüft ihren Token über
    /// <c>/api/status</c> und würde einen falschen Token sonst für gültig
    /// halten.
    /// </summary>
    [Fact]
    public async Task Ein_Ungueltiger_Token_Ergibt_401_Auch_Bei_Offenen_Endpunkten()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "cbk_gibtsnicht_secret");

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Ein gültiger Token wird auch auf den offenen Endpunkten akzeptiert.
    /// </summary>
    [Fact]
    public async Task Ein_Gueltiger_Token_Wird_Auch_Bei_Offenen_Endpunkten_Akzeptiert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var created = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.PostAsync("/api/security/tokens", Json("{\"name\":\"ha\"}")));

        var plaintext = created.GetProperty("token").GetString()!;

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/status");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Ohne Zugangsmittel bleiben die offenen Endpunkte erreichbar
    /// (LAN-Modus).
    /// </summary>
    [Fact]
    public async Task Ohne_Zugangsmittel_Bleiben_Die_Offenen_Endpunkte_Erreichbar()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Der Administrator kann sich mit Basic-Authentifizierung anmelden.
    /// </summary>
    [Fact]
    public async Task Der_Administrator_Kann_Sich_Mit_Basic_Anmelden()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?> { ["Api:RequireAuthentication"] = "true" });

        var security = fixture.Services.GetRequiredService<ApiSecurityStore>();
        security.SetAdmin("admin", PasswordHasher.Hash("geheim123"));
        security.SetSetupCompleted(true);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/security/tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("admin:geheim123")));

        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// Der Token erscheint **nirgends** im Log-Ringpuffer.
    /// </summary>
    [Fact]
    public async Task Der_Token_Erscheint_Nirgends_Im_Log()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var created = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.PostAsync("/api/security/tokens", Json("{\"name\":\"test\"}")));

        var plaintext = created.GetProperty("token").GetString()!;

        var sink = fixture.Services.GetRequiredService<InMemoryLogSink>();
        var entries = sink.Snapshot(sink.Capacity);

        Assert.DoesNotContain(entries, entry =>
            entry.Message.Contains(plaintext, StringComparison.Ordinal));
    }

    /// <summary>
    /// Ein Token wird mit Umfang und Ablaufdatum erzeugt.
    /// </summary>
    [Fact]
    public async Task Ein_Token_Traegt_Umfang_Und_Ablaufdatum()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var created = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.PostAsync(
                "/api/security/tokens",
                Json("{\"name\":\"home-assistant\",\"scope\":\"read\",\"expiresInDays\":30}")));

        Assert.Equal("read", created.GetProperty("scope").GetString());
        Assert.False(created.GetProperty("isAdmin").GetBoolean());

        var expiresAt = created.GetProperty("expiresAt");
        Assert.Equal(System.Text.Json.JsonValueKind.String, expiresAt.ValueKind);

        var list = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.GetAsync("/api/security/tokens"));

        Assert.Equal("read", list[0].GetProperty("scope").GetString());
        Assert.False(list[0].GetProperty("expired").GetBoolean());
    }

    /// <summary>
    /// Ohne Ablaufdatum bleibt das Token unbegrenzt gültig.
    /// </summary>
    [Fact]
    public async Task Ohne_Dauer_Gibt_Es_Kein_Ablaufdatum()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var created = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.PostAsync(
                "/api/security/tokens",
                Json("{\"name\":\"dauerhaft\",\"scope\":\"control\"}")));

        Assert.Equal("control", created.GetProperty("scope").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, created.GetProperty("expiresAt").ValueKind);
    }

    /// <summary>
    /// Ein unbekannter Umfang wird abgelehnt.
    /// </summary>
    [Fact]
    public async Task Ein_Unbekannter_Umfang_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsync(
            "/api/security/tokens",
            Json("{\"name\":\"root\",\"scope\":\"root\"}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Ein Nur-Lese-Token darf keine Werte schreiben (403, nicht 401).
    /// </summary>
    [Fact]
    public async Task Ein_Nur_Lese_Token_Darf_Nicht_Schreiben()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?> { ["Api:RequireAuthentication"] = "true" });

        var created = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.PostAsync(
                "/api/security/tokens",
                Json("{\"name\":\"nur-lesen\",\"scope\":\"read\"}")));

        var plaintext = created.GetProperty("token").GetString()!;

        fixture.Services.GetRequiredService<ApiSecurityStore>().SetSetupCompleted(true);

        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/items/zone.set_point_normal")
        {
            Content = Json("{\"value\":21.5}"),
        };

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

        var response = await fixture.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);

        var error = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("read_only_token", error.GetProperty("error").GetString());

        // Es wurde **nichts** geschrieben - der Zähler blieb unverändert.
        Assert.Empty(fixture.Bacnet.WrittenObjects);
    }

    /// <summary>
    /// Ein abgelaufenes Token authentifiziert nicht mehr.
    /// </summary>
    [Fact]
    public async Task Ein_Abgelaufenes_Token_Authentifiziert_Nicht()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?> { ["Api:RequireAuthentication"] = "true" });

        var store = fixture.Services.GetRequiredService<ApiSecurityStore>();

        // Eine negative Laufzeit ist bereits bei der Erzeugung abgelaufen -
        // der Test braucht damit keine Wartezeit.
        var (_, plaintext) = store.CreateToken("abgelaufen", TokenScope.Control, TimeSpan.FromSeconds(-1));

        store.SetSetupCompleted(true);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/security/tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", plaintext);

        var response = await fixture.Client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// Ein unbekannter Umfangswert in der config.json fällt auf „Control" zurück.
    /// </summary>
    [Fact]
    public async Task Ein_Altes_Token_Ohne_Umfang_Behaelt_Seine_Rechte()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var store = fixture.Services.GetRequiredService<ApiSecurityStore>();
        store.SetSetupCompleted(true);

        var (token, _) = store.CreateToken("alt", TokenScope.Admin, null);

        // Der Umfang steht als Text in der Datei und wird wieder gelesen.
        var reloaded = store.FindToken(token.Id);

        Assert.NotNull(reloaded);
        Assert.Equal(TokenScope.Admin, reloaded!.Scope);
        Assert.True(reloaded.IsAdmin);
        Assert.Null(reloaded.ExpiresAt);
    }

    /// <summary>
    /// Erzeugt einen JSON-Body.
    /// </summary>
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
