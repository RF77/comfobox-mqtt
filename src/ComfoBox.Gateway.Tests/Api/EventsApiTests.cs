using System.Net;
using System.Text;
using ComfoBox.Domain.Values;
using Microsoft.Extensions.DependencyInjection;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests des SSE-Streams (Aufgabe 4.3).
/// </summary>
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

        // Ein Wertänderung erzeugt ein "value"-Ereignis.
        cache.Update("zone.set_point_normal", new AnalogValue(22.5f, 5f, 30f, "°C"));

        var change = await ReadEventAsync(stream);
        Assert.StartsWith("event: value", change, StringComparison.Ordinal);
        Assert.Contains("22.5", change, StringComparison.Ordinal);
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

        Assert.StartsWith("event: value", change, StringComparison.Ordinal);
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
    private static async Task<string> ReadEventAsync(
        Stream stream,
        CancellationToken cancellationToken = default)
    {
        var builder = new StringBuilder();
        var buffer = new byte[1];

        while (builder.ToString().Length < 4096)
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
}
