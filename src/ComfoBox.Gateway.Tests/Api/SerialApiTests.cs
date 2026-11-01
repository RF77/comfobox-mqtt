using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Endpunkte für die serielle Schnittstelle.
/// </summary>
/// <remarks>
/// Der Kern der Aufgabe: ein Portwechsel muss **ohne Neustart** wirken. Der
/// Test prüft deshalb alle drei Schritte: die Konfiguration wird geschrieben,
/// die Laufzeit-Einstellung wird übernommen und der BACnet-Stack baut die
/// Verbindung neu auf.
/// </remarks>
public sealed class SerialApiTests
{
    /// <summary>
    /// Die Port-Liste wird geliefert.
    /// </summary>
    [Fact]
    public async Task Die_Port_Liste_Wird_Geliefert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/config/serial-ports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal(System.Text.Json.JsonValueKind.Array, body.ValueKind);
    }

    /// <summary>
    /// Ein Portwechsel wirkt sofort - ohne Neustart.
    /// </summary>
    [Fact]
    public async Task Ein_Portwechsel_Wirkt_Sofort_Ohne_Neustart()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/config/bacnet",
            Json("{\"port\":\"COM9\",\"baudrate\":38400}"));

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // 1. Die Konfiguration wurde geschrieben (überlebt einen Neustart).
        var store = fixture.Services.GetRequiredService<ConfigStore>();
        var content = JsonNode.Parse(await File.ReadAllTextAsync(store.FilePath))!.AsObject();
        var bacnet = content["Bacnet"]!.AsObject();

        Assert.Equal("COM9", bacnet["Port"]!.GetValue<string>());
        Assert.Equal(38400, bacnet["Baudrate"]!.GetValue<int>());

        // 2. Die Laufzeit-Einstellung wurde übernommen.
        var settings = fixture.Services.GetRequiredService<SerialPortSettings>();
        Assert.Equal("COM9", settings.PortName);
        Assert.Equal(38400, settings.BaudRate);

        // 3. Der BACnet-Stack baut die Verbindung neu auf - es gab keinen
        //    Neustart.
        Assert.Equal(1, fixture.Bacnet.Reconnects);
        Assert.Equal(0, fixture.Restart.Requests);
    }

    /// <summary>
    /// Der Status meldet den neuen Port.
    /// </summary>
    [Fact]
    public async Task Der_Status_Meldet_Den_Neuen_Port()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        await fixture.Client.PutAsync(
            "/api/config/bacnet",
            Json("{\"port\":\"COM9\",\"baudrate\":9600}"));

        var status = await GatewayApiFixture.ReadJsonAsync(
            await fixture.Client.GetAsync("/api/status"));

        Assert.Equal("COM9", status.GetProperty("bacnet").GetProperty("port").GetString());
    }

    /// <summary>
    /// Eine ungültige Baudrate wird abgelehnt.
    /// </summary>
    [Fact]
    public async Task Eine_Ungueltige_Baudrate_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/config/bacnet",
            Json("{\"port\":\"COM9\",\"baudrate\":12345}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.Bacnet.Reconnects);
    }

    /// <summary>
    /// Ein fehlender Port wird abgelehnt.
    /// </summary>
    [Fact]
    public async Task Ein_Fehlender_Port_Wird_Abgelehnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PutAsync(
            "/api/config/bacnet",
            Json("{\"port\":\"\",\"baudrate\":38400}"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(0, fixture.Bacnet.Reconnects);
    }

    /// <summary>
    /// Die Prüfung des aktiven Ports meldet den aktuellen Zustand.
    /// </summary>
    /// <remarks>
    /// Der aktive Port kann nicht geöffnet werden - der Gateway hält ihn
    /// selbst. Die Prüfung meldet deshalb den Zustand der Verbindung.
    /// </remarks>
    [Fact]
    public async Task Die_Pruefung_Meldet_Den_Zustand_Des_Aktiven_Ports()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsync(
            "/api/config/bacnet/test",
            Json("{\"port\":\"COM_TEST\",\"baudrate\":38400}"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await GatewayApiFixture.ReadJsonAsync(response);

        Assert.False(result.GetProperty("ok").GetBoolean());
        Assert.Equal("not_connected", result.GetProperty("hint").GetString());
    }

    /// <summary>
    /// Erzeugt einen JSON-Body.
    /// </summary>
    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");
}
