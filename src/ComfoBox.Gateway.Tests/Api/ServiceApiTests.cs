using System.Net;
using System.Text;
using ComfoBox.Gateway.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Betriebssteuerung (Aufgabe 4.5).
/// </summary>
public sealed class ServiceApiTests
{
    /// <summary>
    /// Der Neustart wird mit 202 quittiert.
    /// </summary>
    [Fact]
    public async Task Der_Neustart_Wird_Mit_202_Quittiert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.PostAsync("/api/service/restart", null);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(1, fixture.Restart.Requests);

        var body = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.Equal("restarting", body.GetProperty("status").GetString());
    }

    /// <summary>
    /// Ein deaktivierter Neustart wird abgelehnt (der Host bleibt laufen).
    /// </summary>
    [Fact]
    public async Task Ein_Deaktivierter_Neustart_Beendet_Den_Host_Nicht()
    {
        var lifetime = new FakeHostApplicationLifetime();

        var control = new ServiceControl(
            lifetime,
            Options.Create(new GatewayOptions { RestartEnabled = false }),
            Path.Combine(Path.GetTempPath(), "comfobox-restart-" + Guid.NewGuid().ToString("N")),
            NullLogger<ServiceControl>.Instance);

        await control.RequestRestartAsync(CancellationToken.None);

        Assert.False(lifetime.StopCalled);
    }

    /// <summary>
    /// Ein aktivierter Neustart beendet den Host.
    /// </summary>
    [Fact]
    public async Task Ein_Aktivierter_Neustart_Beendet_Den_Host()
    {
        var lifetime = new FakeHostApplicationLifetime();
        var directory = Path.Combine(Path.GetTempPath(), "comfobox-restart-" + Guid.NewGuid().ToString("N"));

        var control = new ServiceControl(
            lifetime,
            Options.Create(new GatewayOptions { RestartEnabled = true }),
            directory,
            NullLogger<ServiceControl>.Instance);

        await control.RequestRestartAsync(CancellationToken.None);

        Assert.True(lifetime.StopCalled);
        Assert.True(File.Exists(Path.Combine(directory, ServiceControl.RestartMarkerFileName)));

        Directory.Delete(directory, recursive: true);
    }

    /// <summary>
    /// Eine Attrapp-Lebensdauer.
    /// </summary>
    private sealed class FakeHostApplicationLifetime : IHostApplicationLifetime
    {
        /// <summary>
        /// Wurde der Stopp angefordert?
        /// </summary>
        public bool StopCalled { get; private set; }

        /// <inheritdoc />
        public CancellationToken ApplicationStarted => CancellationToken.None;

        /// <inheritdoc />
        public CancellationToken ApplicationStopping => CancellationToken.None;

        /// <inheritdoc />
        public CancellationToken ApplicationStopped => CancellationToken.None;

        /// <inheritdoc />
        public void StopApplication() => StopCalled = true;
    }
}
