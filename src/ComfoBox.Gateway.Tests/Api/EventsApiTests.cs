using System.Diagnostics;
using System.Net;
using System.Text;
using ComfoBox.Domain.Values;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests des SSE-Streams (Aufgabe 4.3).
/// </summary>
/// <remarks>
/// Der Stream schickt die Werte **gesammelt**: ein Poll-Zyklus liest die Items
/// nacheinander über den Bus, die Änderungen gehören aber sachlich zusammen.
/// Ein Ereignis je Wert hieße, dass der Empfänger einen Zyklus in Dutzenden
/// Runden verarbeitet.
/// </remarks>
public sealed class EventsApiTests
{
    /// <summary>
    /// Der Stream liefert zuerst den Snapshot, danach die Änderungen.
    /// </summary>
    [Fact]
    public async Task Der_Stream_Liefert_Snapshot_Und_Werte()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var cache = fixture.Services.GetRequiredService<IValueCache>();
        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));

        using var response = await fixture.Client.GetAsync(
            "/api/events",
            HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();

        var snapshot = await ReadEventAsync(stream);
        Assert.StartsWith("event: snapshot", snapshot, StringComparison.Ordinal);
        Assert.Contains("zone.set_point_normal", snapshot, StringComparison.Ordinal);
        Assert.Contains("21.5", snapshot, StringComparison.Ordinal);

        // Eine Wertänderung erzeugt ein "values"-Ereignis (Array).
        cache.Update("zone.set_point_normal", new AnalogValue(22.5f, 5f, 30f, "°C"));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var change = await ReadEventAsync(stream, timeout.Token);

        Assert.StartsWith("event: values", change, StringComparison.Ordinal);
        Assert.Contains("22.5", change, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(change, "\"id\":"));
    }

    /// <summary>
    /// Die Werte eines Auslesezyklus kommen als **ein** Ereignis.
    /// </summary>
    /// <remarks>
    /// <b>Messung (2026-11-19):</b> ein Zyklus erzeugte 35 Ereignisse, der
    /// Sekundärzyklus 160. Die Home-Assistant-Integration benachrichtigt bei
    /// **jeder** Nachricht alle Entitäten – das waren bis zu 25 000
    /// Zustands-Aktualisierungen je Sekundärzyklus.
    /// </remarks>
    [Fact]
    public async Task Ein_Auslesezyklus_Wird_Ein_Ereignis()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        using var response = await fixture.Client.GetAsync(
            "/api/events",
            HttpCompletionOption.ResponseHeadersRead);

        await using var stream = await response.Content.ReadAsStreamAsync();

        var snapshot = await ReadEventAsync(stream);
        Assert.StartsWith("event: snapshot", snapshot, StringComparison.Ordinal);

        var cache = fixture.Services.GetRequiredService<IValueCache>();
        for (var index = 0; index < 35; index++)
        {
            cache.Update($"item-{index}", new AnalogValue(index, null, null, null));
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var change = await ReadEventAsync(stream, timeout.Token);

        Assert.StartsWith("event: values", change, StringComparison.Ordinal);
        Assert.Equal(35, CountOccurrences(change, "\"id\":"));
    }

    /// <summary>
    /// Ein einzelner Wert kommt innerhalb der Obergrenze an.
    /// </summary>
    /// <remarks>
    /// Das Sammelfenster darf die Latenz nicht offen lassen: ein einzelner
    /// Schreibvorgang soll in Home Assistant „sofort“ sichtbar sein.
    /// </remarks>
    [Fact]
    public async Task Ein_Einzelner_Wert_Kommt_Innerhalb_Der_Obergrenze()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Api:SseBatchQuietMs"] = "100",
                ["Api:SseBatchMaxDelayMs"] = "500",
            });

        using var response = await fixture.Client.GetAsync(
            "/api/events",
            HttpCompletionOption.ResponseHeadersRead);

        await using var stream = await response.Content.ReadAsStreamAsync();

        await ReadEventAsync(stream);

        var cache = fixture.Services.GetRequiredService<IValueCache>();
        var stopwatch = Stopwatch.StartNew();

        cache.Update("zone.set_point_normal", new AnalogValue(23.5f, 5f, 30f, "°C"));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var change = await ReadEventAsync(stream, timeout.Token);

        stopwatch.Stop();

        Assert.StartsWith("event: values", change, StringComparison.Ordinal);
        Assert.Contains("23.5", change, StringComparison.Ordinal);
        Assert.True(
            stopwatch.Elapsed < TimeSpan.FromSeconds(2),
            $"Der Wert kam erst nach {stopwatch.ElapsedMilliseconds} ms.");
    }

    /// <summary>
    /// Zwei Änderungen derselben Id werden zu einem Eintrag.
    /// </summary>
    /// <remarks>
    /// Der Client braucht nur den letzten Stand; ein Zwischenwert wäre eine
    /// Zustandsänderung, die nie jemand sehen soll.
    /// </remarks>
    [Fact]
    public async Task Der_Letzte_Stand_Einer_Id_Gewinnt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        using var response = await fixture.Client.GetAsync(
            "/api/events",
            HttpCompletionOption.ResponseHeadersRead);

        await using var stream = await response.Content.ReadAsStreamAsync();

        await ReadEventAsync(stream);

        var cache = fixture.Services.GetRequiredService<IValueCache>();
        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));
        cache.Update("zone.set_point_normal", new AnalogValue(22.5f, 5f, 30f, "°C"));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var change = await ReadEventAsync(stream, timeout.Token);

        Assert.StartsWith("event: values", change, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(change, "\"id\":"));
        Assert.Contains("22.5", change, StringComparison.Ordinal);
        Assert.DoesNotContain("21.5", change, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ohne Fenster geht wieder jedes Ereignis einzeln hinaus.
    /// </summary>
    /// <remarks>
    /// Die beiden Optionen auf <c>0</c> stellen das Verhalten vor dem
    /// Sammelfenster her - der Notausgang, falls ein Client damit nicht
    /// zurechtkommt.
    /// </remarks>
    [Fact]
    public async Task Ohne_Fenster_Geht_Jeder_Wert_Einzeln_Hinaus()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Api:SseBatchQuietMs"] = "0",
                ["Api:SseBatchMaxDelayMs"] = "0",
            });

        using var response = await fixture.Client.GetAsync(
            "/api/events",
            HttpCompletionOption.ResponseHeadersRead);

        await using var stream = await response.Content.ReadAsStreamAsync();

        await ReadEventAsync(stream);

        var cache = fixture.Services.GetRequiredService<IValueCache>();
        cache.Update("item-a", new AnalogValue(1f, null, null, null));
        cache.Update("item-b", new AnalogValue(2f, null, null, null));

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));

        var first = await ReadEventAsync(stream, timeout.Token);
        var second = await ReadEventAsync(stream, timeout.Token);

        Assert.StartsWith("event: values", first, StringComparison.Ordinal);
        Assert.StartsWith("event: values", second, StringComparison.Ordinal);
        Assert.Equal(1, CountOccurrences(first, "\"id\":"));
        Assert.Equal(1, CountOccurrences(second, "\"id\":"));
    }

    /// <summary>
    /// Der Abbruch des Clients beendet den Stream sauber (kein Leck).
    /// </summary>
    [Fact]
    public async Task Der_Abbruch_Beendet_Den_Stream_Sauber()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var response = await fixture.Client.GetAsync(
            "/api/events",
            HttpCompletionOption.ResponseHeadersRead);

        var stream = await response.Content.ReadAsStreamAsync();

        // Der Stream wird gelesen und dann weggeworfen (Abbruch).
        var buffer = new byte[64];
        await stream.ReadExactlyAsync(buffer.AsMemory(0, 1), CancellationToken.None);

        response.Dispose();
        await Task.Delay(100);

        // Der Gateway läuft noch - der Abbruch hat nichts zerschossen.
        var health = await fixture.Client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, health.StatusCode);
    }

    /// <summary>
    /// Ein fehlgeschlagener Read wird als Ereignis gemeldet.
    /// </summary>
    /// <remarks>
    /// Ohne dieses Ereignis bliebe der Wechsel auf „veraltet“ für die
    /// Push-Clients unsichtbar, bis der nächste Wert eintrifft - bei einem
    /// stündlich gelesenen Item also bis zu eine Stunde lang.
    /// </remarks>
    [Fact]
    public async Task Eine_Fehlgeschlagene_Lesung_Erzeugt_Ein_Ereignis()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var cache = fixture.Services.GetRequiredService<IValueCache>();
        cache.Update("zone.set_point_normal", new AnalogValue(21.5f, 5f, 30f, "°C"));

        using var response = await fixture.Client.GetAsync(
            "/api/events",
            HttpCompletionOption.ResponseHeadersRead);

        await using var stream = await response.Content.ReadAsStreamAsync();

        var snapshot = await ReadEventAsync(stream);
        Assert.StartsWith("event: snapshot", snapshot, StringComparison.Ordinal);

        cache.MarkStale("zone.set_point_normal");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var change = await ReadEventAsync(stream, timeout.Token);

        Assert.StartsWith("event: values", change, StringComparison.Ordinal);
        Assert.Contains("\"isStale\":true", change, StringComparison.Ordinal);
        Assert.Contains("21.5", change, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein stiller Stream schickt Lebenszeichen.
    /// </summary>
    /// <remarks>
    /// Ohne sie fällt die Verbindung hinter einem Proxy unbemerkt weg: der
    /// Client merkt es erst, wenn er wieder Werte erwartet.
    /// </remarks>
    [Fact]
    public async Task Ein_Stiller_Stream_Schickt_Lebenszeichen()
    {
        await using var fixture = await GatewayApiFixture.StartAsync(
            new Dictionary<string, string?>(StringComparer.Ordinal)
            {
                ["Api:SseHeartbeatSeconds"] = "1",
            });

        using var response = await fixture.Client.GetAsync(
            "/api/events",
            HttpCompletionOption.ResponseHeadersRead);

        await using var stream = await response.Content.ReadAsStreamAsync();

        var snapshot = await ReadEventAsync(stream);
        Assert.StartsWith("event: snapshot", snapshot, StringComparison.Ordinal);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var ping = await ReadEventAsync(stream, timeout.Token);

        Assert.StartsWith(": ping", ping, StringComparison.Ordinal);
    }

    /// <summary>
    /// Liest das nächste SSE-Ereignis.
    /// </summary>
    /// <remarks>
    /// Ein Sammel-Ereignis mit 160 Werten ist deutlich größer als der
    /// Zeilenpuffer eines Terminals - die Obergrenze ist deshalb bewusst
    /// großzügig.
    /// </remarks>
    private static async Task<string> ReadEventAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        const int MaxEventLength = 1_048_576;

        var builder = new StringBuilder();
        var buffer = new byte[1];

        while (builder.Length < MaxEventLength)
        {
            try
            {
                await stream.ReadExactlyAsync(buffer.AsMemory(0, 1), cancellationToken);
            }
            catch (EndOfStreamException)
            {
                break;
            }

            builder.Append((char)buffer[0]);

            if (builder.Length >= 2
                && builder[^2] == '\n'
                && builder[^1] == '\n')
            {
                break;
            }
        }

        return builder.ToString();
    }

    /// <summary>
    /// Zählt das Vorkommen einer Zeichenfolge.
    /// </summary>
    private static int CountOccurrences(string text, string needle)
    {
        var count = 0;
        var index = 0;

        while ((index = text.IndexOf(needle, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += needle.Length;
        }

        return count;
    }
}
