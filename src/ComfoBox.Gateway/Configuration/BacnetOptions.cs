using System.ComponentModel.DataAnnotations;

namespace ComfoBox.Gateway.Configuration;

/// <summary>
/// Die Einstellungen der BACnet-Anbindung.
/// </summary>
public sealed class BacnetOptions
{
    /// <summary>
    /// Der Name des Konfigurationsabschnitts.
    /// </summary>
    public const string SectionName = "Bacnet";

    /// <summary>
    /// Die von BACnet unterstützten Baudraten.
    /// </summary>
    public static readonly int[] AllowedBaudrates = [9600, 19200, 38400, 57600, 76800, 115200];

    /// <summary>
    /// Der serielle Port, z. B. <c>COM3</c> oder <c>/dev/ttyUSB0</c>.
    /// </summary>
    [Required]
    [RegularExpression(@"^\S+$", ErrorMessage = "Bacnet:Port darf nicht leer sein.")]
    public string Port { get; init; } = string.Empty;

    /// <summary>
    /// Die Baudrate des seriellen Ports.
    /// </summary>
    /// <remarks>
    /// Der ComfoBox muss auf <c>38400</c> umgestellt sein (Werks-Default 76800).
    /// </remarks>
    [Range(9600, 115200)]
    public int Baudrate { get; init; } = 38400;

    /// <summary>
    /// Die Geräte-Instanz der ComfoBox am Bus (ELESTA: 1).
    /// </summary>
    [Range(0, 254)]
    public int DeviceId { get; init; } = 1;

    /// <summary>
    /// Die MSTP-Adresse dieses Knotens.
    /// </summary>
    [Range(0, 254)]
    public int ThisStation { get; init; } = 3;

    /// <summary>
    /// Die höchste MAC-Adresse im Netzsegment.
    /// </summary>
    [Range(0, 254)]
    public int MaxMaster { get; init; } = 127;

    /// <summary>
    /// Das Zeitfenster je Item in Millisekunden.
    /// </summary>
    /// <remarks>
    /// Je Item, damit ein hängender Wert nicht den ganzen Poll-Zyklus
    /// blockiert.
    /// </remarks>
    [Range(100, 60000)]
    public int ItemTimeoutMs { get; init; } = 3000;

    /// <summary>
    /// Die Anzahl der Versuche je Anfrage.
    /// </summary>
    [Range(1, 10)]
    public int Retries { get; init; } = 3;

    /// <summary>
    /// Prüft, ob die Baudrate von BACnet unterstützt wird.
    /// </summary>
    /// <param name="baudrate">Die zu prüfende Baudrate.</param>
    public static bool IsAllowedBaudrate(int baudrate) => Array.IndexOf(AllowedBaudrates, baudrate) >= 0;
}
