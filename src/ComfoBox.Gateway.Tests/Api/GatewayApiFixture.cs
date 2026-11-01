using System.Net.Http.Json;
using System.Text.Json;
using ComfoBox.Bacnet;
using ComfoBox.Gateway;
using ComfoBox.Gateway.Api;
using ComfoBox.Gateway.Logging;
using ComfoBox.Gateway.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog.Events;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Der Neustart-Attrapp: der echte ServiceControl würde den Testhost beenden.
/// </summary>
internal sealed class FakeRestartService : IRestartService
{
    /// <summary>
    /// Wie oft wurde ein Neustart angefordert?
    /// </summary>
    public int Requests { get; private set; }

    /// <inheritdoc />
    public Task RequestRestartAsync(CancellationToken cancellationToken)
    {
        Requests++;
        return Task.CompletedTask;
    }
}

/// <summary>
/// Der Testhost für die REST/SSE-API.
/// </summary>
/// <remarks>
/// <para>
/// Der Host wird **echt** gestartet (Kestrel auf einem freien Port) - so
/// werden Routing, Serialisierung und Statuscodes wirklich getestet.
/// </para>
/// <para>
/// Die Hintergrunddienste (Polling, MQTT) werden entfernt: der Test darf
/// weder den seriellen Port öffnen noch sich mit einem Broker verbinden.
/// </para>
/// </remarks>
internal sealed class GatewayApiFixture : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly string _directory;

    private GatewayApiFixture(WebApplication app, HttpClient client, string directory, FakeBacnetClient bacnet, FakeRestartService restart)
    {
        _app = app;
        _directory = directory;

        Client = client;
        Bacnet = bacnet;
        Restart = restart;
    }

    /// <summary>
    /// Der HTTP-Client.
    /// </summary>
    public HttpClient Client { get; }

    /// <summary>
    /// Der BACnet-Attrapp.
    /// </summary>
    public FakeBacnetClient Bacnet { get; }

    /// <summary>
    /// Der Neustart-Attrapp.
    /// </summary>
    public FakeRestartService Restart { get; }

    /// <summary>
    /// Der Service-Container des Hosts.
    /// </summary>
    public IServiceProvider Services => _app.Services;

    /// <summary>
    /// Startet den Testhost.
    /// </summary>
    /// <param name="settings">Zusätzliche Konfigurationswerte.</param>
    /// <returns>Der gestartete Host.</returns>
    public static async Task<GatewayApiFixture> StartAsync(
        IReadOnlyDictionary<string, string?>? settings = null)
    {
        var directory = Path.Combine(
            Path.GetTempPath(),
            "comfobox-api-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(directory);

        var values = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            ["Gateway:BaseTopic"] = "ComfoBox",
            ["Gateway:DeviceId"] = "comfobox1",
            ["Gateway:DataDirectory"] = directory,
            ["Gateway:ExpertMode"] = "false",
            ["Gateway:PollIntervalSeconds"] = "3600",
            ["Bacnet:Port"] = "COM_TEST",
            ["Bacnet:Baudrate"] = "38400",
            ["Bacnet:DeviceId"] = "1",
            ["Mqtt:ClientId"] = "comfobox-api-test",
            ["Mqtt:KeepAliveSeconds"] = "15",
            ["Mqtt:Brokers:0:Host"] = "127.0.0.1",
            ["Mqtt:Brokers:0:Port"] = "1883",
            ["Logging:Directory"] = Path.Combine(directory, "logs"),
        };

        if (settings is not null)
        {
            foreach (var entry in settings)
            {
                values[entry.Key] = entry.Value;
            }
        }

        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ApplicationName = typeof(GatewayApiFixture).Assembly.GetName().Name,
            ContentRootPath = directory,
            EnvironmentName = Environments.Production,
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Configuration.AddInMemoryCollection(values);

        var logBuffer = new InMemoryLogSink(500, LogEventLevel.Verbose);

        builder.Services.AddGatewayServices(
            builder.Configuration,
            directory,
            logBuffer,
            startBackgroundServices: false);

        builder.Services.AddGatewayApi(builder.Configuration, directory);

        // Der BACnet-Client wird durch den Attrapp ersetzt.
        var bacnet = new FakeBacnetClient();
        builder.Services.AddSingleton<IBacnetClient>(bacnet);

        // Der Neustart darf den Testhost nicht beenden.
        var restart = new FakeRestartService();
        builder.Services.AddSingleton<IRestartService>(restart);

        var app = builder.Build();
        app.MapGatewayApi();

        await app.StartAsync();

        var address = app.Services
            .GetRequiredService<IServer>()
            .Features
            .Get<IServerAddressesFeature>()!
            .Addresses
            .First();

        var client = new HttpClient
        {
            BaseAddress = new Uri(address),
            Timeout = TimeSpan.FromSeconds(30),
        };

        return new GatewayApiFixture(app, client, directory, bacnet, restart);
    }

    /// <summary>
    /// Liest die Antwort als JSON.
    /// </summary>
    /// <param name="response">Die Antwort.</param>
    /// <returns>Das JSON-Dokument.</returns>
    public static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(text);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// Setzt den Administrator (so wie es der Assistent täte).
    /// </summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="password">Das Passwort im Klartext.</param>
    public void SetAdmin(string userName, string password)
    {
        var store = Services.GetRequiredService<ApiSecurityStore>();

        store.SetAdmin(userName, PasswordHasher.Hash(password));
        store.SetSetupCompleted(true);
    }

    /// <summary>
    /// Meldet sich an und liefert das Sitzungskennzeichen (den Cookie-Wert).
    /// </summary>
    /// <param name="userName">Der Benutzername.</param>
    /// <param name="password">Das Passwort im Klartext.</param>
    /// <returns>Der Klartext des Sitzungs-Cookies.</returns>
    public async Task<string> LoginAsync(string userName, string password)
    {
        var response = await Client.PostAsJsonAsync(
            "/api/auth/login",
            new { username = userName, password });

        response.EnsureSuccessStatusCode();

        var cookie = response.Headers.GetValues("Set-Cookie")
            .First(c => c.StartsWith("cbk_session=", StringComparison.Ordinal));

        var value = cookie["cbk_session=".Length..];

        var separator = value.IndexOf(';', StringComparison.Ordinal);

        return separator < 0 ? value : value[..separator];
    }

    /// <summary>
    /// Beendet den Host und räumt auf.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        Client.Dispose();

        await _app.StopAsync(TimeSpan.FromSeconds(10));
        await _app.DisposeAsync();

        if (Directory.Exists(_directory))
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // Der Schreibzähler kann noch gesperrt sein - das stört nicht.
            }
        }
    }
}
