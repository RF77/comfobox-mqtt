namespace ComfoBox.Gateway.Health;

/// <summary>
/// Der Health-Reporter des Gateways.
/// </summary>
/// <remarks>
/// Die Aggregation passiert in <see cref="Aggregate"/>: der schlechteste
/// Zustand einer Komponente bestimmt den Zustand des Gateways.
/// </remarks>
public sealed class HealthReporter : IHealthReporter
{
    private readonly Dictionary<string, ComponentHealth> _components = new(StringComparer.Ordinal);
    private readonly object _sync = new();
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// Erzeugt den Reporter.
    /// </summary>
    /// <param name="timeProvider">Die Zeitquelle (für Tests austauschbar).</param>
    public HealthReporter(TimeProvider? timeProvider = null)
    {
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <inheritdoc />
    public HealthState Current { get; private set; } = HealthState.Online;

    /// <inheritdoc />
    public string Message { get; private set; } = "Der Gateway ist gestartet.";

    /// <inheritdoc />
    public IReadOnlyDictionary<string, ComponentHealth> Components
    {
        get
        {
            lock (_sync)
            {
                return new Dictionary<string, ComponentHealth>(_components, StringComparer.Ordinal);
            }
        }
    }

    /// <inheritdoc />
    public event EventHandler<HealthChangedEventArgs>? Changed;

    /// <inheritdoc />
    public void Report(string component, HealthState state, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(component);

        HealthState aggregated;
        string aggregatedMessage;

        lock (_sync)
        {
            _components[component] = new ComponentHealth(
                component,
                state,
                message ?? string.Empty,
                _timeProvider.GetUtcNow());

            (aggregated, aggregatedMessage) = Aggregate();
        }

        Apply(aggregated, aggregatedMessage);
    }

    /// <inheritdoc />
    public void ReportHealthy(string component, string message)
        => Report(component, HealthState.Online, message);

    /// <summary>
    /// Aggregiert die Komponenten zu einem Gesamtzustand.
    /// </summary>
    /// <returns>Der Zustand und die Meldung.</returns>
    private (HealthState State, string Message) Aggregate()
    {
        if (_components.Count == 0)
        {
            return (HealthState.Online, "Der Gateway ist gestartet.");
        }

        var offline = _components.Values.Where(c => c.State == HealthState.Offline).ToList();
        if (offline.Count > 0)
        {
            return (HealthState.Offline, Describe(offline));
        }

        var degraded = _components.Values.Where(c => c.State == HealthState.Degraded).ToList();
        if (degraded.Count > 0)
        {
            return (HealthState.Degraded, Describe(degraded));
        }

        return (HealthState.Online, "Alle Komponenten sind in Ordnung.");
    }

    /// <summary>
    /// Fasst die Komponenten-Meldungen zusammen.
    /// </summary>
    /// <param name="components">Die Komponenten.</param>
    /// <returns>Die Zusammenfassung.</returns>
    private static string Describe(IReadOnlyCollection<ComponentHealth> components)
        => string.Join("; ", components.Select(c => $"{c.Name}: {c.Message}"));

    /// <summary>
    /// Übernimmt einen neuen Gesamtzustand und meldet die Änderung.
    /// </summary>
    /// <param name="state">Der neue Zustand.</param>
    /// <param name="message">Die Meldung.</param>
    private void Apply(HealthState state, string message)
    {
        var changed = false;

        lock (_sync)
        {
            if (Current != state)
            {
                Current = state;
                changed = true;
            }

            Message = message;
        }

        if (changed)
        {
            Changed?.Invoke(this, new HealthChangedEventArgs(state, message));
        }
    }
}
