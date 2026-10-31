using ComfoBox.Gateway.Logging;
using Serilog;
using Serilog.Events;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für den In-Memory-Ringpuffer der Log-Einträge.
/// </summary>
public sealed class InMemoryLogSinkTests
{
    private static Serilog.Core.Logger CreateLogger(InMemoryLogSink sink)
        => new LoggerConfiguration()
            .MinimumLevel.Verbose()
            .WriteTo.Sink(sink)
            .CreateLogger();

    [Fact]
    public void Capacity_Zero_DisablesBuffer()
    {
        var sink = new InMemoryLogSink(0, LogEventLevel.Information);
        var logger = CreateLogger(sink);

        logger.Information("Hallo");

        Assert.Equal(0, sink.Capacity);
        Assert.Empty(sink.Snapshot(10));
        Assert.Equal(0, sink.Count);
    }

    [Fact]
    public void Emit_OverwritesOldestEntry()
    {
        var sink = new InMemoryLogSink(3, LogEventLevel.Information);
        var logger = CreateLogger(sink);

        logger.Information("Erste");
        logger.Information("Zweite");
        logger.Information("Dritte");
        logger.Information("Vierte");

        var eintraege = sink.Snapshot(10);

        Assert.Equal(3, eintraege.Length);
        Assert.Equal("Vierte", eintraege[0].Message);
        Assert.Equal("Zweite", eintraege[2].Message);
    }

    [Fact]
    public void Die_Eintraege_Kommen_Neueste_Zuerst()
    {
        var sink = new InMemoryLogSink(10, LogEventLevel.Information);
        var logger = CreateLogger(sink);

        logger.Information("Erste");
        logger.Information("Zweite");

        var eintraege = sink.Snapshot(10);

        Assert.Equal("Zweite", eintraege[0].Message);
        Assert.Equal("Erste", eintraege[1].Message);
    }

    [Fact]
    public void Das_Limit_Des_Levels_Wird_Eingehalten()
    {
        var sink = new InMemoryLogSink(10, LogEventLevel.Warning);
        var logger = CreateLogger(sink);

        logger.Information("Info");
        logger.Warning("Warnung");

        var eintraege = sink.Snapshot(10);

        Assert.Single(eintraege);
        Assert.Equal("Warnung", eintraege[0].Message);
        Assert.Equal("Warning", eintraege[0].Level);
    }

    [Fact]
    public void Das_Limit_Des_Snapshots_Wird_Eingehalten()
    {
        var sink = new InMemoryLogSink(10, LogEventLevel.Information);
        var logger = CreateLogger(sink);

        for (var i = 0; i < 5; i++)
        {
            logger.Information("Eintrag {Index}", i);
        }

        Assert.Equal(2, sink.Snapshot(2).Length);
    }

    [Fact]
    public void Geheimnisse_Werden_Maskiert()
    {
        var sink = new InMemoryLogSink(10, LogEventLevel.Information);
        var logger = CreateLogger(sink);

        logger.Information("Verbinde mit {Host}", "broker:1883");
        logger.Information("Passwort ist {Password}", "geheim");

        var eintraege = sink.Snapshot(10);

        var mitPasswort = eintraege.Single(e => e.Properties.ContainsKey("Password"));
        Assert.Equal(SecretRedactor.Mask, mitPasswort.Properties["Password"]);

        var mitHost = eintraege.Single(e => e.Properties.ContainsKey("Host"));
        Assert.Equal("broker:1883", mitHost.Properties["Host"]);
    }

    [Fact]
    public void Passwoerter_In_URLs_Werden_Maskiert()
    {
        var maskiert = SecretRedactor.RedactText("Verbindung zu mqtt://benutzer:geheim@broker:1883 fehlgeschlagen");

        Assert.DoesNotContain("geheim", maskiert, StringComparison.Ordinal);
        Assert.Contains(SecretRedactor.Mask, maskiert, StringComparison.Ordinal);
    }

    [Fact]
    public void Die_Logger_Kategorie_Wird_Uebernommen()
    {
        var sink = new InMemoryLogSink(10, LogEventLevel.Information);
        var logger = CreateLogger(sink).ForContext("SourceContext", "ComfoBox.Bacnet.BacnetClient");

        logger.Information("Hallo");

        var eintrag = Assert.Single(sink.Snapshot(10));

        Assert.Equal("ComfoBox.Bacnet.BacnetClient", eintrag.Source);
    }

    [Fact]
    public void Clear_Leert_Den_Puffer()
    {
        var sink = new InMemoryLogSink(10, LogEventLevel.Information);
        var logger = CreateLogger(sink);

        logger.Information("Hallo");
        sink.Clear();

        Assert.Empty(sink.Snapshot(10));
        Assert.Equal(0, sink.Count);
    }

    [Fact]
    public void Ein_Fehler_Im_Sink_Wirft_Nie()
    {
        var sink = new InMemoryLogSink(10, LogEventLevel.Information);
        var logger = CreateLogger(sink);

        var ausnahme = Record.Exception(() => logger.Error(new InvalidOperationException("Test"), "Fehler {Wert}", 42));

        Assert.Null(ausnahme);
        Assert.Single(sink.Snapshot(10));
    }
}
