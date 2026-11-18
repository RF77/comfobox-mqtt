using ComfoBox.Bacnet;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Protection;
using ComfoBox.Domain.Values;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für den BACnet-Zugriff.
/// </summary>
public sealed class BacnetDeviceTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "comfobox-bacnet-" + Guid.NewGuid().ToString("N"));

    private readonly FakeBacnetClient _client = new();
    private readonly ValueCache _cache = new();
    private readonly ErrorCollector _errors = new();
    private readonly WriteCounter _counter;

    /// <summary>
    /// Erzeugt den Test.
    /// </summary>
    public BacnetDeviceTests()
    {
        Directory.CreateDirectory(_directory);
        _counter = new WriteCounter(Path.Combine(_directory, WriteCounter.FileName));
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private BacnetDevice CreateDevice(int itemTimeoutMs = 500, bool expertMode = false, WriteCounter? counter = null)
    {
        var bacnet = Options.Create(new BacnetOptions
        {
            Port = "COM3",
            Baudrate = 38400,
            DeviceId = 1,
            ItemTimeoutMs = itemTimeoutMs,
        });

        var gateway = Options.Create(new GatewayOptions
        {
            BaseTopic = "ComfoBox",
            ExpertMode = expertMode,
        });

        return new BacnetDevice(
            _client,
            _cache,
            _errors,
            counter ?? _counter,
            bacnet,
            gateway,
            new BacnetRuntimeSettings(bacnet.Value.DeviceId, bacnet.Value.ItemTimeoutMs),
            new GatewayRuntimeSettings(expertMode),
            NullLogger<BacnetDevice>.Instance);
    }

    private static ItemDefinition Item(uint objectId, ItemValueKind kind = ItemValueKind.Analog, bool readOnly = false)
        => new()
        {
            Id = $"item.{objectId}",
            Group = "Zone",
            Name = objectId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            BacnetObjectId = (int)objectId,
            Kind = kind,
            IsReadOnly = readOnly,
        };

    [Fact]
    public async Task Der_Gelesene_Wert_Landet_Im_Cache()
    {
        _client.Values[62] = 21.5f;
        var device = CreateDevice();

        var gelesen = await device.ReadItemsAsync([Item(62)], CancellationToken.None);

        Assert.Single(gelesen);
        Assert.True(_cache.TryGet("item.62", out var wert));
        Assert.Equal(21.5f, ((AnalogValue)wert!.Value!).Value);
    }

    [Fact]
    public async Task Ein_Lesefehler_Bricht_Den_Zyklus_Nicht_Ab()
    {
        _client.Values[62] = 21.5f;
        _client.Values[63] = 22.5f;
        _client.FailingObjects.Add(61);

        var device = CreateDevice();

        var gelesen = await device.ReadItemsAsync([Item(61), Item(62), Item(63)], CancellationToken.None);

        Assert.Equal(2, gelesen.Count);
        Assert.Equal([61u, 62u, 63u], _client.ReadObjects);

        // Der Fehler landet in der Fehler-Senke.
        Assert.Single(_errors.Snapshot());
        Assert.Equal("timeout", _errors.Snapshot()[0].Code);
    }

    [Fact]
    public async Task Ein_Haengender_Wert_Blockiert_Die_Anderen_Nicht()
    {
        _client.Values[62] = 21.5f;
        _client.HangingObjects.Add(61);

        var device = CreateDevice(itemTimeoutMs: 200);

        var gelesen = await device.ReadItemsAsync([Item(61), Item(62)], CancellationToken.None);

        Assert.Single(gelesen);
        Assert.Equal(62u, _client.ReadObjects[1]);
        Assert.True(_cache.TryGet("item.62", out _));
    }

    [Fact]
    public async Task Ein_Date_Item_Bleibt_Ohne_Wert()
    {
        // Befund A20: der Wert eines Legacy-DateValue bleibt leer.
        _client.Values[3353] = 42f;
        var device = CreateDevice();

        var gelesen = await device.ReadItemsAsync([Item(3353, ItemValueKind.Date)], CancellationToken.None);

        Assert.Single(gelesen);
        Assert.False(_cache.TryGet("item.3353", out _));
    }

    [Fact]
    public async Task Der_Sentinel_32767_Wird_Nicht_Als_Wert_Gespeichert()
    {
        // Der Regler meldet einen nicht vorhandenen Fühler mit dem
        // INT16-Maximum. Der Wert ist keine Aussage und darf nirgends
        // auftauchen - weder im Cache noch als Fehler im Ringpuffer.
        _client.Values[24] = HardwareRequirements.NoSensorValue;
        var device = CreateDevice();

        var temperature = new ItemDefinition
        {
            Id = "status.buffer.temperature_top",
            Group = "Status/Buffer",
            Name = "TemperatureTop",
            BacnetObjectId = 24,
            Kind = ItemValueKind.Analog,
            Unit = "°C",
            IsReadOnly = true,
        };

        var gelesen = await device.ReadItemsAsync([temperature], CancellationToken.None);

        Assert.Empty(gelesen);
        Assert.False(_cache.TryGet(temperature.Id, out _));
        Assert.Empty(_errors.Snapshot());
    }

    [Fact]
    public async Task Ein_Grosser_Zaehlerstand_Ist_Kein_Sentinel()
    {
        // Nur Temperaturfühler liefern 32767. Ein Zähler mit diesem Stand
        // (Betriebsstunden) ist ein echter Wert.
        _client.Values[211] = 32767f;
        var device = CreateDevice();

        var hours = new ItemDefinition
        {
            Id = "states.controller.operating_hours",
            Group = "States/Controller",
            Name = "OperatingHours",
            BacnetObjectId = 211,
            Kind = ItemValueKind.Analog,
            Unit = "h",
            IsReadOnly = true,
        };

        var gelesen = await device.ReadItemsAsync([hours], CancellationToken.None);

        Assert.Single(gelesen);
        Assert.True(_cache.TryGet(hours.Id, out var wert));
        Assert.Equal(32767f, ((AnalogValue)wert!.Value!).Value);
    }

    [Fact]
    public async Task Ein_Enum_Item_Wird_Ueber_Den_Namen_Identifiziert()
    {
        _client.Values[830] = 6f;
        var device = CreateDevice();

        var item = new ItemDefinition
        {
            Id = "zone.operation_mode",
            Group = "Zone",
            Name = "OperationMode",
            BacnetObjectId = 830,
            Kind = ItemValueKind.Enum,
            EnumValues = ItemEnums.OperationModes,
        };

        await device.ReadItemsAsync([item], CancellationToken.None);

        Assert.True(_cache.TryGet("zone.operation_mode", out var wert));
        Assert.Equal("Normal", ((EnumValue)wert!.Value!).Name);
    }

    [Fact]
    public async Task Ein_Schreibvorgang_Laeuft_Ueber_Den_Zaehler()
    {
        var device = CreateDevice();

        var ergebnis = await device.WriteAsync(Item(62), 21.5, CancellationToken.None);

        Assert.Equal(WriteOutcome.Ok, ergebnis);
        Assert.Equal(1, _counter.Count);
        Assert.Equal([62u], _client.WrittenObjects);
    }

    [Fact]
    public async Task Ein_ReadOnly_Item_Wird_Abgelehnt()
    {
        var device = CreateDevice();

        var ergebnis = await device.WriteAsync(Item(62, readOnly: true), 21.5, CancellationToken.None);

        Assert.Equal(WriteOutcome.ReadOnly, ergebnis);
        Assert.Empty(_client.WrittenObjects);
    }

    /// <summary>
    /// Die neuen V2-Konfigurationswerte (Welle 2c) sind ohne Experten-Modus gesperrt.
    /// </summary>
    /// <remarks>
    /// ADR-014: kein BACnet-Request, kein Verbrauch des Schreibzählers - der Wert
    /// verändert das Regelverhalten der Anlage.
    /// </remarks>
    [Fact]
    public async Task Ein_Config_Item_Braucht_Den_Experten_Modus()
    {
        var item = new ItemCatalog().FindByTopicPath("Config/Ventilation/SpeedStage3");
        Assert.NotNull(item);

        var device = CreateDevice(expertMode: false);

        var ergebnis = await device.WriteAsync(item!, 50, CancellationToken.None);

        Assert.Equal(WriteOutcome.ExpertModeRequired, ergebnis);
        Assert.Empty(_client.WrittenObjects);
        Assert.Equal(0, _counter.Count);
    }

    [Fact]
    public async Task Ein_Wert_Ausserhalb_Der_Grenzen_Wird_Abgelehnt()
    {
        var item = new ItemDefinition
        {
            Id = "zone.set_point_normal",
            Group = "Zone",
            Name = "SetPointNormal",
            BacnetObjectId = 62,
            Kind = ItemValueKind.Analog,
            Min = 5f,
            Max = 30f,
        };

        var device = CreateDevice();

        var ergebnis = await device.WriteAsync(item, 40, CancellationToken.None);

        Assert.Equal(WriteOutcome.OutOfRange, ergebnis);
        Assert.Empty(_client.WrittenObjects);
    }

    [Fact]
    public async Task Ein_Nicht_Erlaubter_Wert_Wird_Abgelehnt()
    {
        // R9: Kühlen kennt 0/1/3 - der Wert 2 liegt im Bereich, ist aber ungültig.
        var item = new ItemDefinition
        {
            Id = "config.energy_comsumer.cooling_zone.cooling",
            Group = "Config/EnergyComsumer/CoolingZone",
            Name = "Cooling",
            BacnetObjectId = 470,
            Kind = ItemValueKind.Analog,
            Min = 0f,
            Max = 3f,
            AllowedValues = [0f, 1f, 3f],
        };

        var device = CreateDevice();

        var ergebnis = await device.WriteAsync(item, 2, CancellationToken.None);

        Assert.Equal(WriteOutcome.OutOfRange, ergebnis);
        Assert.Empty(_client.WrittenObjects);
        Assert.Equal(0, _counter.Count);

        Assert.Equal(WriteOutcome.Ok, await device.WriteAsync(item, 3, CancellationToken.None));
        Assert.Equal([470u], _client.WrittenObjects);
    }

    [Fact]
    public async Task Ein_Fehlgeschlagener_Schreibvorgang_Zaehlt_Nicht()
    {
        // Befund A22: nur erfolgreiche Schreibvorgänge zählen.
        _client.FailWrites = true;
        var device = CreateDevice();

        var ergebnis = await device.WriteAsync(Item(62), 21.5, CancellationToken.None);

        Assert.Equal(WriteOutcome.BusError, ergebnis);
        Assert.Equal(0, _counter.Count);
    }

    [Fact]
    public async Task Beim_Erreichten_Des_Limits_Wird_Abgelehnt()
    {
        var counter = new WriteCounter(Path.Combine(_directory, "limit.json"), limit: 1);
        var device = CreateDevice(counter: counter);

        Assert.Equal(WriteOutcome.Ok, await device.WriteAsync(Item(62), 21.5, CancellationToken.None));
        Assert.Equal(WriteOutcome.LimitExceeded, await device.WriteAsync(Item(62), 21.5, CancellationToken.None));
    }

    [Fact]
    public async Task Die_Reihenfolge_der_Lesezugriffe_Folgt_Dem_Poll_Plan()
    {
        _client.Values[1] = 1f;
        _client.Values[2] = 2f;
        _client.Values[3] = 3f;

        var device = CreateDevice();

        var plan = PollPlan.ForCycle([Item(1), Item(2)], [Item(3)], [], [], cycle: 0, secondaryFactor: 5, slowFactor: 60);
        await device.ReadItemsAsync(plan, CancellationToken.None);

        Assert.Equal([1u, 2u, 3u], _client.ReadObjects);
    }
}
