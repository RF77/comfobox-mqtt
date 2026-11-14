using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Konfigurations-Endpunkte (Aufgabe 4.4).
/// </summary>
public sealed class ConfigApiTests
{
    /// <summary>
    /// Die Konfiguration wird ohne Passwörter geliefert.
    /// </summary>
    [Fact]
    public async Task Die_Konfiguration_Wird_Ohne_Passwoerter_Geliefert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Mqtt:Brokers:0:Username"] = "comfobox",
                ["Mqtt:Brokers:0:Password"] = "geheim",
            });

        var response = await fixture.Client.GetAsync("/api/config");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("geheim", body, StringComparison.Ordinal);

        var config = await GatewayApiFixture.ReadJsonAsync(response);

        var broker = config.GetProperty("mqtt").GetProperty("brokers")[0];
        Assert.Equal("comfobox", broker.GetProperty("username").GetString());
        Assert.True(broker.GetProperty("passwordSet").GetBoolean());
        Assert.False(broker.TryGetProperty("password", out _));

        Assert.Equal("ComfoBox", config.GetProperty("gateway").GetProperty("baseTopic").GetString());
        Assert.True(config.TryGetProperty("bacnet", out _));
        Assert.True(config.TryGetProperty("logging", out _));
        Assert.True(config.TryGetProperty("api", out _));
        Assert.True(config.TryGetProperty("source", out _));
    }

    /// <summary>
    /// Eine Änderung wird in die config.json geschrieben.
    /// </summary>
    [Fact]
    public async Task Eine_Aenderung_Wird_In_Die_Config_Json_Geschrieben()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/config",
            Json("{\"expertMode\":true,\"pollIntervalSeconds\":30,\"maxWritesPer24h\":42}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var store = fixture.Services.GetRequiredService<ConfigStore>();
        Assert.True(store.Exists);

        var content = JsonNode.Parse(await File.ReadAllTextAsync(store.FilePath))!.AsObject();
        var gateway = content["Gateway"]!.AsObject();

        Assert.True(gateway["ExpertMode"]!.GetValue<bool>());
        Assert.Equal(30, gateway["PollIntervalSeconds"]!.GetValue<int>());
        Assert.Equal(42, gateway["MaxWritesPer24h"]!.GetValue<int>());
    }

    /// <summary>
    /// Ein ungültiger Wert wird abgelehnt.
    /// </summary>
    [Fact]
    public async Task Ein_Ungueltiger_Wert_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/config",
            Json("{\"pollIntervalSeconds\":0}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Ein geänderter Wert wird beim nächsten Lesen auch **zurückgemeldet**.
    /// </summary>
    /// <remarks>
    /// <b>Regression (2026-11-14):</b> Der Experten-Modus ließ sich in der
    /// Oberfläche nicht einschalten - er wurde gespeichert, aber
    /// <c>GET /api/config</c> antwortete weiter mit dem Stand vom Start
    /// (<c>IOptions&lt;T&gt;</c> wird nur einmal gebunden). Die Oberfläche lädt
    /// nach dem Speichern neu und zeigte wieder „aus".
    /// </remarks>
    [Fact]
    public async Task Ein_Gespeicherter_Wert_Wird_Zurueckgemeldet()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var put = await fixture.Client.PutAsync(
            "/api/config",
            Json("{\"expertMode\":true,\"pollIntervalSeconds\":30,\"maxWritesPer24h\":42,"
                 + "\"deviceId\":7,\"itemTimeoutMs\":5000,\"thisStation\":9,\"writePriority\":8}"));

        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var config = await GatewayApiFixture.ReadJsonAsync(await fixture.Client.GetAsync("/api/config"));

        var gateway = config.GetProperty("gateway");
        Assert.True(gateway.GetProperty("expertMode").GetBoolean());
        Assert.Equal(30, gateway.GetProperty("pollIntervalSeconds").GetInt32());
        Assert.Equal(42, gateway.GetProperty("maxWritesPer24h").GetInt32());

        var bacnet = config.GetProperty("bacnet");
        Assert.Equal(7, bacnet.GetProperty("deviceId").GetInt32());
        Assert.Equal(5000, bacnet.GetProperty("itemTimeoutMs").GetInt32());
        Assert.Equal(9, bacnet.GetProperty("thisStation").GetInt32());
        Assert.Equal(8, bacnet.GetProperty("writePriority").GetInt32());
    }

    /// <summary>
    /// Der Experten-Modus wirkt sofort - ohne Neustart.
    /// </summary>
    /// <remarks>
    /// Er entscheidet bei jedem Schreibvorgang. Wird er nur in der Datei
    /// gespeichert, lehnt der Gateway sensible Werte bis zum nächsten Start
    /// weiter ab.
    /// </remarks>
    [Fact]
    public async Task Der_Experten_Modus_Wirkt_Sofort()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var runtime = fixture.Services.GetRequiredService<GatewayRuntimeSettings>();
        Assert.False(runtime.ExpertMode);

        var put = await fixture.Client.PutAsync("/api/config", Json("{\"expertMode\":true}"));
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        Assert.True(runtime.ExpertMode);
    }

    /// <summary>
    /// Eine unsinnige Geräte-ID wird abgelehnt.
    /// </summary>
    [Theory]
    [InlineData("{\"deviceId\":300}")]
    [InlineData("{\"itemTimeoutMs\":5}")]
    [InlineData("{\"thisStation\":300}")]
    [InlineData("{\"writePriority\":20}")]
    public async Task Ein_Unsinniger_Bacnet_Wert_Wird_Abgelehnt(string body)
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync("/api/config", Json(body));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Ohne Authentifizierung (LAN-Modus) darf die Konfiguration geändert werden.
    /// </summary>
    [Fact]
    public async Task Im_Lan_Modus_Darf_Geaendert_Werden()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync("/api/config", Json("{\"expertMode\":true}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    /// <summary>
    /// Erzeugt einen JSON-Body.
    /// </summary>
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
