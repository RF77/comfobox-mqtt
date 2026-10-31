using ComfoBox.Gateway.Protection;

namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Tests für den persistenten Schreibzähler (Befund A3).
/// </summary>
public sealed class WriteCounterTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "comfobox-write-counter-" + Guid.NewGuid().ToString("N"));

    private readonly string _filePath;

    /// <summary>
    /// Erzeugt den Test und legt das temporäre Verzeichnis an.
    /// </summary>
    public WriteCounterTests()
    {
        Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, WriteCounter.FileName);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    [Fact]
    public void Ein_Neuer_Zaehler_Startet_Bei_Null()
    {
        var counter = new WriteCounter(_filePath);

        Assert.Equal(0, counter.Count);
        Assert.Equal(WriteCounter.DefaultLimit, counter.Limit);
        Assert.False(counter.IsLimitExceeded);
    }

    [Fact]
    public void Count_Persists_AcrossRestart()
    {
        var counter = new WriteCounter(_filePath);
        counter.TryAcquire();
        counter.TryAcquire();

        Assert.Equal(2, counter.Count);

        // Neustart: ein neuer Zähler mit derselben Datei.
        var neu = new WriteCounter(_filePath);

        Assert.Equal(2, neu.Count);
        Assert.True(File.Exists(_filePath));
    }

    [Fact]
    public void Die_Datei_Liegt_Im_Datenverzeichnis_Und_Wird_Atomar_Geschrieben()
    {
        var counter = new WriteCounter(_filePath);
        counter.TryAcquire();

        Assert.True(File.Exists(_filePath));
        Assert.Equal(_filePath, Path.GetFullPath(_filePath));

        // Keine temporäre Datei darf übrig bleiben.
        Assert.False(File.Exists(_filePath + ".tmp"));
    }

    [Fact]
    public void Bei_Ueberschreitung_Wird_Der_Schreibvorgang_Abgelehnt()
    {
        var counter = new WriteCounter(_filePath, limit: 3);

        Assert.True(counter.TryAcquire());
        Assert.True(counter.TryAcquire());
        Assert.True(counter.TryAcquire());

        Assert.False(counter.TryAcquire());
        Assert.True(counter.IsLimitExceeded);
        Assert.Equal(3, counter.Count);
    }

    [Fact]
    public void Ein_Fehlgeschlagener_Schreibvorgang_Zaehlt_Nicht()
    {
        // Befund A22: der Zähler wird nur bei erfolgreichen Schreibvorgängen erhöht.
        var counter = new WriteCounter(_filePath, limit: 2);

        Assert.True(counter.TryAcquire());
        counter.Release();

        Assert.Equal(0, counter.Count);
        Assert.False(counter.IsLimitExceeded);
    }

    [Fact]
    public void Alte_Eintraege_Verlassen_Das_Fenster()
    {
        var zeit = new TestTimeProvider(new DateTimeOffset(2026, 10, 31, 8, 0, 0, TimeSpan.Zero));
        var counter = new WriteCounter(_filePath, limit: 10, zeit);

        counter.TryAcquire();
        counter.TryAcquire();
        Assert.Equal(2, counter.Count);

        // 25 Stunden später sind beide Einträge aus dem 24-h-Fenster gefallen.
        zeit.Advance(TimeSpan.FromHours(25));

        Assert.Equal(0, counter.Count);
        Assert.False(counter.IsLimitExceeded);
    }

    [Fact]
    public void Der_Zaehler_Ueberlebt_Den_Neustart_Auch_Mit_Altem_Fenster()
    {
        var zeit = new TestTimeProvider(new DateTimeOffset(2026, 10, 31, 8, 0, 0, TimeSpan.Zero));
        var counter = new WriteCounter(_filePath, limit: 10, zeit);
        counter.TryAcquire();

        zeit.Advance(TimeSpan.FromHours(25));

        var neu = new WriteCounter(_filePath, limit: 10, zeit);

        Assert.Equal(0, neu.Count);
    }

    [Fact]
    public void Ohne_Limit_Wird_Nichts_Abgelehnt()
    {
        var counter = new WriteCounter(_filePath, limit: 0);

        for (var i = 0; i < 100; i++)
        {
            Assert.True(counter.TryAcquire());
        }

        Assert.Equal(100, counter.Count);
        Assert.False(counter.IsLimitExceeded);
    }

    [Fact]
    public void Die_Kennzahlen_Zeigen_Das_Fenster()
    {
        var start = new DateTimeOffset(2026, 10, 31, 8, 0, 0, TimeSpan.Zero);
        var zeit = new TestTimeProvider(start);
        var counter = new WriteCounter(_filePath, limit: 10, zeit);

        counter.TryAcquire();
        zeit.Advance(TimeSpan.FromMinutes(5));
        counter.TryAcquire();

        var stats = counter.GetStats();

        Assert.Equal(2, stats.Count);
        Assert.Equal(10, stats.Limit);
        Assert.Equal(start, stats.Oldest);
        Assert.Equal(start.AddMinutes(5), stats.Newest);
    }

    [Fact]
    public void Eine_Kaputte_Datei_Verhindert_Den_Start_Nicht()
    {
        File.WriteAllText(_filePath, "{ kaputt");

        var counter = new WriteCounter(_filePath);

        Assert.Equal(0, counter.Count);
    }

    [Fact]
    public void Reset_Leert_Den_Zaehler_Auch_Dauerhaft()
    {
        var counter = new WriteCounter(_filePath);
        counter.TryAcquire();
        counter.Reset();

        Assert.Equal(0, counter.Count);
        Assert.Equal(0, new WriteCounter(_filePath).Count);
    }
}
