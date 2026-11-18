using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Hardware;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Gateway.Tests.Hardware;

/// <summary>
/// Tests für das Gate der Baugruppen-Verfügbarkeit (Aufgabe 8.C).
/// </summary>
/// <remarks>
/// Die Tests fahren die Naht direkt an (<see cref="HardwareAvailabilityGate.ApplyAsync"/>)
/// - kein Hintergrunddienst, kein Broker, keine Zeit.
/// </remarks>
public sealed class HardwareAvailabilityGateTests
{
    private const string BufferTypeId = "config.general.buffer_type";
    private const string HeatPumpSensorId = "config.general.heat_pump_sensor";
    private const string BufferTopId = "status.buffer.temperature_top";
    private const string BufferBottomId = "status.buffer.temperature_bottom";

    private static readonly ItemCatalog Catalog = new();

    [Fact]
    public void Ohne_Gelesenen_Typ_Parameter_Ist_Der_Zustand_Unbekannt()
    {
        var harness = Harness.Create();

        Assert.Equal(HardwareState.Unknown, harness.Gate.StateOf(HardwareRequirements.BufferTop));
        Assert.False(harness.Gate.IsAvailable(BufferTopId));
        Assert.Equal("unknown", harness.Gate.UnavailableReason(BufferTopId));
    }

    [Fact]
    public void Ein_Item_Ohne_Baugruppe_Ist_Immer_Verfuegbar()
    {
        var harness = Harness.Create();

        Assert.True(harness.Gate.IsAvailable("zone.set_point_normal"));
        Assert.Null(harness.Gate.UnavailableReason("zone.set_point_normal"));
        Assert.True(harness.Gate.ShouldPoll(Catalog.Find("zone.set_point_normal")!));
    }

    [Fact]
    public void Die_Typ_Parameter_Werden_Immer_Gelesen()
    {
        var harness = Harness.Create();

        Assert.All(
            HardwareRequirements.All,
            requirement => Assert.True(
                harness.Gate.ShouldPoll(Catalog.Find(requirement.DetectorItemId)!),
                $"{requirement.DetectorItemId} ist ein Typ-Parameter und muss gelesen werden."));
    }

    [Fact]
    public void Ein_Pufferspeicher_Mit_Einem_Fuehler_Liest_Nur_Oben()
    {
        var harness = Harness.Create();
        harness.SetDetector(BufferTypeId, 1f);

        Assert.Equal(HardwareState.Available, harness.Gate.StateOf(HardwareRequirements.BufferTop));
        Assert.Equal(HardwareState.Unavailable, harness.Gate.StateOf(HardwareRequirements.BufferBottom));

        Assert.True(harness.Gate.ShouldPoll(Catalog.Find(BufferTopId)!));
        Assert.False(harness.Gate.ShouldPoll(Catalog.Find(BufferBottomId)!));
        Assert.Null(harness.Gate.UnavailableReason(BufferTopId));
        Assert.Equal("not_installed", harness.Gate.UnavailableReason(BufferBottomId));
    }

    [Fact]
    public void Zwei_Pufferspeicher_Fuehler_Lesen_Beide()
    {
        var harness = Harness.Create();
        harness.SetDetector(BufferTypeId, 11f);

        Assert.True(harness.Gate.ShouldPoll(Catalog.Find(BufferTopId)!));
        Assert.True(harness.Gate.ShouldPoll(Catalog.Find(BufferBottomId)!));
    }

    [Fact]
    public async Task Der_Wechsel_Publiziert_Die_Verfuegbarkeit_Und_Leert_Die_Werte()
    {
        var harness = Harness.Create();

        // Der erste Durchlauf setzt alle Verfügbarkeits-Topics auf "unbekannt"
        // und leert die Werte der noch nicht bestätigten Baugruppen.
        await harness.Gate.ApplyAsync(CancellationToken.None);

        foreach (var requirement in HardwareRequirements.All)
        {
            Assert.Equal(
                string.Empty,
                harness.Publisher.LastPayloadFor($"ComfoBox/Status/{requirement.TopicPath}"));
        }

        // Jetzt meldet der Regler: kein Pufferspeicher.
        harness.SetDetector(BufferTypeId, 0f);
        await harness.Gate.ApplyAsync(CancellationToken.None);

        Assert.Equal(
            "0",
            harness.Publisher.LastPayloadFor("ComfoBox/Status/Hardware/BufferTop"));
        Assert.Equal(
            "0",
            harness.Publisher.LastPayloadFor("ComfoBox/Status/Hardware/BufferBottom"));

        // Die retained Topics der beiden Fühler sind geleert.
        Assert.Equal(string.Empty, harness.Publisher.LastPayloadFor("ComfoBox/Status/Buffer/TemperatureTop"));
        Assert.Equal(string.Empty, harness.Publisher.LastPayloadFor("ComfoBox/Status/Buffer/TemperatureBottom"));

        // Es gibt genau **eine** Meldung für diese Baugruppe.
        var meldungen = harness.Errors.Snapshot()
            .Where(entry => entry.Code == HardwareAvailabilityGate.NotInstalledCode)
            .ToList();
        Assert.Equal(2, meldungen.Count);
        Assert.All(meldungen, entry => Assert.Equal(HardwareAvailabilityGate.Source, entry.Source));
    }

