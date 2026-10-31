namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Der vollständige Laufzeitstatus des Gateways.
/// </summary>
/// <remarks>
/// <b>Die Feldnamen sind verbindlich</b> - die Home-Assistant-Integration liest
/// sie unverändert. Sie dürfen nicht umbenannt werden.
/// </remarks>
public sealed record StatusReport
{
    /// <summary>
    /// Die Geräte-Id, z. B. <c>comfobox1</c>.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Die Version des Gateways.
    /// </summary>
    public required string Version { get; init; }

    /// <summary>
    /// Die Laufzeit in Sekunden.
    /// </summary>
    public required long UptimeSeconds { get; init; }

    /// <summary>
    /// Der Zustand der BACnet-Verbindung.
    /// </summary>
    public required BacnetStatusDto Bacnet { get; init; }

    /// <summary>
    /// Der Zustand der MQTT-Verbindung.
    /// </summary>
    public required MqttStatusDto Mqtt { get; init; }

    /// <summary>
    /// Der Zustand des 24-h-Schreibzählers.
    /// </summary>
    public required WriteStatusDto Writes { get; init; }
}

/// <summary>
/// Der Zustand der BACnet-Verbindung.
/// </summary>
public sealed record BacnetStatusDto
{
    /// <summary>
    /// Ist die ComfoBox erreichbar?
    /// </summary>
    public required bool Connected { get; init; }

    /// <summary>
    /// Der serielle Port, z. B. <c>/dev/ttyUSB0</c>.
    /// </summary>
    public required string Port { get; init; }

    /// <summary>
    /// Der letzte Fehler (oder <c>null</c>).
    /// </summary>
    public string? LastError { get; init; }
}

/// <summary>
/// Der Zustand der MQTT-Verbindung.
/// </summary>
public sealed record MqttStatusDto
{
    /// <summary>
    /// Ist mindestens ein Broker verbunden?
    /// </summary>
    public required bool Connected { get; init; }

    /// <summary>
    /// Die Broker (durch Komma getrennt), z. B. <c>core-mosquitto</c>.
    /// </summary>
    public required string Broker { get; init; }
}

/// <summary>
/// Der Zustand des 24-h-Schreibzählers.
/// </summary>
public sealed record WriteStatusDto
{
    /// <summary>
    /// Die Anzahl der Schreibvorgänge im Fenster.
    /// </summary>
    public required int Count { get; init; }

    /// <summary>
    /// Das Limit (<c>0</c> = kein Limit).
    /// </summary>
    public required int Limit { get; init; }

    /// <summary>
    /// Der Zeitpunkt, zu dem das Fenster zurückgesetzt wird (oder <c>null</c>).
    /// </summary>
    public DateTimeOffset? ResetsAt { get; init; }
}
