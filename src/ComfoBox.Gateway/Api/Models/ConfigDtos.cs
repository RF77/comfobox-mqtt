using ComfoBox.Gateway.Configuration;

namespace ComfoBox.Gateway.Api.Models;

/// <summary>
/// Die aktuelle Konfiguration (ohne Passwörter).
/// </summary>
public sealed record ConfigDto
{
    /// <summary>
    /// Der Abschnitt „Gateway".
    /// </summary>
    public required GatewayConfigDto Gateway { get; init; }

    /// <summary>
    /// Der Abschnitt „Bacnet".
    /// </summary>
    public required BacnetConfigDto Bacnet { get; init; }

    /// <summary>
    /// Der Abschnitt „Mqtt".
    /// </summary>
    public required MqttConfigDto Mqtt { get; init; }

    /// <summary>
    /// Der Abschnitt „Logging".
    /// </summary>
    public required LoggingConfigDto Logging { get; init; }

    /// <summary>
    /// Der Abschnitt „Api".
    /// </summary>
    public required ApiConfigDto Api { get; init; }

    /// <summary>
    /// Woher kommt die Konfiguration?
    /// </summary>
    /// <remarks>
    /// <c>file</c> = <c>config.json</c> (höchste Priorität), <c>environment</c> =
    /// Umgebungsvariablen, <c>defaults</c> = <c>appsettings.json</c>.
    /// </remarks>
    public required string Source { get; init; }
}

/// <summary>
/// Der Abschnitt „Gateway" der Konfiguration.
/// </summary>
public sealed record GatewayConfigDto
{
    /// <summary>
    /// Das Basis-Topic (Standard: <c>ComfoBox</c>).
    /// </summary>
    public required string BaseTopic { get; init; }

    /// <summary>
    /// Die Geräte-Id, z. B. <c>comfobox1</c>.
    /// </summary>
    public required string DeviceId { get; init; }

    /// <summary>
    /// Ist der Experten-Modus aktiv?
    /// </summary>
    public required bool ExpertMode { get; init; }

    /// <summary>
    /// Das Datenverzeichnis.
    /// </summary>
    public required string DataDirectory { get; init; }

    /// <summary>
    /// Das Poll-Intervall in Sekunden.
    /// </summary>
    public required int PollIntervalSeconds { get; init; }

    /// <summary>
    /// Der Faktor für sekundäre Items.
    /// </summary>
    public required int SecondaryPollFactor { get; init; }

    /// <summary>
    /// Das Limit der Schreibvorgänge im 24-h-Fenster.
    /// </summary>
    public required int MaxWritesPer24h { get; init; }

    /// <summary>
    /// Ist der Neustart über die API erlaubt?
    /// </summary>
    public required bool RestartEnabled { get; init; }
}

/// <summary>
/// Der Abschnitt „Bacnet" der Konfiguration.
/// </summary>
public sealed record BacnetConfigDto
{
    /// <summary>
    /// Der serielle Port.
    /// </summary>
    public required string Port { get; init; }

    /// <summary>
    /// Die Baudrate.
    /// </summary>
    public required int Baudrate { get; init; }

    /// <summary>
    /// Die Geräte-Instanz der ComfoBox.
    /// </summary>
    public required int DeviceId { get; init; }

    /// <summary>
    /// Die MSTP-Adresse dieses Knotens.
    /// </summary>
    public required int ThisStation { get; init; }

    /// <summary>
    /// Die höchste MAC-Adresse im Netzsegment.
    /// </summary>
    public required int MaxMaster { get; init; }

    /// <summary>
    /// Das Zeitfenster je Item in Millisekunden.
    /// </summary>
    public required int ItemTimeoutMs { get; init; }

    /// <summary>
    /// Die Anzahl der Versuche je Anfrage.
    /// </summary>
    public required int Retries { get; init; }
}

/// <summary>
/// Der Abschnitt „Mqtt" der Konfiguration.
/// </summary>
public sealed record MqttConfigDto
{
    /// <summary>
    /// Die Client-ID am Broker.
    /// </summary>
    public required string ClientId { get; init; }

