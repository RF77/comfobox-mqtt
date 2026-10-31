namespace ComfoBox.Gateway.Tests;

/// <summary>
/// Eine Zeitquelle, die man vorrücken kann.
/// </summary>
/// <remarks>
/// Damit lassen sich Zeitfenster (Schreibzähler, Wert-Ablauf) ohne Wartezeit
/// testen.
/// </remarks>
internal sealed class TestTimeProvider : TimeProvider
{
    private DateTimeOffset _now;

    /// <summary>
    /// Erzeugt die Zeitquelle.
    /// </summary>
    /// <param name="start">Der Startzeitpunkt.</param>
    public TestTimeProvider(DateTimeOffset start)
    {
        _now = start;
    }

    /// <summary>
    /// Der aktuelle Zeitpunkt.
    /// </summary>
    public DateTimeOffset Now => _now;

    /// <inheritdoc />
    public override DateTimeOffset GetUtcNow() => _now;

    /// <summary>
    /// Lässt die Zeit weiterlaufen.
    /// </summary>
    /// <param name="delta">Die Zeitspanne.</param>
    public void Advance(TimeSpan delta) => _now += delta;
}
