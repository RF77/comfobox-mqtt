using System.Net;
using System.Net.Http.Json;
using ComfoBox.Gateway.Bacnet;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der BACnet-Bus-Analyse.
/// </summary>
/// <remarks>
/// Die Analyse ist **lesend**: sie stellt keine Anfragen an die ComfoBox.
/// Geprüft wird, dass die Endpunkte das erwartete Verhalten liefern - und
/// dass das Polling für die Dauer einer Beobachtung wirklich pausiert.
/// </remarks>
public sealed class BacnetDiagnosticsApiTests
{
    /// <summary>
    /// Die Auswertung ist ohne Beobachtung abrufbar.
    /// </summary>
    [Fact]
    public async Task Die_Auswertung_Ist_Ohne_Beobachtung_Abrufbar()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync("/api/diagnostics/bacnet");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var analysis = await GatewayApiFixture.ReadJsonAsync(response);

        Assert.False(analysis.GetProperty("observing").GetBoolean());
        Assert.Equal(0, analysis.GetProperty("totalEvents").GetInt64());
        Assert.True(analysis.TryGetProperty("verdict", out _));
        Assert.True(analysis.TryGetProperty("counters", out _));
        Assert.Equal("COM_TEST", analysis.GetProperty("port").GetString());
        Assert.Equal(38400, analysis.GetProperty("baudrate").GetInt32());
    }

    /// <summary>
    /// Eine Beobachtung startet, pausiert das Polling und wird beendet.
    /// </summary>
    [Fact]
    public async Task Eine_Beobachtung_Startet_Und_Pausiert_Das_Polling()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var polling = fixture.Services.GetRequiredService<BacnetPollingService>();
        Assert.False(polling.IsPaused);

        var start = await fixture.Client.PostAsJsonAsync(
            "/api/diagnostics/bacnet/observe",
            new { durationSeconds = 5, pausePolling = true });

        Assert.Equal(HttpStatusCode.OK, start.StatusCode);

        var started = await GatewayApiFixture.ReadJsonAsync(start);

        Assert.True(started.GetProperty("observing").GetBoolean());
        Assert.True(started.GetProperty("pollingPaused").GetBoolean());
        Assert.True(polling.IsPaused, "Das Polling muss während der Beobachtung pausiert sein.");

        var stop = await fixture.Client.DeleteAsync("/api/diagnostics/bacnet/observe");

        Assert.Equal(HttpStatusCode.OK, stop.StatusCode);

        var stopped = await GatewayApiFixture.ReadJsonAsync(stop);

        Assert.False(stopped.GetProperty("observing").GetBoolean());
        Assert.False(stopped.GetProperty("pollingPaused").GetBoolean());
        Assert.False(polling.IsPaused, "Nach dem Beenden muss das Polling weiterlaufen.");
    }

    /// <summary>
    /// Ohne Pause läuft das Polling weiter.
    /// </summary>
    [Fact]
    public async Task Ohne_Pause_Laeuft_Das_Polling_Weiter()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var polling = fixture.Services.GetRequiredService<BacnetPollingService>();

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/diagnostics/bacnet/observe",
            new { durationSeconds = 5, pausePolling = false });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(polling.IsPaused);
    }

    /// <summary>
    /// Eine Dauer außerhalb der Grenzen wird abgelehnt.
    /// </summary>
    [Theory]
    [InlineData(1)]
    [InlineData(1000)]
    public async Task Eine_Unzulaessige_Dauer_Wird_Abgelehnt(int duration)
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsJsonAsync(
            "/api/diagnostics/bacnet/observe",
            new { durationSeconds = duration, pausePolling = true });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Die Ereignisse des Ringpuffers erscheinen in der Auswertung.
    /// </summary>
    [Fact]
    public async Task Die_Ereignisse_Erscheinen_In_Der_Auswertung()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        // Der Ringpuffer wird direkt gefüllt - im Test gibt es keinen Bus.
        var recorder = fixture.Services.GetRequiredService<ComfoBox.Bacnet.MstpEventRecorder>();

        recorder.OnMstpEvent(ComfoBox.Bacnet.MstpEventKind.TokenReceived, 4, 0);
        recorder.OnMstpEvent(ComfoBox.Bacnet.MstpEventKind.TokenPassed, 5, 0);

        var response = await fixture.Client.GetAsync("/api/diagnostics/bacnet");
        var analysis = await GatewayApiFixture.ReadJsonAsync(response);

        Assert.Equal(2, analysis.GetProperty("totalEvents").GetInt64());

        var events = analysis.GetProperty("events");
        Assert.Equal(2, events.GetArrayLength());
        Assert.Equal("TokenReceived", events[0].GetProperty("kind").GetString());
        Assert.Equal(4, events[0].GetProperty("station").GetInt32());
        Assert.Equal(5, events[1].GetProperty("station").GetInt32());

        var counters = analysis.GetProperty("counters");
        Assert.Equal(1, counters.GetProperty("TokenReceived").GetInt64());
        Assert.Equal(1, counters.GetProperty("TokenPassed").GetInt64());
    }
}