    /// <summary>
    /// Das Keep-Alive in Sekunden.
    /// </summary>
    public required int KeepAliveSeconds { get; init; }

    /// <summary>
    /// Das Präfix der Discovery-Topics.
    /// </summary>
    public required string DiscoveryPrefix { get; init; }

    /// <summary>
    /// Werden die Item-Werte retained publiziert?
    /// </summary>
    public required bool UseMqttRetain { get; init; }

    /// <summary>
    /// Das Intervall der Status-Publishes in Sekunden.
    /// </summary>
    public required int StatusIntervalSeconds { get; init; }

    /// <summary>
    /// Die Broker (ohne Passwörter).
    /// </summary>
    public required MqttBrokerConfigDto[] Brokers { get; init; }
}

/// <summary>
/// Ein Broker (ohne Passwort).
/// </summary>
public sealed record MqttBrokerConfigDto
{
    /// <summary>
    /// Der Hostname oder die IP-Adresse.
    /// </summary>
    public required string Host { get; init; }

    /// <summary>
    /// Der Port.
    /// </summary>
    public required int Port { get; init; }

    /// <summary>
    /// Der Benutzername (oder <c>null</c>).
    /// </summary>
    public string? Username { get; init; }

    /// <summary>
    /// Ist ein Passwort gesetzt? (<b>der Wert selbst wird nie ausgegeben</b>)
    /// </summary>
    public required bool PasswordSet { get; init; }

    /// <summary>
    /// Läuft die Verbindung über TLS?
    /// </summary>
    public required bool UseTls { get; init; }

    /// <summary>
    /// Werden selbstsignierte Zertifikate akzeptiert?
    /// </summary>
    public required bool AllowUntrustedCertificates { get; init; }
}

/// <summary>
/// Der Abschnitt „Logging" der Konfiguration.
/// </summary>
public sealed record LoggingConfigDto
{
    /// <summary>
    /// Das Verzeichnis der Log-Dateien.
    /// </summary>
    public required string Directory { get; init; }

    /// <summary>
    /// Die Anzahl der aufbewahrten Log-Dateien.
    /// </summary>
    public required int RetainedFileCount { get; init; }

    /// <summary>
    /// Die Kapazität des In-Memory-Ringpuffers.
    /// </summary>
    public required int RingBufferCapacity { get; init; }

    /// <summary>
    /// Der minimale Level im Ringpuffer.
    /// </summary>
    public required string RingBufferMinimumLevel { get; init; }
}

/// <summary>
/// Der Abschnitt „Api" der Konfiguration.
/// </summary>
public sealed record ApiConfigDto
{
    /// <summary>
    /// Müssen alle Anfragen authentifiziert werden?
    /// </summary>
    public required bool RequireAuthentication { get; init; }

    /// <summary>
    /// Ist die Ersteinrichtung abgeschlossen?
    /// </summary>
    public required bool SetupCompleted { get; init; }

    /// <summary>
    /// Der Benutzername des Administrators (oder <c>null</c>).
    /// </summary>
    public string? AdminUsername { get; init; }

    /// <summary>
    /// Wie viele API-Token existieren?
    /// </summary>
    public required int TokenCount { get; init; }
}

/// <summary>
/// Die zur Laufzeit änderbaren Konfigurationswerte.
/// </summary>
/// <remarks>
/// Alles andere (Port, Baudrate, Broker) erfordert einen Neustart.
/// </remarks>
public sealed class ConfigUpdateRequest
{
    /// <summary>
    /// Der Experten-Modus.
    /// </summary>
    public bool? ExpertMode { get; set; }

    /// <summary>
    /// Das Poll-Intervall in Sekunden.
    /// </summary>
    public int? PollIntervalSeconds { get; set; }

    /// <summary>
    /// Das Limit der Schreibvorgänge im 24-h-Fenster.
    /// </summary>
    public int? MaxWritesPer24h { get; set; }

    /// <summary>
    /// Werden die Item-Werte retained publiziert?
    /// </summary>
    public bool? UseMqttRetain { get; set; }
}
