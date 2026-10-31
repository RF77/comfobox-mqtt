using System.Net;
using System.Text;
using ComfoBox.Gateway.Logging;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using Serilog.Events;

namespace ComfoBox.Gateway.Tests.Api;

/// <summary>
/// Die Tests der Log-Endpunkte (Aufgabe 4.9).
/// </summary>
public sealed class LogApiTests
{
    /// <summary>
    /// Die Log-Einträge werden als Array geliefert.
    /// </summary>
    [Fact]
    public async Task Die_Log_Eintraege_Werden_Als_Array_Geliefert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        Write(fixture, "ComfoBox.Test.Bacnet", LogEventLevel.Warning, "Lesefehler bei Zone/SetPointNormal");

        var response = await fixture.Client.GetAsync("/api/diagnostics/logs");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = await GatewayApiFixture.ReadJsonAsync(response);
        Assert.True(entries.GetArrayLength() >= 1);

        var entry = entries.EnumerateArray().First(e =>
            e.GetProperty("message").GetString()!.Contains("Lesefehler", StringComparison.Ordinal));

        Assert.Equal("Warning", entry.GetProperty("level").GetString());
        Assert.Equal("ComfoBox.Test.Bacnet", entry.GetProperty("source").GetString());
        Assert.True(entry.TryGetProperty("timestamp", out _));
        Assert.True(entry.TryGetProperty("properties", out _));
    }

    /// <summary>
    /// Die Filter arbeiten **kombiniert**.
    /// </summary>
    [Fact]
    public async Task Die_Filter_Arbeiten_Kombiniert()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        Write(fixture, "ComfoBox.Test.Bacnet", LogEventLevel.Warning, "Lesefehler beim Poll");
        Write(fixture, "ComfoBox.Test.Mqtt", LogEventLevel.Error, "Broker nicht erreichbar");
        Write(fixture, "ComfoBox.Test.Bacnet", LogEventLevel.Information, "Poll gestartet");

        var response = await fixture.Client.GetAsync(
            "/api/diagnostics/logs?level=Warning&source=Bacnet&search=Lesefehler");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var entries = await GatewayApiFixture.ReadJsonAsync(response);
        var matches = entries.EnumerateArray()
            .Where(e => e.GetProperty("message").GetString()!.Contains("Lesefehler", StringComparison.Ordinal))
            .ToArray();

        Assert.Single(matches);
        Assert.Equal("ComfoBox.Test.Bacnet", matches[0].GetProperty("source").GetString());
    }

    /// <summary>
    /// Das Limit ist begrenzt.
    /// </summary>
    [Fact]
    public async Task Das_Limit_Ist_Begrenzt()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        for (var i = 0; i < 20; i++)
        {
            Write(fixture, "ComfoBox.Test", LogEventLevel.Information, $"Eintrag {i}");
        }

        var response = await fixture.Client.GetAsync("/api/diagnostics/logs?limit=5");
        var entries = await GatewayApiFixture.ReadJsonAsync(response);

        Assert.Equal(5, entries.GetArrayLength());
    }

    /// <summary>
    /// Ein ungültiger Parameter ergibt 400.
    /// </summary>
    [Fact]
    public async Task Ein_Ungueltiger_Parameter_Ergibt_400()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var limit = await fixture.Client.GetAsync("/api/diagnostics/logs?limit=abc");
        Assert.Equal(HttpStatusCode.BadRequest, limit.StatusCode);

        var order = await fixture.Client.GetAsync("/api/diagnostics/logs?order=seitwärts");
        Assert.Equal(HttpStatusCode.BadRequest, order.StatusCode);
    }

    /// <summary>
    /// Die Reihenfolge ist umstellbar.
    /// </summary>
    [Fact]
    public async Task Die_Reihenfolge_Ist_Umstellbar()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        Write(fixture, "ComfoBox.Test", LogEventLevel.Information, "Erster");
        Write(fixture, "ComfoBox.Test", LogEventLevel.Information, "Zweiter");

        var desc = await GatewayApiFixture.ReadJsonAsync(await fixture.Client.GetAsync("/api/diagnostics/logs?order=desc"));
        var asc = await GatewayApiFixture.ReadJsonAsync(await fixture.Client.GetAsync("/api/diagnostics/logs?order=asc"));

        var firstDesc = desc.EnumerateArray().First().GetProperty("message").GetString();
        var firstAsc = asc.EnumerateArray().First().GetProperty("message").GetString();

        Assert.NotEqual(firstDesc, firstAsc);
    }

    /// <summary>
    /// Der Puffer lässt sich leeren.
    /// </summary>
    [Fact]
    public async Task Der_Puffer_Laesst_Sich_Leeren()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        Write(fixture, "ComfoBox.Test", LogEventLevel.Information, "Eintrag");

        var delete = await fixture.Client.DeleteAsync("/api/diagnostics/logs");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var sink = fixture.Services.GetRequiredService<InMemoryLogSink>();
        Assert.Empty(sink.Snapshot(sink.Capacity));
    }

    /// <summary>
    /// Der SSE-Tail liefert den Initial-Snapshot und danach neue Einträge.
    /// </summary>
    [Fact]
    public async Task Der_Sse_Tail_Liefert_Snapshot_Und_Neue_Eintraege()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        Write(fixture, "ComfoBox.Test", LogEventLevel.Information, "Vorher");

        using var response = await fixture.Client.GetAsync(
            "/api/diagnostics/logs/stream",
            HttpCompletionOption.ResponseHeadersRead);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/event-stream", response.Content.Headers.ContentType?.MediaType);

        await using var stream = await response.Content.ReadAsStreamAsync();

        var snapshot = await ReadEventAsync(stream);
        Assert.StartsWith("event: log", snapshot, StringComparison.Ordinal);
        Assert.Contains("Vorher", snapshot, StringComparison.Ordinal);

        Write(fixture, "ComfoBox.Test", LogEventLevel.Information, "Nachher");

        var live = await ReadEventAsync(stream);
        Assert.StartsWith("event: log", live, StringComparison.Ordinal);
        Assert.Contains("Nachher", live, StringComparison.Ordinal);
    }

    /// <summary>
    /// Es sind höchstens fünf Log-Streams gleichzeitig möglich.
    /// </summary>
    [Fact]
    public async Task Es_Sind_Hoechstens_Fuenf_Log_Streams_Moeglich()
    {
        await using var fixture = await GatewayApiFixture.StartAsync();

        var responses = new List<HttpResponseMessage>();

        try
        {
            for (var i = 0; i < LogStreamBroker.MaxStreams; i++)
            {
                var response = await fixture.Client.GetAsync(
                    "/api/diagnostics/logs/stream",
                    HttpCompletionOption.ResponseHeadersRead);

                responses.Add(response);
                Assert.Equal(HttpStatusCode.OK, response.StatusCode);

                // Der Stream muss aktiv sein, bevor der nächste geöffnet wird.
                await Task.Delay(50);
            }

            var tooMany = await fixture.Client.GetAsync(
                "/api/diagnostics/logs/stream",
                HttpCompletionOption.ResponseHeadersRead);

            Assert.Equal(HttpStatusCode.TooManyRequests, tooMany.StatusCode);
        }
        finally
        {
            foreach (var response in responses)
            {
                response.Dispose();
            }
        }
    }

    /// <summary>
    /// Schreibt einen Log-Eintrag direkt in den Ringpuffer.
    /// </summary>
    /// <remarks>
    /// Der Test schreibt direkt über Serilog in den Puffer - so muss der
    /// Testhost keinen Log-Provider konfigurieren.
    /// </remarks>
    private static void Write(GatewayApiFixture fixture, string source, LogEventLevel level, string message)
    {
        var sink = fixture.Services.GetRequiredService<InMemoryLogSink>();

        var logger = new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger()
            .ForContext("SourceContext", source);

        logger.Write(level, "{Message}", message);
    }

    /// <summary>
    /// Liest das nächste SSE-Ereignis.
    /// </summary>
    private static async Task<string> ReadEventAsync(Stream stream)
    {
        var builder = new StringBuilder();
        var buffer = new byte[1];

        while (builder.Length < 4096)
        {
            try
            {
                await stream.ReadExactlyAsync(buffer.AsMemory(0, 1), CancellationToken.None);
            }
            catch (EndOfStreamException)
            {
                break;
            }

            builder.Append((char)buffer[0]);

            if (builder.Length >= 2 && builder[^2] == '\n' && builder[^1] == '\n')
            {
                break;
            }
        }

        return builder.ToString();
    }
}
