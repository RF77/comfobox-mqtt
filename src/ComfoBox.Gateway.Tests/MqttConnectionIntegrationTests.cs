using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;
using MQTTnet;
using MQTTnet.Formatter;
using MQTTnet.Protocol;
using MQTTnet.Server;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Integrationstests für die MQTT-Verbindung gegen einen eingebetteten Broker.
/// </summary>
/// <remarks>
/// <para>
/// Der Test startet einen MQTT-Broker im Prozess (MQTTnet.Server). Damit
/// lässt sich das **echte** Verbindungsverhalten prüfen - ohne Docker und
/// ohne externe Infrastruktur.
/// </para>
/// <para>
/// Der wichtigste Test ist
/// <see cref="Connected_Wird_Nach_Jedem_Reconnect_Publiziert"/> - die
/// Regression aus Befund A10: der Legacy publizierte <c>Connected=1</c> nur
/// beim ersten Connect, danach sah Home Assistant dauerhaft „offline".
/// </para>
/// </remarks>
public sealed class MqttConnectionIntegrationTests : IAsyncLifetime, IDisposable
{
    private const string BaseTopic = "ComfoBox";

    private readonly List<IMqttClient> _clients = [];
    private readonly List<string> _connectedPayloads = [];
    private readonly List<string> _healthPayloads = [];

    private MqttServer? _server;
    private IMqttClient? _observer;
    private int _port;
    private MqttConnectionManager? _manager;

    public async Task InitializeAsync()
    {
        _port = GetFreePort();
        await StartBrokerAsync();
        await ConnectObserverAsync();
    }

    public async Task DisposeAsync()
    {
        if (_manager is not null)
        {
            await _manager.StopAsync(CancellationToken.None);
            await _manager.DisposeAsync();
        }

        foreach (var client in _clients)
        {
            try
            {
                await client.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
            }
            catch (Exception)
            {
                // Der Broker ist schon weg - das ist beim Abbau in Ordnung.
            }

            client.Dispose();
        }

        if (_server is not null)
        {
            await _server.StopAsync();
            _server.Dispose();
        }
    }

    [Fact]
    public async Task Connected_Wird_Nach_Jedem_Reconnect_Publiziert()
    {
        await StartManagerAsync();

        await WaitForAsync(() => ConnectedCount() >= 1);

        Assert.Equal("1", LastConnected());

        // Der Broker wird neu gestartet - der Gateway muss sich neu verbinden
        // und "Connected=1" erneut publizieren (Befund A10).
        await RestartBrokerAsync();

        await WaitForAsync(() => ConnectedCount() >= 2, TimeSpan.FromSeconds(30));

        Assert.Equal("1", LastConnected());
        Assert.True(ConnectedCount() >= 2, "Connected=1 wurde nach dem Reconnect nicht publiziert.");
    }

    [Fact]
    public async Task Die_Version_Wird_Mit_Dem_Praefix_Ver_Publiziert()
    {
        await StartManagerAsync();

        await WaitForAsync(() => LastConnected() == "1");

        var version = await ReceiveAsync($"{BaseTopic}/Version");

        Assert.NotNull(version);
        Assert.StartsWith("Ver ", version, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Der_Shutdown_Publiziert_Offline_Und_Connected_Zero()
    {
        await StartManagerAsync();

        await WaitForAsync(() => ConnectedCount() >= 1);

        await _manager!.StopAsync(CancellationToken.None);

        await WaitForAsync(() => ConnectedCount() >= 2, TimeSpan.FromSeconds(10));

        Assert.Equal("0", LastConnected());
    }

    [Fact]
    public async Task Die_Last_Will_Topics_Sind_Korrekt_Registriert()
    {
        // MQTT 5 erlaubt nur einen Last Will pro Verbindung - der Vertrag
        // verlangt zwei. Deshalb gibt es je Broker zwei Verbindungen.
        var options = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "127.0.0.1", Port = _port }],
            ClientId = "comfobox-test",
            KeepAliveSeconds = 30,
        };

        var topics = new MqttTopicMap(new ItemCatalog(), BaseTopic, expertModeEnabled: false);
        var factory = new MqttConnectionFactory(options, topics);

        var main = factory.BuildOptions(options.Brokers[0], options.ClientId, MqttConnectionRole.Main);
        var will = factory.BuildOptions(options.Brokers[0], MqttConnectionFactory.WillClientId(options.ClientId), MqttConnectionRole.Will);

        Assert.Equal($"{BaseTopic}/Status/Health", main.WillTopic);
        Assert.Equal("offline", System.Text.Encoding.UTF8.GetString(main.WillPayload!));
        Assert.True(main.WillRetain);

