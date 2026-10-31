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
