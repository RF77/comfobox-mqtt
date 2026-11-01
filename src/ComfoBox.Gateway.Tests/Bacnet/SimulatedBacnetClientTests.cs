using System.Text.Json;
using ComfoBox.Bacnet;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Bacnet;
using Microsoft.Extensions.Logging.Abstractions;

namespace ComfoBox.Gateway.Tests.Bacnet;

/// <summary>
/// Tests für den simulierten BACnet-Client.
/// </summary>
/// <remarks>
/// Der simulierte Client ersetzt die ComfoBox - er ist die Grundlage für den
/// Parallelvergleich aus Phase 7 ohne echte Hardware (siehe ADR-013).
/// </remarks>
public sealed class SimulatedBacnetClientTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "comfobox-sim-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Erzeugt den Test.
    /// </summary>
    public SimulatedBacnetClientTests()
    {
        Directory.CreateDirectory(_directory);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private static SimulatedBacnetClient CreateClient(string? valuesPath = null)
        => new(new ItemCatalog(), NullLogger<SimulatedBacnetClient>.Instance, valuesPath);

    /// <summary>
    /// Baut den Inhalt einer Wert-Datei.
    /// </summary>
    /// <param name="topicPath">Der Topic-Pfad des Items.</param>
    /// <param name="value">Der Wert.</param>
    /// <returns>Das JSON.</returns>
    private static string ValuesJson(string topicPath, object value)
    {
        var document = new Dictionary<string, Dictionary<string, object>>
        {
            ["values"] = new() { [topicPath] = value },
        };

        return JsonSerializer.Serialize(document);
    }

    private static async Task<float?> ReadAsync(SimulatedBacnetClient client, int instance)
    {
        var values = await client.ReadPropertyAsync(
            new BacnetAddress(BacnetAddressTypes.MSTP, 0, [1]),
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)instance),
            BacnetPropertyIds.PROP_PRESENT_VALUE);

        return values.Count == 0 ? null : (float)values[0].Value;
    }

    [Fact]
    public async Task Liest_einen_Wert_fuer_jedes_Item()
    {
        var client = CreateClient();
        var item = new ItemCatalog().All.First(i => i.Kind == ItemValueKind.Analog);

        var value = await ReadAsync(client, item.BacnetObjectId);

        Assert.NotNull(value);
    }

    [Fact]
    public async Task Die_Werte_sind_deterministisch()
    {
        // Zwei Instanzen - auch auf zwei Rechnern - müssen dieselben Werte
        // liefern, sonst ist der Vergleich wertlos.
        var first = CreateClient();
        var second = CreateClient();

        foreach (var item in new ItemCatalog().All.Where(i => i.Kind != ItemValueKind.Date))
        {
            var a = await ReadAsync(first, item.BacnetObjectId);
            var b = await ReadAsync(second, item.BacnetObjectId);

            Assert.Equal(a, b);
        }
    }

    [Fact]
    public async Task Die_Werte_liegen_im_Wertebereich_des_Items()
    {
        var client = CreateClient();
        var catalog = new ItemCatalog();
        var seen = new HashSet<int>();

        foreach (var item in catalog.All.Where(i => i.Kind == ItemValueKind.Analog))
        {
            // Zwei Topics koennen auf dasselbe Objekt zeigen (Legacy: 864).
            // Der Wert gehoert dem Objekt - geprueft wird er beim ersten Item.
            if (!seen.Add(item.BacnetObjectId))
            {
                continue;
            }

            var value = await ReadAsync(client, item.BacnetObjectId);

            Assert.NotNull(value);
            Assert.InRange(value!.Value, item.Min ?? float.MinValue, item.Max ?? float.MaxValue);
        }
    }

    [Fact]
    public async Task Zwei_Topics_auf_demselben_Objekt_haben_denselben_Wert()
    {
        // Legacy-Eigenheit: Objekt 864 wird von zwei Topics benutzt
        // (KuehlraumsollAbstStandby und RestartDelay). Am echten Geraet
        // lesen beide denselben Rohwert.
        var client = CreateClient();
        var catalog = new ItemCatalog();

        var items = catalog.All.Where(i => i.BacnetObjectId == 864).ToList();
        Assert.Equal(2, items.Count);

        var first = await ReadAsync(client, 864);
        var second = await ReadAsync(client, 864);

        Assert.Equal(first, second);
        Assert.NotNull(first);
    }

    [Fact]
    public async Task Enum_Items_liefern_einen_der_bekannten_Werte()
    {
        var client = CreateClient();

        foreach (var item in new ItemCatalog().All.Where(i => i.Kind == ItemValueKind.Enum))
        {
            var value = await ReadAsync(client, item.BacnetObjectId);

            Assert.NotNull(value);
            Assert.Contains(item.EnumValues.Keys, key => key == (int)value!.Value);
        }
    }

    [Fact]
    public async Task Ein_unbekanntes_Objekt_liefert_keinen_Wert()
    {
        var client = CreateClient();

        var value = await ReadAsync(client, 999_999);

        Assert.Null(value);
    }

    [Fact]
    public async Task Die_Wertdatei_ueberschreibt_die_generierten_Werte()
    {
        var catalog = new ItemCatalog();
        var item = catalog.All.First(i => i.Kind == ItemValueKind.Analog);

        var path = Path.Combine(_directory, "values.json");
        await File.WriteAllTextAsync(path, ValuesJson(item.TopicPath, 12.5));

        var client = CreateClient(path);

        var value = await ReadAsync(client, item.BacnetObjectId);

        Assert.Equal(12.5f, value!.Value);
    }

    [Fact]
    public async Task Die_Wertdatei_kann_einen_Enum_Namen_nennen()
    {
        var catalog = new ItemCatalog();
        var item = catalog.All.First(i => i.Kind == ItemValueKind.Enum);
        var name = item.EnumValues.Values.First();

        var path = Path.Combine(_directory, "values.json");
        await File.WriteAllTextAsync(path, ValuesJson(item.TopicPath, name));

        var client = CreateClient(path);

        var value = await ReadAsync(client, item.BacnetObjectId);

        Assert.Equal((float)item.EnumValues.First(e => e.Value == name).Key, value!.Value);
    }

    [Fact]
    public async Task Eine_fehlende_Wertdatei_ist_kein_Fehler()
    {
        var client = CreateClient(Path.Combine(_directory, "gibt-es-nicht.json"));

        var item = new ItemCatalog().All.First(i => i.Kind == ItemValueKind.Analog);
        var value = await ReadAsync(client, item.BacnetObjectId);

        Assert.NotNull(value);
    }

    [Fact]
    public async Task Eine_kaputte_Wertdatei_ist_kein_Fehler()
    {
        var path = Path.Combine(_directory, "kaputt.json");
        await File.WriteAllTextAsync(path, "{ kein json");

        var client = CreateClient(path);

        var item = new ItemCatalog().All.First(i => i.Kind == ItemValueKind.Analog);
        var value = await ReadAsync(client, item.BacnetObjectId);

        Assert.NotNull(value);
    }

    [Fact]
    public async Task Ein_Schreibvorgang_bleibt_in_der_Simulation()
    {
        var client = CreateClient();
        var item = new ItemCatalog().All.First(i => i.Kind == ItemValueKind.Analog);

        await client.WritePropertyAsync(
            new BacnetAddress(BacnetAddressTypes.MSTP, 0, [1]),
            new BacnetObjectId(BacnetObjectTypes.OBJECT_ANALOG_VALUE, (uint)item.BacnetObjectId),
            BacnetPropertyIds.PROP_PRESENT_VALUE,
            [new BacnetValue(BacnetApplicationTags.BACNET_APPLICATION_TAG_REAL, 42f)]);

        var value = await ReadAsync(client, item.BacnetObjectId);

        Assert.Equal(42f, value!.Value);
    }

    [Fact]
    public void Date_Items_haben_keinen_Wert()
    {
        // Befund A20: der Wert eines Legacy-DateValue bleibt leer.
        var client = CreateClient();
        var catalog = new ItemCatalog();

        foreach (var item in catalog.All.Where(i => i.Kind == ItemValueKind.Date))
        {
            Assert.False(client.HasValue(item.BacnetObjectId));
        }
    }
}
