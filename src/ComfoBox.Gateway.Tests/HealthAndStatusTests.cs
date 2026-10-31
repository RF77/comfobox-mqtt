using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using ComfoBox.Gateway.Protection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für den Health-Status und den Status-Publisher.
/// </summary>
public sealed class HealthAndStatusTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "comfobox-status-" + Guid.NewGuid().ToString("N"));

    private readonly RecordingMqttPublisher _publisher = new();
    private readonly HealthReporter _health = new();
    private readonly ErrorCollector _errors = new();
    private readonly WriteCounter _counter;
    private readonly ItemCatalog _catalog = new();

    public HealthAndStatusTests()
    {
        Directory.CreateDirectory(_directory);
        _counter = new WriteCounter(Path.Combine(_directory, WriteCounter.FileName));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }

        GC.SuppressFinalize(this);
    }

    [Fact]
    public void Ein_Gesundes_Gateway_Ist_Online()
    {
        _health.ReportHealthy("Mqtt", "MQTT verbunden");
        _health.ReportHealthy("Bacnet", "BACnet verbunden");

        Assert.Equal(HealthState.Online, _health.Current);
    }

    [Fact]
    public void Eine_Defekte_Komponente_Macht_Das_Gateway_Degraded()
    {
        _health.ReportHealthy("Mqtt", "MQTT verbunden");
        _health.Report("Bacnet", HealthState.Degraded, "Bus antwortet nicht");

        Assert.Equal(HealthState.Degraded, _health.Current);
        Assert.Contains("Bacnet", _health.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Eine_Offline_Komponente_Hat_Vorrang_Vor_Degraded()
    {
        _health.Report("Mqtt", HealthState.Offline, "Broker weg");
        _health.Report("Bacnet", HealthState.Degraded, "Bus antwortet nicht");

        Assert.Equal(HealthState.Offline, _health.Current);
    }

    [Fact]
    public void Die_Health_Anderung_Wird_Meldedet()
    {
        var ereignisse = new List<HealthChangedEventArgs>();
        _health.Changed += (_, e) => ereignisse.Add(e);

        _health.ReportHealthy("Mqtt", "MQTT verbunden");
        _health.Report("Bacnet", HealthState.Degraded, "Bus antwortet nicht");

        Assert.Single(ereignisse);
        Assert.Equal(HealthState.Degraded, ereignisse[0].State);
    }

    [Fact]
    public void Benutzerfehler_Verschlechtern_Den_Health_Status_Nicht()
    {
        _health.ReportHealthy("Mqtt", "MQTT verbunden");
        _health.ReportHealthy("Bacnet", "BACnet verbunden");

        // Ein abgelehnter Schreibvorgang ist ein Benutzerfehler.
        _errors.Report("Mqtt", "value_out_of_range", "Wert ausserhalb des Bereichs", null, "warning");

        Assert.Equal(HealthState.Online, _health.Current);
    }

    [Fact]
    public async Task Der_Status_Publisher_Publiziert_Den_Health_Topic()
    {
        var publisher = CreateStatusPublisher();
        var client = new NoopMqttClient();

        _health.ReportHealthy("Mqtt", "MQTT verbunden");

        await publisher.OnConnectedAsync(client, CancellationToken.None);

        Assert.Equal("online", client.LastPayloadFor("ComfoBox/Status/Health"));
    }

    [Fact]
    public async Task Der_Status_Publisher_Publiziert_Offline_Beim_Shutdown()
    {
        var publisher = CreateStatusPublisher();
        var client = new NoopMqttClient();

        _health.Report("Mqtt", HealthState.Offline, "Der Gateway wird beendet.");

        await publisher.OnConnectedAsync(client, CancellationToken.None);

        Assert.Equal("offline", client.LastPayloadFor("ComfoBox/Status/Health"));
    }

    [Fact]
    public async Task Der_Status_Publisher_Publiziert_Die_Sonder_Topics()
    {
        var publisher = CreateStatusPublisher();
        var client = new NoopMqttClient();

        await publisher.OnConnectedAsync(client, CancellationToken.None);

        Assert.Equal(MqttVersion.Current, client.LastPayloadFor("ComfoBox/Status/Version"));
        Assert.Equal("0", client.LastPayloadFor("ComfoBox/Special/NumberOfWritesPer24h"));
        Assert.Equal("200", client.LastPayloadFor("ComfoBox/Status/WritesMax"));
        Assert.NotNull(client.LastPayloadFor("ComfoBox/Status/Uptime"));
        Assert.NotNull(client.LastPayloadFor("ComfoBox/Status/StartedAt"));
    }

    [Fact]
    public async Task Ein_Fehler_Erscheint_In_Der_Fehlerhistorie()
    {
        var publisher = CreateStatusPublisher();

        _errors.Report("Bacnet", "timeout", "ReadProperty hat nicht geantwortet");

        await publisher.OnConnectedAsync(new NoopMqttClient(), CancellationToken.None);

        // Der Fehler landet in Status/LastError und in Status/Errors.
        Assert.Contains(_publisher.Published, p => p.Topic == "ComfoBox/Status/LastError");
    }

    private MqttStatusPublisher CreateStatusPublisher()
    {
        var options = new MqttOptions
        {
            Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
            ClientId = "comfobox-test",
            StatusIntervalSeconds = 1,
        };

        return new MqttStatusPublisher(
            new MqttTopicMap(_catalog, "ComfoBox", expertModeEnabled: false),
            _publisher,
            _health,
            _errors,
            _counter,
            new BacnetOptions { Port = "COM3", Baudrate = 38400, DeviceId = 1 },
            options,
            TimeProvider.System,
            NullLogger<MqttStatusPublisher>.Instance);
    }
}