        Assert.Equal($"{BaseTopic}/Connected", will.WillTopic);
        Assert.Equal("0", System.Text.Encoding.UTF8.GetString(will.WillPayload!));
        Assert.True(will.WillRetain);
    }

    [Fact]
    public void Das_Keepalive_Ist_Mindestens_15_Sekunden()
    {
        var options = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "127.0.0.1", Port = _port }],
            KeepAliveSeconds = 30,
        };

        var topics = new MqttTopicMap(new ItemCatalog(), BaseTopic, expertModeEnabled: false);
        var factory = new MqttConnectionFactory(options, topics);

        var clientOptions = factory.BuildOptions(options.Brokers[0], "test", MqttConnectionRole.Main);

        Assert.Equal(TimeSpan.FromSeconds(30), clientOptions.KeepAlivePeriod);
        Assert.Equal(MqttProtocolVersion.V500, clientOptions.ProtocolVersion);
    }

    private async Task StartManagerAsync()
    {
        var options = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "127.0.0.1", Port = _port }],
            ClientId = "comfobox-test",
            KeepAliveSeconds = 15,
        };

        var topics = new MqttTopicMap(new ItemCatalog(), BaseTopic, expertModeEnabled: false);
        var factory = new MqttConnectionFactory(options, topics);

        _manager = new MqttConnectionManager(
            options,
            topics,
            factory,
            new MqttClientRegistry(),
            new HealthReporter(),
            new ErrorCollector(),
            [],
            NullLogger<MqttConnectionManager>.Instance);

        await _manager.StartAsync(CancellationToken.None);
    }

    /// <inheritdoc />
    public void Dispose() => GC.SuppressFinalize(this);

    private async Task StartBrokerAsync()
    {
        var options = new MqttServerOptionsBuilder()
            .WithDefaultEndpoint()
            .WithDefaultEndpointPort(_port)
            .Build();

        _server = new MqttServerFactory().CreateMqttServer(options);
        await _server.StartAsync();
    }

    private async Task ConnectObserverAsync()
    {
        var client = new MqttClientFactory().CreateMqttClient();

        await client.ConnectAsync(
            new MqttClientOptionsBuilder()
                .WithClientId($"comfobox-test-observer-{Guid.NewGuid():N}")
                .WithTcpServer("127.0.0.1", _port)
                .WithProtocolVersion(MqttProtocolVersion.V500)
                .Build());

        client.ApplicationMessageReceivedAsync += e =>
        {
            var topic = e.ApplicationMessage.Topic;
            var payload = e.ApplicationMessage.ConvertPayloadToString();

            if (topic == $"{BaseTopic}/Connected")
            {
                lock (_connectedPayloads)
                {
                    _connectedPayloads.Add(payload);
                }
            }
            else if (topic == $"{BaseTopic}/Status/Health")
            {
                lock (_healthPayloads)
                {
                    _healthPayloads.Add(payload);
                }
            }

            return Task.CompletedTask;
        };

        await client.SubscribeAsync($"{BaseTopic}/#", MqttQualityOfServiceLevel.AtMostOnce, CancellationToken.None);

        _observer = client;
        _clients.Add(client);
    }

    private async Task RestartBrokerAsync()
    {
        await _server!.StopAsync();
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await StartBrokerAsync();

        // Der Beobachter muss sich neu verbinden - der Broker ist neu.
        if (_observer is not null)
        {
            try
            {
                await _observer.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
            }
            catch (Exception)
            {
                // Die Verbindung ist schon weg.
            }
        }

        await ConnectObserverAsync();
    }

    private int ConnectedCount()
    {
        lock (_connectedPayloads)
        {
            return _connectedPayloads.Count;
        }
    }

    private string? LastConnected()
    {
        lock (_connectedPayloads)
        {
            return _connectedPayloads.Count == 0 ? null : _connectedPayloads[^1];
        }
    }

    private async Task<string?> ReceiveAsync(string topic)
    {
        var client = new MqttClientFactory().CreateMqttClient();
        await client.ConnectAsync(
            new MqttClientOptionsBuilder()
                .WithClientId($"comfobox-test-reader-{Guid.NewGuid():N}")
                .WithTcpServer("127.0.0.1", _port)
                .WithProtocolVersion(MqttProtocolVersion.V500)
                .Build());

        try
        {
            var received = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            client.ApplicationMessageReceivedAsync += e =>
            {
                if (e.ApplicationMessage.Topic == topic)
                {
                    received.TrySetResult(e.ApplicationMessage.ConvertPayloadToString());
                }

                return Task.CompletedTask;
            };

            await client.SubscribeAsync(topic);

            // Das Topic ist retained - der Wert kommt sofort.
            return await received.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            await client.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None);
            client.Dispose();
        }
    }

    private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        var limit = timeout ?? TimeSpan.FromSeconds(15);
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