    [Fact]
    public async Task Ein_Wechsel_Wird_Nur_Einmal_Ausgewertet()
    {
        var harness = Harness.Create();
        harness.SetDetector(BufferTypeId, 0f);

        await harness.Gate.ApplyAsync(CancellationToken.None);
        var nachErstem = harness.Publisher.Published.Count;

        await harness.Gate.ApplyAsync(CancellationToken.None);

        Assert.Equal(nachErstem, harness.Publisher.Published.Count);

        // Zwei Meldungen: Pufferspeicher oben und unten hängen an DP 805.
        Assert.Equal(2, harness.Errors.Snapshot().Length);
    }

    [Fact]
    public async Task Eine_Vorhandene_Baugruppe_Meldet_1()
    {
        var harness = Harness.Create();
        harness.SetDetector(HeatPumpSensorId, 1f);

        await harness.Gate.ApplyAsync(CancellationToken.None);

        Assert.Equal(
            "1",
            harness.Publisher.LastPayloadFor("ComfoBox/Status/Hardware/HeatPumpSensors"));

        Assert.Empty(harness.Errors.Snapshot());
    }

    [Fact]
    public async Task Der_Sentinel_Wird_Einmal_Gemeldet_Und_Leert_Das_Topic()
    {
        var harness = Harness.Create();
        var item = Catalog.Find(BufferTopId)!;

        harness.Gate.ReportNoSensor(item);
        harness.Gate.ReportNoSensor(item);

        Assert.True(harness.Gate.IsNoSensor(BufferTopId));
        Assert.Equal("no_sensor", harness.Gate.UnavailableReason(BufferTopId));

        var meldung = Assert.Single(harness.Errors.Snapshot());
        Assert.Equal(HardwareAvailabilityGate.NoSensorCode, meldung.Code);

        await harness.Gate.ApplyAsync(CancellationToken.None);
        Assert.Equal(string.Empty, harness.Publisher.LastPayloadFor("ComfoBox/Status/Buffer/TemperatureTop"));
    }

    [Fact]
    public void Ein_Zurueckgekehrter_Fuehler_Hebt_Die_Meldung_Auf()
    {
        var harness = Harness.Create();
        var item = Catalog.Find(BufferTopId)!;

        harness.Gate.ReportNoSensor(item);
        harness.Gate.ConfirmSensor(BufferTopId);

        Assert.False(harness.Gate.IsNoSensor(BufferTopId));
        Assert.NotEqual("no_sensor", harness.Gate.UnavailableReason(BufferTopId));
    }

    [Fact]
    public async Task Ein_Veralteter_Typ_Parameter_Gilt_Als_Unbekannt()
    {
        var harness = Harness.Create();
        harness.SetDetector(BufferTypeId, 11f);

        harness.Cache.MarkStale(BufferTypeId);

        Assert.Equal(HardwareState.Unknown, harness.Gate.StateOf(HardwareRequirements.BufferTop));

        await harness.Gate.ApplyAsync(CancellationToken.None);

        // Unbekannt ist nicht "vorhanden": das Topic wird geleert, nicht auf 1 gesetzt.
        Assert.Equal(string.Empty, harness.Publisher.LastPayloadFor("ComfoBox/Status/Hardware/BufferTop"));
    }

    /// <summary>
    /// Die Testumgebung: Cache, protokollierender Publisher und Fehler-Senke.
    /// </summary>
    private sealed class Harness
    {
        private Harness(ValueCache cache, RecordingMqttPublisher publisher, ErrorCollector errors, HardwareAvailabilityGate gate)
        {
            Cache = cache;
            Publisher = publisher;
            Errors = errors;
            Gate = gate;
        }

        public ValueCache Cache { get; }

        public RecordingMqttPublisher Publisher { get; }

        public ErrorCollector Errors { get; }

        public HardwareAvailabilityGate Gate { get; }

        public static Harness Create()
        {
            var cache = new ValueCache();
            var publisher = new RecordingMqttPublisher();
            var errors = new ErrorCollector();
            var options = new MqttOptions
            {
                Brokers = [new MqttBrokerOptions { Host = "localhost", Port = 1883 }],
            };

            var topics = new MqttTopicMap(Catalog, "ComfoBox", expertModeEnabled: false);

            var gate = new HardwareAvailabilityGate(
                cache,
                Catalog,
                topics,
                publisher,
                errors,
                options,
                NullLogger<HardwareAvailabilityGate>.Instance);

            return new Harness(cache, publisher, errors, gate);
        }

        /// <summary>
        /// Schreibt den Wert eines Typ-Parameters in den Cache.
        /// </summary>
        /// <param name="itemId">Die Id des Typ-Parameters.</param>
        /// <param name="value">Der Wert des Reglers.</param>
        public void SetDetector(string itemId, float value)
        {
            var item = Catalog.Find(itemId) ?? throw new InvalidOperationException($"Unbekanntes Item {itemId}.");

            ItemValue itemValue = item.Kind == ItemValueKind.Enum
                ? new EnumValue((int)value, item.EnumValues)
                : new AnalogValue(value, item.Min, item.Max, item.Unit);

            Cache.Update(item.Id, itemValue);
        }
    }
}
