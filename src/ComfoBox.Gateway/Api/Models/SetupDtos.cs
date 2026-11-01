namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Der Zustand der Ersteinrichtung.
/// </summary>
public sealed record SetupStateDto
{
    /// <summary>
    /// Ist die Ersteinrichtung abgeschlossen?
    /// </summary>
    public required bool SetupCompleted { get; init; }

    /// <summary>
    /// Die Schritte des Assistenten in Reihenfolge.
    /// </summary>
    public required string[] Steps { get; init; }

    /// <summary>
    /// Die Version des Setup-Formats.
    /// </summary>
    public required int Version { get; init; }

    /// <summary>
    /// Die aktuell gespeicherten Werte (für den wiederholbaren Assistenten).
    /// </summary>
    /// <remarks>
    /// Nach der Ersteinrichtung ist der Assistent erneut unter <c>/setup</c>
    /// erreichbar. Damit der Benutzer nicht alles neu eingeben muss, bekommt
    /// die Oberfläche die aktuellen Werte geliefert.
    /// </remarks>
    public SetupCurrentValuesDto? Current { get; init; }
}

/// <summary>
/// Die aktuell gespeicherten Werte der Einrichtung.
/// </summary>
/// <remarks>
/// Es werden **niemals** Passwörter geliefert - weder das Passwort des
/// Administrators noch das des Brokers.
/// </remarks>
public sealed record SetupCurrentValuesDto
{
    /// <summary>
    /// Der konfigurierte serielle Port (oder <c>null</c>).
    /// </summary>
    public string? Port { get; init; }

    /// <summary>
    /// Die konfigurierte Baudrate.
    /// </summary>
    public int Baudrate { get; init; }

    /// <summary>
    /// Der Host des Brokers (oder <c>null</c>).
    /// </summary>
    public string? BrokerHost { get; init; }

    /// <summary>
    /// Der Port des Brokers.
    /// </summary>
    public int BrokerPort { get; init; }

    /// <summary>
    /// Der Benutzername am Broker (oder <c>null</c>).
    /// </summary>
    public string? BrokerUsername { get; init; }

    /// <summary>
    /// Der Benutzername des Administrators (oder <c>null</c>).
    /// </summary>
    public string? AdminUsername { get; init; }

    /// <summary>
    /// Sollen alle seriellen Ports durchgereicht werden?
    /// </summary>
    /// <remarks>
    /// Das betrifft nur den Supervisor des Synology-Pakets - er rendert den
    /// <c>devices:</c>-Block der Compose-Datei.
    /// </remarks>
    public bool PassAllSerialDevices { get; init; }
}

/// <summary>
/// Ein erkannter serieller Port.
/// </summary>
public sealed record SerialPortInfo
{
    /// <summary>
    /// Der Name des Ports, z. B. <c>COM3</c> oder <c>/dev/ttyUSB0</c>.
    /// </summary>
    public required string Port { get; init; }

    /// <summary>
    /// Die Beschreibung des Ports (Auf dem Windows-Betriebssystem der Name des
    /// Geräts).
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Der Pfad aus <c>ports.json</c> des Supervisors (oder <c>null</c>).
    /// </summary>
    public string? StablePath { get; init; }
}

/// <summary>
/// Das Ergebnis eines Verbindungstests.
/// </summary>
public sealed record TestResultDto
{
    /// <summary>
    /// War der Test erfolgreich?
    /// </summary>
    public required bool Ok { get; init; }

    /// <summary>
    /// Die Meldung (deutsch, für die Anzeige).
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Der maschinenlesbare Hinweis, z. B. <c>no_iam</c>.
    /// </summary>
    public string? Hint { get; init; }

    /// <summary>
    /// Die Dauer des Tests in Millisekunden.
    /// </summary>
    public required long DurationMs { get; init; }
}

/// <summary>
/// Die Anfrage für den BACnet-Verbindungstest.
/// </summary>
public sealed class BacnetTestRequest
{
    /// <summary>
    /// Der serielle Port, z. B. <c>/dev/ttyUSB0</c>.
    /// </summary>
    public string? Port { get; set; }

    /// <summary>
    /// Die Baudrate (Standard: 38400).
    /// </summary>
    public int Baudrate { get; set; } = 38400;
}

/// <summary>
/// Die Anfrage für den MQTT-Verbindungstest.
/// </summary>
public sealed class MqttTestRequest
{
    /// <summary>
    /// Der Broker-Host.
    /// </summary>
    public string? Host { get; set; }

    /// <summary>
    /// Der Broker-Port (Standard: 1883).
    /// </summary>
    public int Port { get; set; } = 1883;

    /// <summary>
    /// Der Benutzername (oder <c>null</c>).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Das Passwort im Klartext (oder <c>null</c>).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Soll TLS verwendet werden?
    /// </summary>
    public bool UseTls { get; set; }
}

/// <summary>
/// Die Konfiguration, die der Assistent schreibt.
/// </summary>
/// <remarks>
/// Das Passwort kommt im **Klartext** an und wird serverseitig gehasht - es
/// verlässt den Gateway nur auf diesem einen Weg.
/// </remarks>
public sealed class SetupConfigRequest
{
    /// <summary>
    /// Der serielle Port, z. B. <c>/dev/ttyUSB0</c>.
    /// </summary>
    public string? Port { get; set; }

    /// <summary>
    /// Die Baudrate (Standard: 38400).
    /// </summary>
    public int Baudrate { get; set; } = 38400;

    /// <summary>
    /// Der Broker-Host.
    /// </summary>
    public string? BrokerHost { get; set; }

    /// <summary>
    /// Der Broker-Port (Standard: 1883).
    /// </summary>
    public int BrokerPort { get; set; } = 1883;

    /// <summary>
    /// Der Broker-Benutzername (oder <c>null</c>).
    /// </summary>
    public string? BrokerUsername { get; set; }

    /// <summary>
    /// Das Broker-Passwort im Klartext (oder <c>null</c>).
    /// </summary>
    public string? BrokerPassword { get; set; }

    /// <summary>
    /// Soll TLS verwendet werden?
    /// </summary>
    public bool UseTls { get; set; }

    /// <summary>
    /// Der Benutzername des Administrators.
    /// </summary>
    public string? AdminUsername { get; set; }

    /// <summary>
    /// Das Passwort des Administrators im Klartext.
    /// </summary>
    public string? AdminPassword { get; set; }

    /// <summary>
    /// Sollen alle seriellen Ports durchgereicht werden?
    /// </summary>
    /// <remarks>
    /// Das ist die Voreinstellung: nur so kann der Assistent den
    /// Verbindungstest durchführen, solange der endgültige Port noch nicht
    /// feststeht. Der Supervisor des Synology-Pakets rendert damit den
    /// <c>devices:</c>-Block der Compose-Datei.
    /// </remarks>
    public bool PassAllSerialDevices { get; set; } = true;
}
