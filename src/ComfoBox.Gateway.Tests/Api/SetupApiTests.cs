using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Security;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Setup-API (Aufgabe 4.7).
/// </summary>
public sealed class SetupApiTests
{
    /// <summary>
    /// Vor der Ersteinrichtung ist das Setup offen.
    /// </summary>
    [Fact]
    public async Task Vor_Der_Ersteinrichtung_Ist_Das_Setup_Offen()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/setup/state");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var state = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.False(state.GetProperty("setupCompleted").GetBoolean());
        Assert.Equal(1, state.GetProperty("version").GetInt32());
        Assert.True(state.GetProperty("steps").GetArrayLength() > 0);
    }

    /// <summary>
    /// Die Port-Liste ist ein Array.
    /// </summary>
    [Fact]
    public async Task Die_Port_Liste_Ist_Ein_Array()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/setup/ports");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var ports = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, ports.ValueKind);
    }

    /// <summary>
    /// Es wird ein freier Web-Port vorgeschlagen.
    /// </summary>
    [Fact]
    public async Task Es_Wird_Ein_Freier_Web_Port_Vorgeschlagen()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/setup/ports/suggest");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var port = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.True(port.GetInt32() >= 8081);
    }

    /// <summary>
    /// Das Passwort wird **serverseitig** gehasht - im Klartext steht es
    /// nirgends in der config.json.
    /// </summary>
    [Fact]
    public async Task Das_Passwort_Wird_Serverseitig_Gehasht()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/setup/config",
            Json("{\"port\":\"/dev/ttyUSB0\",\"baudrate\":38400,\"brokerHost\":\"broker\",\"adminUsername\":\"admin\",\"adminPassword\":\"geheim123\"}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var store = fixture.Services.GetRequiredService<ConfigStore>();
        var content = await File.ReadAllTextAsync(store.FilePath);

        Assert.DoesNotContain("geheim123", content, StringComparison.Ordinal);

        var api = JsonNode.Parse(content)!["Api"]!.AsObject();
        var hash = api["AdminPasswordHash"]!.GetValue<string>();

        Assert.StartsWith("pbkdf2-sha256$600000$", hash, StringComparison.Ordinal);
        Assert.True(PasswordHasher.Verify("geheim123", hash));
        Assert.False(PasswordHasher.Verify("falsch", hash));

        // Die übrigen Angaben stehen in der Datei.
        Assert.Equal("/dev/ttyUSB0", JsonNode.Parse(content)!["Bacnet"]!["Port"]!.GetValue<string>());
    }

    /// <summary>
    /// Eine unvollständige Konfiguration wird abgelehnt.
    /// </summary>
    [Fact]
    public async Task Eine_Unvollstaendige_Konfiguration_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/setup/config",
            Json("{\"baudrate\":38400}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Nach dem Anwenden ist die Ersteinrichtung abgeschlossen und der
    /// Neustart wurde angestoßen.
    /// </summary>
    [Fact]
    public async Task Nach_Dem_Anwenden_Ist_Das_Setup_Abgeschlossen()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        await fixture.Client.PutAsync(
            "/api/setup/config",
            Json("{\"port\":\"/dev/ttyUSB0\",\"adminUsername\":\"admin\",\"adminPassword\":\"geheim123\"}"));

        var apply = await fixture.Client.PostAsync("/api/setup/apply", null);
        Assert.Equal(HttpStatusCode.Accepted, apply.StatusCode);
        Assert.Equal(1, fixture.Restart.Requests);

        // Nach dem Anwenden sind die Setup-Endpunkte geschützt - der Zustand
        // steht deshalb im Speicher, nicht in einem HTTP-Aufruf.
        var security = fixture.Services.GetRequiredService<ApiSecurityStore>();
        Assert.True(security.SetupCompleted);
    }

    /// <summary>
    /// Nach der Ersteinrichtung sind die Setup-Endpunkte geschützt.
    /// </summary>
    [Fact]
    public async Task Nach_Der_Ersteinrichtung_Sind_Die_Setup_Endpunkte_Geschuetzt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        fixture.Services.GetRequiredService<ApiSecurityStore>().SetSetupCompleted(true);

        var response = await fixture.Client.GetAsync("/api/setup/state");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);

        var apply = await fixture.Client.PostAsync("/api/setup/apply", null);
        Assert.Equal(HttpStatusCode.Unauthorized, apply.StatusCode);
    }

    /// <summary>
    /// Ein BACnet-Test an einem nicht vorhandenen Port liefert einen Hinweis.
    /// </summary>
    [Fact]
    public async Task Ein_Bacnet_Test_Liefert_Einen_Hinweis()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsync(
            "/api/setup/bacnet/test",
            Json("{\"port\":\"/dev/gibtsnicht\",\"baudrate\":38400}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.Equal("serial_port_not_found", result.GetProperty("hint").GetString());
        Assert.True(result.TryGetProperty("durationMs", out _));
    }

    /// <summary>
    /// Ein MQTT-Test gegen einen nicht erreichbaren Broker liefert einen Hinweis.
    /// </summary>
    [Fact]
    public async Task Ein_Mqtt_Test_Liefert_Einen_Hinweis()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsync(
            "/api/setup/mqtt/test",
            Json("{\"host\":\"127.0.0.1\",\"port\":1}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(result.GetProperty("hint").GetString()));
    }

    /// <summary>
    /// Erzeugt einen JSON-Body.
    /// </summary>
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
