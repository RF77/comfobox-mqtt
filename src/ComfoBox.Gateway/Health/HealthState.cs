namespace ComfoBox.Gateway.Health;

/// <summary>
/// Der Health-Status des Gateways.
/// </summary>
public enum HealthState
{
    /// <summary>
    /// Alles in Ordnung: BACnet und MQTT sind verbunden, das Polling läuft.
    /// </summary>
    Online,

    /// <summary>
    /// Mindestens eine Komponente ist defekt - die Werte sind <c>stale</c>.
    /// </summary>
    Degraded,

    /// <summary>
    /// Der Gateway ist nicht erreichbar (MQTT getrennt oder Prozess beendet).
    /// </summary>
    Offline,
}

/// <summary>
/// Der Zustand einer Komponente.
/// </summary>
/// <param name="Name">Der Name der Komponente, z. B. <c>Mqtt</c>.</param>
/// <param name="State">Der Zustand.</param>
/// <param name="Message">Die letzte Meldung.</param>
/// <param name="ChangedAt">Der Zeitpunkt der letzten Änderung.</param>
public sealed record ComponentHealth(
    string Name,
    HealthState State,
    string Message,
    DateTimeOffset ChangedAt);

/// <summary>
/// Das Ereignis einer Health-Änderung.
/// </summary>
public sealed class HealthChangedEventArgs : EventArgs
{
    /// <summary>
    /// Erzeugt die Ereignis-Argumente.
    /// </summary>
    /// <param name="state">Der neue Gesamt-Zustand.</param>
    /// <param name="message">Die Meldung zum neuen Zustand.</param>
    public HealthChangedEventArgs(HealthState state, string message)
    {
        State = state;
        Message = message;
    }

    /// <summary>
    /// Der neue Gesamt-Zustand.
    /// </summary>
    public HealthState State { get; }

    /// <summary>
    /// Die Meldung zum neuen Zustand.
    /// </summary>
    public string Message { get; }
}

/// <summary>
/// Der Health-Status des Gateways.
/// </summary>
/// <remarks>
/// <para>
/// Der Status wird aus den **Komponenten** aggregiert: ist eine Komponente
/// <c>offline</c>, ist der Gateway <c>offline</c>; ist eine <c>degraded</c>,
/// ist der Gateway <c>degraded</c>.
/// </para>
/// <para>
/// Benutzerfehler (ein abgelehnter Schreibvorgang) verschlechern den Status
/// **nicht** - der Gateway läuft ja.
/// </para>
/// </remarks>
public interface IHealthReporter
{
    /// <summary>
    /// Der aktuelle Gesamt-Zustand.
    /// </summary>
    HealthState Current { get; }

    /// <summary>
    /// Die Meldung zum aktuellen Zustand.
    /// </summary>
    string Message { get; }

    /// <summary>
    /// Die Zustände der einzelnen Komponenten.
    /// </summary>
    IReadOnlyDictionary<string, ComponentHealth> Components { get; }

    /// <summary>
    /// Wird ausgelöst, wenn sich der Gesamt-Zustand ändert.
    /// </summary>
    event EventHandler<HealthChangedEventArgs>? Changed;

    /// <summary>
    /// Meldet den Zustand einer Komponente.
    /// </summary>
    /// <param name="component">Die Komponente, z. B. <c>Mqtt</c>.</param>
    /// <param name="state">Der Zustand.</param>
    /// <param name="message">Die Meldung.</param>
    void Report(string component, HealthState state, string message);

    /// <summary>
    /// Meldet, dass eine Komponente wieder in Ordnung ist.
    /// </summary>
    /// <param name="component">Die Komponente, z. B. <c>Mqtt</c>.</param>
    /// <param name="message">Die Meldung.</param>
    void ReportHealthy(string component, string message);
}
