using ComfoBox.Bacnet;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway;
using ComfoBox.Gateway.Logging;
using ComfoBox.Gateway.Mqtt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Der End-to-End-Test: der **komplette** Gateway gegen einen eingebetteten Broker.
/// </summary>
/// <remarks>
/// <para>
/// Dieser Test baut den echten DI-Container auf (wie <c>Program.cs</c> es
/// tut), startet den Host und prüft, dass
/// </para>
/// <list type="bullet">
/// <item>der Gateway startet (der DI-Container ist vollständig);</item>
/// <item><c>ComfoBox/Connected</c> und <c>ComfoBox/Version</c> publiziert
/// werden;</item>
/// <item>die Item-Werte auf ihren Topics ankommen;</item>
/// <item>die Discovery für alle 127 Items publiziert wird.</item>
/// </list>
/// </remarks>
public sealed class GatewayEndToEndTests : IAsyncLifetime, IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "comfobox-e2e-" + Guid.NewGuid().ToString("N"));

    private readonly List<string> _received = [];

    private MqttServer? _server;
    private IMqttClient? _observer;
    private IHost? _host;
    private int _port;

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_directory);

        _port = GetFreePort();

        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(_port)
            .Build();

        _server = new MqttServerFactory().CreateMqttServer(options);
        await _server.StartAsync();

        _observer = new MqttClientFactory().CreateMqttClient();
        await _observer.ConnectAsync(
            new MqttClientOptionsBuilder()
                .WithClientId("comfobox-e2e-observer")
                .WithTcpServer("127.0.0.1", _port)
                .WithProtocolVersion(MqttProtocolVersion.V500)
                .Build());

        _observer.ApplicationMessageReceivedAsync += e =>
        {
            lock (_received)
            {
                _received.Add($"{e.ApplicationMessage.Topic}={e.ApplicationMessage.ConvertPayloadToString()}");
            }

            return Task.CompletedTask;
        };

        await _observer.SubscribeAsync("#", MqttQualityOfServiceLevel.AtMostOnce, CancellationToken.None);
    }

    public async Task DisposeAsync()
    {
        if (_host is not null)
        {
            await _host.StopAsync(TimeSpan.FromSeconds(10));
            _host.Dispose();
        }

        if (_observer is not null)
        {
            try
            {
                await _observer.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
            }
            catch (Exception)
            {
                // Der Broker ist schon weg.
            }

            _observer.Dispose();
        }

        if (_server is not null)
        {
            await _server.StopAsync();
            _server.Dispose();
        }

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    /// <inheritdoc />
    public void Dispose() => GC.SuppressFinalize(this);

    [Fact]
    public async Task Der_Gateway_Startet_Und_Publiziert_Alles()
    {
        await StartGatewayAsync();

        // 1) Connected und Version.
        await WaitForAsync(() => Has("ComfoBox/Connected=1"));
        Assert.True(Has("ComfoBox/Connected=1"), "ComfoBox/Connected=1 wurde nicht publiziert.");
        Assert.True(
            Received().Any(m => m.StartsWith("ComfoBox/Version=Ver ", StringComparison.Ordinal)),
            "ComfoBox/Version wurde nicht publiziert.");

        // 2) Der Wert eines Items landet auf seinem Topic.
        var cache = _host!.Services.GetRequiredService<IValueCache>();
        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));

        await WaitForAsync(() => Has("ComfoBox/Zone/SetPointNormal=21.5"));
        Assert.True(Has("ComfoBox/Zone/SetPointNormal=21.5"), "Der Item-Wert wurde nicht publiziert.");

        // 3) Der Enum-Wert landet auf seinem Topic und als Zahl.
        cache.Update("zone.operation_mode", new EnumValue(6, ItemEnums.OperationModes));

        await WaitForAsync(() => Has("ComfoBox/Zone/OperationMode=Normal"));
        Assert.True(Has("ComfoBox/Zone/OperationMode=Normal"), "Der Enum-Wert wurde nicht publiziert.");
        Assert.True(Has("ComfoBox/Zone/OperationMode/AsNumber=6"), "Der Enum-Zahlwert wurde nicht publiziert.");

        // 4) Die Discovery für alle 127 Items.
        await WaitForAsync(() => Received().Count(m => m.StartsWith("homeassistant/", StringComparison.Ordinal)) >= 127);
        Assert.True(
            Received().Count(m => m.StartsWith("homeassistant/", StringComparison.Ordinal)) >= 127,
            $"Die Discovery wurde nicht für alle Items publiziert. Empfangen: {Received().Count(m => m.StartsWith("homeassistant/", StringComparison.Ordinal))}");

        Assert.True(HasTopic("homeassistant/device/comfobox/config"), "Die Geräte-Discovery fehlt.");
        Assert.True(Has("ComfoBox/Status/Health=online"), $"Der Health-Status fehlt. Empfangen: {string.Join(", ", Received().Take(40))}");
    }

    [Fact]
    public async Task Der_Gateway_Startet_Ueberhaupt()
    {
        // Der Container muss vollständig auflösbar sein - das prüft die
        // DI-Registrierung (Phase 3 hat sie um die MQTT-Dienste erweitert).
        await StartGatewayAsync();

        Assert.NotNull(_host!.Services.GetRequiredService<MqttConnectionManager>());
        Assert.NotNull(_host.Services.GetRequiredService<IMqttPublisher>());
        Assert.NotNull(_host.Services.GetRequiredService<MqttSubscriber>());
        Assert.NotNull(_host.Services.GetRequiredService<HomeAssistantDiscovery>());
        Assert.NotNull(_host.Services.GetRequiredService<IItemCatalog>());
    }

    private async Task StartGatewayAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Gateway:BaseTopic"] = "ComfoBox",
                ["Gateway:DataDirectory"] = _directory,
                ["Gateway:ExpertMode"] = "false",
                ["Gateway:PollIntervalSeconds"] = "3600",
                ["Bacnet:Port"] = "COM_TEST",
                ["Bacnet:Baudrate"] = "38400",
                ["Bacnet:DeviceId"] = "1",
                ["Mqtt:ClientId"] = "comfobox-e2e",
                ["Mqtt:KeepAliveSeconds"] = "15",
                ["Mqtt:Brokers:0:Host"] = "127.0.0.1",
                ["Mqtt:Brokers:0:Port"] = _port.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["Logging:Directory"] = Path.Combine(_directory, "logs"),
            })
            .Build();

        var logBuffer = new InMemoryLogSink(100, Serilog.Events.LogEventLevel.Warning);

        _host = new HostBuilder()
            .ConfigureLogging(logging => logging.ClearProviders())
            .ConfigureServices((_, configured) =>
            {
                configured.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));
                configured.AddGatewayServices(configuration, _directory, logBuffer);

                // Der BACnet-Client wird durch die Attrappe ersetzt - der Test
                // darf keine echte serielle Schnittstelle öffnen.
                configured.AddSingleton<IBacnetClient, FakeBacnetClient>();
            })
            .Build();

        await _host.StartAsync();
    }

    private IReadOnlyList<string> Received()
    {
        lock (_received)
        {
            return [.. _received];
        }
    }

    private bool Has(string message)
        => Received().Contains(message, StringComparer.Ordinal);

    private bool HasTopic(string topic)
        => Received().Any(m => m.StartsWith(topic + "=", StringComparison.Ordinal));

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(20);
        var end = DateTime.UtcNow + limit;

        while (DateTime.UtcNow < end)
        {
            if (condition())
            {
                return;
            }

            await Task.Delay(100);
        }
    }

    private static int GetFreePort()
    {
        var listener = new System.Net.Sockets.TcpListener(System.Net.IPAddress.Loopback, 0);
        listener.Start();
        var port = ((System.Net.IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }
}
