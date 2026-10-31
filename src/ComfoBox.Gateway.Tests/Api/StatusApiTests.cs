using System.Net;
using ComfoBox.Gateway.Health;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Status-Endpunkte (Aufgabe 4.2).
/// </summary>
public sealed class StatusApiTests
{
    /// <summary>
    /// Der Health-Endpunkt antwortet mit 200 und dem Zustand.
    /// </summary>
    [Fact]
    public async Task Der_Health_Endpunkt_Antwortet_Mit_200()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var health = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("online", health.GetProperty("state").GetString());
        Assert.True(health.TryGetProperty("message", out _));
        Assert.True(health.TryGetProperty("checks", out _));
        Assert.True(health.TryGetProperty("version", out _));
        Assert.True(health.TryGetProperty("uptimeSeconds", out _));
    }

    /// <summary>
    /// Ein degradierter Zustand antwortet **weiterhin** mit 200.
    /// </summary>
    [Fact]
    public async Task Ein_Degradierter_Zustand_Antwortet_Mit_200()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var health = fixture.Services.GetRequiredService<IHealthReporter>();
        health.Report("Bacnet", HealthState.Degraded, "Der Port ist zu.");

        var response = await fixture.Client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var report = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("degraded", report.GetProperty("state").GetString());
    }

    /// <summary>
    /// Die Komponenten erscheinen in den Checks.
    /// </summary>
    [Fact]
    public async Task Die_Komponenten_Erscheinen_In_Den_Checks()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var health = fixture.Services.GetRequiredService<IHealthReporter>();
        health.Report("Mqtt", HealthState.Online, "Verbunden.");

        var report = await GatewayApiFixture.ReadJsonAsync(await fixture.Client.GetAsync("/api/health"));

        var checks = report.GetProperty("checks").EnumerateArray().ToArray();
        Assert.Contains(checks, check => check.GetProperty("name").GetString() == "Mqtt");
        Assert.Contains(checks, check => check.GetProperty("state").GetString() == "online");
    }

    /// <summary>
    /// Der Status hat exakt die vorgegebenen Feldnamen.
    /// </summary>
    [Fact]
    public async Task Der_Status_Hat_Die_Vorgegebenen_Feldnamen()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var status = await GatewayApiFixture.ReadJsonAsync(response);

        Assert.Equal("comfobox1", status.GetProperty("deviceId").GetString());
        Assert.True(status.TryGetProperty("version", out _));
        Assert.True(status.TryGetProperty("uptimeSeconds", out _));

        var bacnet = status.GetProperty("bacnet");
        Assert.True(bacnet.TryGetProperty("connected", out _));
        Assert.Equal("COM_TEST", bacnet.GetProperty("port").GetString());
        Assert.True(bacnet.TryGetProperty("lastError", out _));

        var mqtt = status.GetProperty("mqtt");
        Assert.True(mqtt.TryGetProperty("connected", out _));
        Assert.Equal("127.0.0.1", mqtt.GetProperty("broker").GetString());

        var writes = status.GetProperty("writes");
        Assert.Equal(0, writes.GetProperty("count").GetInt32());
        Assert.Equal(200, writes.GetProperty("limit").GetInt32());
        Assert.True(writes.TryGetProperty("resetsAt", out _));
    }

    /// <summary>
    /// Die Fehlerhistorie ist ein Ringpuffer.
    /// </summary>
    [Fact]
    public async Task Die_Fehlerhistorie_Wird_Als_Array_Geliefert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var errors = fixture.Services.GetRequiredService<IErrorCollector>();
        errors.Report("Bacnet", "timeout", "Lesefehler", null, "warning");

        var response = await fixture.Client.GetAsync("/api/diagnostics/errors");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal(1, entries.GetArrayLength());
        Assert.Equal("timeout", entries[0].GetProperty("code").GetString());
        Assert.Equal("Bacnet", entries[0].GetProperty("source").GetString());
    }

    /// <summary>
    /// Der Schreibzähler wird ausgewiesen.
    /// </summary>
    [Fact]
    public async Task Der_Schreibzaehler_Wird_Ausgewiesen()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/diagnostics/writes");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var stats = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal(0, stats.GetProperty("count").GetInt32());
        Assert.Equal(200, stats.GetProperty("limit").GetInt32());
    }
}
