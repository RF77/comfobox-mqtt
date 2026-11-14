using System.IO.Ports;
using System.Text.Json;
using System.Text.Json.Nodes;
using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Configuration;

namespace ComfoBox.Gateway.Hardware;

/// <summary>
/// Die Suche nach den seriellen Ports der Anlage.
/// </summary>
public interface ISerialPortScanner
{
    /// <summary>
    /// Sucht die seriellen Ports.
    /// </summary>
    /// <returns>
    /// Die gefundenen Ports - leer, wenn kein Adapter angeschlossen ist.
    /// </returns>
    IReadOnlyList<SerialPortInfo> Scan();
}

/// <summary>
/// Die Suche nach den seriellen Ports der Anlage.
/// </summary>
/// <remarks>
/// <para>
/// Es gibt zwei Quellen:
/// </para>
/// <list type="bullet">
/// <item>
/// die <c>ports.json</c> des Supervisors (Synology/HA-Add-on) - sie enthält
/// den **stabilen** Pfad (<c>/dev/serial/by-id/…</c>) und die Beschreibung
/// des Adapters;
/// </item>
/// <item>
/// die Liste des Betriebssystems (<see cref="SerialPort.GetPortNames"/>) -
/// sie ist der Rückfall, wenn keine <c>ports.json</c> existiert.
/// </item>
/// </list>
/// <para>
/// Die Datei wird bei **jedem** Scan neu gelesen: ein nachträglich
/// angeschlossener Adapter erscheint damit sofort.
/// </para>
/// </remarks>
public sealed class SerialPortScanner : ISerialPortScanner
{
    /// <summary>
    /// Der Name der Datei mit den erkannten Ports (vom Supervisor erzeugt).
    /// </summary>
    public const string PortsFileName = "ports.json";

    private readonly ConfigStore _config;
    private readonly ISerialDeviceProbe _probe;
    private readonly ILogger<SerialPortScanner> _logger;

    /// <summary>
    /// Erzeugt den Scanner.
    /// </summary>
    /// <param name="config">Die Konfiguration (für das Datenverzeichnis).</param>
    /// <param name="probe">Die Geräte-Erkennung (Chipsatz, Rechte).</param>
    /// <param name="logger">Logger.</param>
    public SerialPortScanner(
        ConfigStore config,
        ISerialDeviceProbe probe,
        ILogger<SerialPortScanner> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(probe);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config;
        _probe = probe;
        _logger = logger;
    }

    /// <inheritdoc />
    public IReadOnlyList<SerialPortInfo> Scan()
    {
        var portsFile = Path.Combine(_config.DataDirectory, PortsFileName);

        if (File.Exists(portsFile))
        {
            var fromFile = ReadPortsFile(portsFile);
            if (fromFile.Count > 0)
            {
                return fromFile.Select(Describe).ToList();
            }
        }

        try
        {
            return Collect()
                .OrderBy(info => KindOrder(info.Kind))
                .ThenBy(info => info.Port, StringComparer.Ordinal)
                .ToArray();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            _logger.LogWarning(ex, "Die seriellen Ports konnten nicht ermittelt werden.");
            return [];
        }
    }

    /// <summary>
    /// Sammelt die Portnamen des Betriebssystems.
    /// </summary>
    /// <remarks>
    /// <see cref="SerialPort.GetPortNames"/> ist die verlässliche Quelle.
    /// Zusätzlich werden die USB-Geräte eingesammelt: so erscheint ein Adapter
    /// auch dann, wenn das Betriebssystem ihn (noch) nicht in seiner Liste
    /// führt - und die Oberfläche kann ihn wenigstens benennen.
    /// </remarks>
    private List<SerialPortInfo> Collect()
    {
        var names = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var name in SerialPort.GetPortNames())
        {
            names.Add(name);
        }

        if (OperatingSystem.IsLinux())
        {
            foreach (var pattern in new[] { "ttyUSB*", "ttyACM*" })
            {
                try
                {
                    foreach (var path in Directory.EnumerateFileSystemEntries("/dev", pattern))
                    {
                        names.Add(path);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // Kein /dev Zugriff - die Liste des Betriebssystems bleibt.
                }
            }
        }

        return names.Select(name => Describe(new SerialPortInfo { Port = name })).ToList();
    }

    /// <summary>
    /// Ergänzt einen Port um die Angaben aus <c>sysfs</c>.
    /// </summary>
    private SerialPortInfo Describe(SerialPortInfo port)
    {
        var device = _probe.Describe(port.Port);

        return port with
        {
            // Die Beschreibung des Supervisors (ports.json) hat Vorrang - sie
            // kommt von der Plattform und ist dort schon lesbar.
            Description = string.IsNullOrWhiteSpace(port.Description) ? device.Label : port.Description,
            StablePath = port.StablePath ?? device.StablePath,
            Kind = device.Kind,
            Chip = device.Chip,
            VendorId = device.VendorId,
            ProductId = device.ProductId,
            Manufacturer = device.Manufacturer,
            Product = device.Product,
            SerialNumber = device.SerialNumber,
            Driver = device.Driver,
        };
    }

    /// <summary>
    /// Die Sortierreihenfolge der Port-Arten: Adapter zuerst.
    /// </summary>
    private static int KindOrder(string? kind) => kind switch
    {
        SerialDeviceKinds.Usb => 0,
        SerialDeviceKinds.BuiltIn => 1,
        _ => 2,
    };

    /// <summary>
    /// Liest die Port-Liste des Supervisors.
    /// </summary>
    private List<SerialPortInfo> ReadPortsFile(string path)
    {
        try
        {
            var text = File.ReadAllText(path);
            var root = JsonNode.Parse(text) as JsonObject;

            if (root?["ports"] is not JsonArray array)
            {
                return [];
            }

            var ports = new List<SerialPortInfo>(array.Count);

            foreach (var node in array)
            {
                if (node is not JsonObject entry)
                {
                    continue;
                }

                var device = entry["device"]?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(device))
                {
                    continue;
                }

                ports.Add(new SerialPortInfo
                {
                    Port = device,
                    Description = entry["description"]?.GetValue<string>(),
                    StablePath = entry["stableId"]?.GetValue<string>(),
                });
            }

            return ports;
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Die Port-Liste konnte nicht gelesen werden.");
            return [];
        }
    }
}
