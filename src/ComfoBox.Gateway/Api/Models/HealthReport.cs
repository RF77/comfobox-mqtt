using ComfoBox.Gateway.Health;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Der Gesundheitszustand des Gateways.
/// </summary>
/// <remarks>
/// <c>state</c> ist <c>online</c>, <c>degraded</c> oder <c>offline</c> - die
/// Werte entsprechen dem MQTT-Vertrag (<c>ComfoBox/Status/Health</c>).
/// </remarks>
public sealed record HealthReport
{
    /// <summary>
    /// Der Gesamtzustand: <c>online</c>, <c>degraded</c> oder <c>offline</c>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Die Meldung zum Gesamtzustand.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Die Zustände der einzelnen Komponenten.
    /// </summary>
    public required HealthCheckDto[] Checks { get; init; }

    /// <summary>
    /// Die Version des Gateways.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Die Laufzeit in Sekunden.
    /// </summary>
    public required long UptimeSeconds { get; init; }

    /// <summary>
    /// Der Zeitpunkt des Starts.
    /// </summary>
    public required DateTimeOffset StartedAt { get; init; }
}

/// <summary>
/// Der Zustand einer Komponente (BACnet, MQTT, …).
/// </summary>
public sealed record HealthCheckDto
{
    /// <summary>
    /// Der Name der Komponente, z. B. <c>Bacnet</c>.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Der Zustand: <c>online</c>, <c>degraded</c> oder <c>offline</c>.
    /// </summary>
    public required string State { get; init; }

    /// <summary>
    /// Die letzte Meldung der Komponente.
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Der Zeitpunkt der letzten Änderung.
    /// </summary>
    public required DateTimeOffset ChangedAt { get; init; }

    /// <summary>
    /// Erzeugt die Beschreibung einer Komponente.
    /// </summary>
    /// <param name="component">Die Komponente.</param>
    /// <returns>Die Beschreibung.</returns>
    public static HealthCheckDto From(ComponentHealth component)
    {
        ArgumentNullException.ThrowIfNull(component);

        return new HealthCheckDto
        {
            Name = component.Name,
            State = component.State switch
            {
                HealthState.Online => "online",
                HealthState.Degraded => "degraded",
                _ => "offline",
            },
            Message = component.Message,
            ChangedAt = component.ChangedAt,
        };
    }
}
