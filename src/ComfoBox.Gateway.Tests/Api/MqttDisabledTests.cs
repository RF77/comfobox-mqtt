using System.Net;
using System.Text;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Der Betrieb **ohne** MQTT (Home Assistant über REST/SSE).
/// </summary>
/// <remarks>
/// <c>Mqtt:Enabled=false</c> ist der sichere Betrieb: kein Broker, keine
/// MQTT-Schreib-Topics, keine Discovery. Die REST-API muss unverändert
/// antworten - die HA-Integration ist der einzige Weg zu den Werten.
/// </remarks>
public sealed class MqttDisabledTests
{
    /// <summary>
    /// Der Status meldet MQTT als deaktiviert - nicht als „getrennt".
    /// </summary>
    [Fact]
    public async Task Der_Status_Meldet_Mqtt_Deaktiviert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Mqtt:Enabled"] = "false",
            });

        var response = await fixture.Client.GetAsync("/api/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await GatewayApiFixture.ReadJsonAsync(response);
        var mqtt = status.GetProperty("mqtt");

        Assert.False(mqtt.GetProperty("enabled").GetBoolean());
        Assert.False(mqtt.GetProperty("connected").GetBoolean());
        Assert.Equal(string.Empty, mqtt.GetProperty("broker").GetString());
    }

    /// <summary>
    /// Der Health-Bericht nennt MQTT als bewusst abgeschaltet.
    /// </summary>
    [Fact]
    public async Task Der_Health_Bericht_Nennt_Mqtt_Deaktiviert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Mqtt:Enabled"] = "false",
            });

        var response = await fixture.Client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await GatewayApiFixture.ReadJsonAsync(response);

        var mqtt = health.GetProperty("checks")
            .EnumerateArray()
            .Single(check => check.GetProperty("name").GetString() == "Mqtt");

        Assert.Equal("online", mqtt.GetProperty("state").GetString());
        Assert.Contains("deaktiviert", mqtt.GetProperty("message").GetString(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein Reconnect ist ohne MQTT kein gültiger Wunsch - er wird klar
    /// abgelehnt (409) statt still nichts zu tun.
    /// </summary>
    [Fact]
    public async Task Ein_Mqtt_Reconnect_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?>
            {
                ["Mqtt:Enabled"] = "false",
            });

        var response = await fixture.Client.PostAsync("/api/service/reconnect/mqtt", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        var body = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("mqtt_disabled", body.GetProperty("error").GetString());
    }

    /// <summary>
    /// Der Schalter wird gespeichert und beim nächsten Lesen zurückgemeldet.
    /// </summary>
    [Fact]
    public async Task Der_Schalter_Wird_Gespeichert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/config",
            Json("{\"mqttEnabled\":false}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        var read = await fixture.Client.GetAsync("/api/config");
        var config = await GatewayApiFixture.ReadJsonAsync(read);

        Assert.False(config.GetProperty("mqtt").GetProperty("enabled").GetBoolean());
    }

    /// <summary>
    /// Erzeugt einen JSON-Body für die Anfrage.
    /// </summary>
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
