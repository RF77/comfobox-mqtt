using System.Net;
using System.Net.Http.Json;
using ComfoBox.Gateway.Configuration;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Anmeldung (Aufgabe 5.10 - Login &amp; Cookie-Auth).
/// </summary>
public sealed class AuthApiTests
{
    /// <summary>
    /// Ohne Ersteinrichtung ist niemand angemeldet - der Assistent läuft
    /// ohne Sitzung.
    /// </summary>
    [Fact]
    public async Task Ohne_Ersteinrichtung_Ist_Niemand_Angemeldet()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/auth/session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await GatewayApiFixture.ReadJsonAsync(response);

        Assert.False(body.GetProperty("authenticated").GetBoolean());
        Assert.False(body.GetProperty("setupCompleted").GetBoolean());
    }

    /// <summary>
    /// Die Anmeldung mit falschen Zugangsdaten wird abgelehnt.
    /// </summary>
    [Fact]
    public async Task Falsche_Zugangsdaten_Werden_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        // Der Administrator wird gesetzt (wie es der Assistent täte).
        fixture.SetAdmin("admin", "Passwort123!");

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "admin", password = "falsch" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var body = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("invalid_credentials", body.GetProperty("error").GetString());
    }

    /// <summary>
    /// Die Anmeldung mit richtigen Zugangsdaten setzt ein HttpOnly-Cookie.
    /// </summary>
    [Fact]
    public async Task Die_Anmeldung_Setzt_Ein_HttpOnly_Cookie()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        fixture.SetAdmin("admin", "Passwort123!");

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = "admin", password = "Passwort123!" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.True(body.GetProperty("authenticated").GetBoolean());
        Assert.Equal("admin", body.GetProperty("userName").GetString());

        // Das Cookie ist HttpOnly - das JavaScript kann es nicht lesen.
        var cookie = Assert.Single(
            response.Headers.GetValues("Set-Cookie"),
            c => c.StartsWith("cbk_session=", StringComparison.Ordinal));

        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", cookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Nach der Anmeldung erkennt der Sitzungs-Endpunkt den Benutzer.
    /// </summary>
    [Fact]
    public async Task Nach_Der_Anmeldung_Wird_Der_Benutzer_Erkannt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        fixture.SetAdmin("admin", "Passwort123!");

        // Der HttpClient der Test-Fixture behauptet keine Cookies - der
        // Cookie-Container wird deshalb von Hand geführt.
        var token = await fixture.LoginAsync("admin", "Passwort123!");

        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", $"cbk_session={token}");

        var response = await fixture.Client.SendAsync(request);
        var body = await GatewayApiFixture.ReadJsonAsync(response);

        Assert.True(body.GetProperty("authenticated").GetBoolean());
        Assert.Equal("admin", body.GetProperty("userName").GetString());
    }

    /// <summary>
    /// Das Abmelden beendet die Sitzung serverseitig.
    /// </summary>
    [Fact]
    public async Task Das_Abmelden_Beendet_Die_Sitzung()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        fixture.SetAdmin("admin", "Passwort123!");

        var token = await fixture.LoginAsync("admin", "Passwort123!");

        var logout = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        logout.Headers.Add("Cookie", $"cbk_session={token}");

        var logoutResponse = await fixture.Client.SendAsync(logout);
        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);

        // Die Sitzung ist ungültig - auch mit dem alten Cookie.
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/session");
        request.Headers.Add("Cookie", $"cbk_session={token}");

        var response = await fixture.Client.SendAsync(request);
        var body = await GatewayApiFixture.ReadJsonAsync(response);

        Assert.False(body.GetProperty("authenticated").GetBoolean());
    }

    /// <summary>
    /// Ein unbekannter API-Pfad liefert JSON, **kein** HTML.
    /// </summary>
    /// <remarks>
    /// Der SPA-Fallback darf die Schnittstelle nicht verschlucken - sonst
    /// interpretiert ein Client die index.html als „erfolgreiche Antwort".
    /// </remarks>
    [Fact]
    public async Task Ein_Unbekannter_Api_Pfad_Liefert_Json()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/gibt-es-nicht");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);

        var body = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("not_found", body.GetProperty("error").GetString());
    }

    /// <summary>
    /// Der Reconnect der BACnet-Schnittstelle wird quittiert.
    /// </summary>
    [Fact]
    public async Task Der_Bacnet_Reconnect_Wird_Mit_202_Quittiert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsync("/api/service/reconnect/bacnet", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("reconnecting", body.GetProperty("status").GetString());
    }

    /// <summary>
    /// Der Reconnect der MQTT-Broker wird quittiert.
    /// </summary>
    [Fact]
    public async Task Der_Mqtt_Reconnect_Wird_Mit_202_Quittiert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsync("/api/service/reconnect/mqtt", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var body = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("reconnecting", body.GetProperty("status").GetString());
    }
}
