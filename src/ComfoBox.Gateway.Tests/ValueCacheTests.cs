using ComfoBox.Domain.Values;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für den Wert-Cache.
/// </summary>
public sealed class ValueCacheTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 31, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Ein_Frisch_Gelesener_Wert_Ist_Nicht_Veraltet()
    {
        var zeit = new TestTimeProvider(Start);
        var cache = new ValueCache(zeit, TimeSpan.FromMinutes(3));

        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));

        Assert.True(cache.TryGet("zone.set_point_normal", out var value));
        Assert.False(value!.IsStale);
        Assert.Equal(21.5f, ((AnalogValue)value.Value!).Value);
    }

    [Fact]
    public void Ein_Wert_Wird_Nach_Drei_Zyklen_Veraltet()
    {
        var zeit = new TestTimeProvider(Start);
        var cache = new ValueCache(zeit, TimeSpan.FromMinutes(3));

        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));

        zeit.Advance(TimeSpan.FromMinutes(2));
        Assert.False(cache.IsStale("zone.set_point_normal"));

        zeit.Advance(TimeSpan.FromMinutes(2));
        Assert.True(cache.IsStale("zone.set_point_normal"));
    }

    [Fact]
    public void Ein_Fehlgeschlagener_Read_Markiert_Den_Wert_Als_Veraltet()
    {
        var zeit = new TestTimeProvider(Start);
        var cache = new ValueCache(zeit, TimeSpan.FromMinutes(3));

        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));
        cache.MarkStale("zone.set_point_normal");

        Assert.True(cache.IsStale("zone.set_point_normal"));

        // Der Wert bleibt trotzdem sichtbar - die UI zeigt den letzten Stand.
        Assert.True(cache.TryGet("zone.set_point_normal", out var value));
        Assert.Equal(21.5f, ((AnalogValue)value!.Value!).Value);
    }

    [Fact]
    public void Ein_Unbekanntes_Item_Liefert_Keinen_Wert()
    {
        var cache = new ValueCache();

        Assert.False(cache.TryGet("gibt.es.nicht", out var value));
        Assert.Null(value);
    }

    [Fact]
    public async Task Paralleles_Lesen_Und_Schreiben_Verursacht_Keinen_Fehler()
    {
        var cache = new ValueCache();

        var schreiber = Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                cache.Update($"item-{i % 20}", new AnalogValue(i, null, null, null));
            }
        });

        var leser = Task.Run(() =>
        {
            for (var i = 0; i < 500; i++)
            {
                cache.TryGet($"item-{i % 20}", out _);
                _ = cache.Snapshot();
            }
        });

        await Task.WhenAll(schreiber, leser).WaitAsync(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public async Task Abonnenten_Werden_Ueber_Aenderungen_Informiert()
    {
        var cache = new ValueCache();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));

        var warten = Task.Run(async () =>
        {
            await foreach (var value in cache.SubscribeAsync(cts.Token))
            {
                return value;
            }

            return (CachedValue?)null;
        }, cts.Token);

        // Der Abonnent muss erst registriert sein.
        await Task.Delay(50);
        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));

        var empfangen = await warten;

        Assert.NotNull(empfangen);
        Assert.Equal("zone.set_point_normal", empfangen!.Id);
    }

    [Fact]
    public void Der_Snapshot_Enthaelt_Alle_Werte()
    {
        var cache = new ValueCache();

        cache.Update("a", new AnalogValue(1f, null, null, null));
        cache.Update("b", new AnalogValue(2f, null, null, null));

        Assert.Equal(2, cache.Snapshot().Length);
    }
}
