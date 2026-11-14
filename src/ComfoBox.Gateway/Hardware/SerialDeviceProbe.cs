using System.Globalization;

namespace ComfoBox.Gateway.Hardware;

/// <summary>
/// Die Art eines seriellen Ports.
/// </summary>
public static class SerialDeviceKinds
{
    /// <summary>Ein Adapter am USB-Bus (z. B. CH340, FT232).</summary>
    public const string Usb = "usb";

    /// <summary>Ein UART auf der Platine (z. B. <c>ttyAMA0</c> auf einem Raspberry Pi).</summary>
    public const string BuiltIn = "builtin";

    /// <summary>Alles andere (oder unbekannt).</summary>
    public const string Other = "other";
}

/// <summary>
/// Was an einem seriellen Port hängt.
/// </summary>
/// <remarks>
/// <para>
/// Die Angaben kommen aus <c>sysfs</c> (<c>/sys/class/tty/…</c>) und aus
/// <c>/dev/serial/by-id</c>. Damit sieht der Benutzer in der Oberfläche, ob
/// ein Port der CH340-Adapter ist oder nur der UART der Platine - die
/// Gerätedatei selbst verrät das nicht.
/// </para>
/// </remarks>
public sealed record SerialDeviceDetails
{
    /// <summary>Die Art des Ports (siehe <see cref="SerialDeviceKinds"/>).</summary>
    public string Kind { get; init; } = SerialDeviceKinds.Other;

    /// <summary>Der erkannte Chipsatz, z. B. <c>CH340</c> (oder <c>null</c>).</summary>
    public string? Chip { get; init; }

    /// <summary>Die USB-Herstellerkennung, z. B. <c>1a86</c> (oder <c>null</c>).</summary>
    public string? VendorId { get; init; }

    /// <summary>Die USB-Produktkennung, z. B. <c>7523</c> (oder <c>null</c>).</summary>
    public string? ProductId { get; init; }

    /// <summary>Der Hersteller aus dem USB-Deskriptor (oder <c>null</c>).</summary>
    public string? Manufacturer { get; init; }

    /// <summary>Das Produkt aus dem USB-Deskriptor (oder <c>null</c>).</summary>
    public string? Product { get; init; }

    /// <summary>Die Seriennummer des Adapters (oder <c>null</c>).</summary>
    public string? SerialNumber { get; init; }

    /// <summary>Der gebundene Kernel-Treiber, z. B. <c>ch341-uart</c> (oder <c>null</c>).</summary>
    public string? Driver { get; init; }

    /// <summary>Der stabile Pfad unter <c>/dev/serial/by-id</c> (oder <c>null</c>).</summary>
    public string? StablePath { get; init; }

    /// <summary>Ein lesbarer Name für die Oberfläche (oder <c>null</c>).</summary>
    public string? Label { get; init; }
}

/// <summary>
/// Die Zugriffsrechte einer Gerätedatei - aus Sicht des laufenden Prozesses.
/// </summary>
/// <remarks>
/// Diese Angaben machen aus „Keine Berechtigung" eine überprüfbare Aussage:
/// sie zeigen, welche Rechte die Datei hat und in welchen Gruppen der Prozess
/// läuft. Genau daran entscheidet sich, ob die Dateirechte oder die
/// Device-Cgroup des Containers die Ursache ist.
/// </remarks>
public sealed record SerialPortAccess
{
    /// <summary>Gibt es die Gerätedatei überhaupt?</summary>
    public required bool Exists { get; init; }

    /// <summary>Der Modus in Oktalschreibweise, z. B. <c>0660</c> (oder <c>null</c>).</summary>
    public string? Mode { get; init; }

    /// <summary>Der Modus als Text, z. B. <c>crw-rw----</c> (oder <c>null</c>).</summary>
    public string? ModeText { get; init; }

    /// <summary>Die Benutzerkennung des Prozesses (oder <c>null</c>).</summary>
    public int? Uid { get; init; }

    /// <summary>Die Gruppen des Prozesses (ergänzende Gruppen eingeschlossen).</summary>
    public IReadOnlyList<int> ProcessGroups { get; init; } = [];

    /// <summary>
    /// Der bekannte GID-Bereich des seriellen Zugriffs (<c>dialout</c> ist auf
    /// Debian und Raspberry Pi OS die 20).
    /// </summary>
    public const int DialoutGid = 20;

    /// <summary>Läuft der Prozess in der Gruppe <c>dialout</c>?</summary>
    public bool HasDialoutGroup => ProcessGroups.Contains(DialoutGid);
}

/// <summary>
/// Ermittelt, was an einem seriellen Port hängt und welche Rechte er hat.
/// </summary>
public interface ISerialDeviceProbe
{
    /// <summary>
    /// Beschreibt das Gerät hinter einem Port.
    /// </summary>
    /// <param name="portName">Der Port, z. B. <c>/dev/ttyUSB0</c>.</param>
    /// <returns>Die Angaben (leer, wenn nichts ermittelt werden konnte).</returns>
    SerialDeviceDetails Describe(string portName);

    /// <summary>
    /// Ermittelt die Zugriffsrechte eines Ports.
    /// </summary>
    /// <param name="portName">Der Port, z. B. <c>/dev/ttyUSB0</c>.</param>
    /// <returns>Die Rechte und die Gruppen des Prozesses.</returns>
    SerialPortAccess DescribeAccess(string portName);
}

/// <summary>
/// Liest die Geräte- und Rechte-Angaben aus <c>sysfs</c> und <c>/proc</c>.
/// </summary>
/// <remarks>
/// <para>
/// Auf Linux ist der Portname eine Gerätedatei; <c>/sys/class/tty/&lt;name&gt;</c>
/// führt über das USB-Gerät zu Hersteller, Produkt und Seriennummer.
/// </para>
/// <para>
/// Auf Windows gibt es kein <c>sysfs</c> - dort bleiben die Angaben leer und
/// der Portname („COM3") ist die Beschreibung.
/// </para>
/// <para>
/// Die Wurzeln sind austauschbar, damit die Auswertung ohne echte
/// <c>sysfs</c>-Einträge getestet werden kann.
/// </para>
/// </remarks>
public sealed class SerialDeviceProbe : ISerialDeviceProbe
{
    private readonly string _sysRoot;
    private readonly string _devRoot;
    private readonly string _procStatusPath;

    /// <summary>
    /// Erzeugt die Sonde für das laufende System.
    /// </summary>
    public SerialDeviceProbe()
        : this("/sys", "/dev", "/proc/self/status")
    {
    }

    /// <summary>
    /// Erzeugt die Sonde mit austauschbaren Wurzeln (für Tests).
    /// </summary>
    /// <param name="sysRoot">Das Verzeichnis von <c>sysfs</c>.</param>
    /// <param name="devRoot">Das Verzeichnis von <c>/dev</c>.</param>
    /// <param name="procStatusPath">Der Pfad zu <c>/proc/self/status</c>.</param>
    public SerialDeviceProbe(string sysRoot, string devRoot, string procStatusPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sysRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(devRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(procStatusPath);

        _sysRoot = sysRoot;
        _devRoot = devRoot;
        _procStatusPath = procStatusPath;
    }

    /// <inheritdoc />
    public SerialDeviceDetails Describe(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new SerialDeviceDetails();
        }

        var name = ResolvePortName(portName);

        // Kein Betriebssystem-Zweig: auf Windows gibt es '/sys/class/tty'
        // nicht, damit fällt die Auswertung von selbst auf den Portnamen
        // zurück. Das macht die Auswertung mit einem nachgebauten sysfs-Baum
        // auf jedem Betriebssystem testbar.
        var ttyDirectory = Path.Combine(_sysRoot, "class", "tty", name);
        if (!Directory.Exists(ttyDirectory))
        {
            return new SerialDeviceDetails { Label = name };
        }

        var devicePath = Path.Combine(ttyDirectory, "device");
        var driver = ReadLinkName(Path.Combine(devicePath, "driver"));
        var stablePath = FindStablePath(portName);

        var usbDirectory = FindUsbDeviceDirectory(devicePath);

        if (usbDirectory is null)
        {
            // Kein USB-Gerät dahinter: der UART der Platine.
            return new SerialDeviceDetails
            {
                Kind = SerialDeviceKinds.BuiltIn,
                Driver = driver,
                StablePath = stablePath,
                Label = $"{name} · UART auf der Platine",
            };
        }

        var vendorId = ReadText(Path.Combine(usbDirectory, "idVendor"));
        var productId = ReadText(Path.Combine(usbDirectory, "idProduct"));
        var chip = ChipName(vendorId, productId);

        return new SerialDeviceDetails
        {
            Kind = SerialDeviceKinds.Usb,
            Chip = chip,
            VendorId = vendorId,
            ProductId = productId,
            Manufacturer = ReadText(Path.Combine(usbDirectory, "manufacturer")),
            Product = ReadText(Path.Combine(usbDirectory, "product")),
            SerialNumber = ReadText(Path.Combine(usbDirectory, "serial")),
            Driver = driver,
            StablePath = stablePath,
            Label = BuildLabel(chip, vendorId, productId,
                ReadText(Path.Combine(usbDirectory, "manufacturer")),
                ReadText(Path.Combine(usbDirectory, "product"))),
        };
    }

    /// <inheritdoc />
    public SerialPortAccess DescribeAccess(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new SerialPortAccess { Exists = false };
        }

        var exists = File.Exists(portName);

        if (!exists)
        {
            return new SerialPortAccess { Exists = false };
        }

        UnixFileMode? mode = null;
        if (OperatingSystem.IsLinux())
        {
            try
            {
                mode = File.GetUnixFileMode(portName);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
            {
                mode = null;
            }
        }

        var (uid, groups) = ReadProcessIdentity();

        return new SerialPortAccess
        {
            Exists = true,
            Mode = mode is null ? null : FormatMode(mode.Value),
            ModeText = mode is null ? null : FormatModeText(mode.Value),
            Uid = uid,
            ProcessGroups = groups,
        };
    }

    /// <summary>
    /// Ermittelt den Gerätenamen - auch aus einem <c>by-id</c>-Pfad.
    /// </summary>
    /// <remarks>
    /// <c>/dev/serial/by-id/usb-1a86_USB_Serial-if00-port0</c> ist ein Symlink
    /// auf <c>/dev/ttyUSB0</c>; der Name in <c>sysfs</c> ist der des Ziels.
    /// </remarks>
    private static string ResolvePortName(string portName)
    {
        var name = Path.GetFileName(portName);

        try
        {
            var target = File.ResolveLinkTarget(portName, returnFinalTarget: true);
            if (target is not null)
            {
                name = Path.GetFileName(target.FullName);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Kein Symlink oder nicht auflösbar - der Name bleibt.
        }

        return name;
    }

    /// <summary>
    /// Sucht das USB-Geräteverzeichnis oberhalb des Interface-Verzeichnisses.
    /// </summary>
    private static string? FindUsbDeviceDirectory(string devicePath)
    {
        var directory = ResolveDirectory(devicePath);

        for (var level = 0; level < 5 && directory is not null; level++)
        {
            if (File.Exists(Path.Combine(directory, "idVendor")))
            {
                return directory;
            }

            directory = Path.GetDirectoryName(directory);
        }

        return null;
    }

    /// <summary>
    /// Löst einen Symlink auf einen echten Pfad auf.
    /// </summary>
    private static string? ResolveDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                var target = Directory.ResolveLinkTarget(path, returnFinalTarget: true);
                return target?.FullName ?? Path.GetFullPath(path);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Nicht auflösbar - dann gilt der angegebene Pfad.
        }

        return Directory.Exists(path) ? path : null;
    }

    /// <summary>
    /// Sucht den stabilen Pfad unter <c>/dev/serial/by-id</c>.
    /// </summary>
    private string? FindStablePath(string portName)
    {
        var byIdDirectory = Path.Combine(_devRoot, "serial", "by-id");

        try
        {
            if (!Directory.Exists(byIdDirectory))
            {
                return null;
            }

            var target = ResolveTarget(portName);

            foreach (var link in Directory.EnumerateFileSystemEntries(byIdDirectory))
            {
                if (string.Equals(ResolveTarget(link), target, StringComparison.Ordinal))
                {
                    return link;
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        return null;
    }

    /// <summary>
    /// Löst einen Symlink auf sein Ziel auf (oder gibt den Pfad selbst zurück).
    /// </summary>
    private static string ResolveTarget(string path)
    {
        try
        {
            var target = File.ResolveLinkTarget(path, returnFinalTarget: true);
            return target?.FullName ?? path;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return path;
        }
    }

    /// <summary>
    /// Liest den Namen eines Symlinks (z. B. den Treiber).
    /// </summary>
    private static string? ReadLinkName(string path)
    {
        try
        {
            var info = new FileInfo(path);
            var target = info.LinkTarget;

            if (string.IsNullOrWhiteSpace(target))
            {
                return null;
            }

            return Path.GetFileName(target.TrimEnd('/'));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Liest eine einzelne Textzeile (getrimmt).
    /// </summary>
    private static string? ReadText(string path)
    {
        try
        {
            if (!File.Exists(path))
            {
                return null;
            }

            var text = File.ReadAllText(path).Trim();
            return text.Length == 0 ? null : text;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>
    /// Ordnet einer USB-Kennung einen Chipsatz zu.
    /// </summary>
    private static string? ChipName(string? vendorId, string? productId)
    {
        if (string.IsNullOrWhiteSpace(vendorId) || string.IsNullOrWhiteSpace(productId))
        {
            return null;
        }

        var key = $"{vendorId.ToLowerInvariant()}:{productId.ToLowerInvariant()}";

        return key switch
        {
            "1a86:7523" => "CH340",
            "1a86:5523" => "CH341A",
            "1a86:7522" => "CH340B",
            "0403:6001" => "FT232R",
            "0403:6015" => "FT231X",
            "0403:6010" => "FT2232",
            "10c4:ea60" => "CP2102",
            "10c4:ea70" => "CP2105",
            "067b:2303" => "PL2303",
            _ => null,
        };
    }

    /// <summary>
    /// Baut den lesbaren Namen für die Oberfläche.
    /// </summary>
    private static string BuildLabel(
        string? chip,
        string? vendorId,
        string? productId,
        string? manufacturer,
        string? product)
    {
        var id = string.IsNullOrWhiteSpace(vendorId) || string.IsNullOrWhiteSpace(productId)
            ? null
            : $"{vendorId}:{productId}";

        if (!string.IsNullOrWhiteSpace(chip))
        {
            return id is null ? $"{chip} · USB-Seriell" : $"{chip} · USB-Seriell ({id})";
        }

        var name = string.Join(' ', new[] { manufacturer, product }
            .Where(part => !string.IsNullOrWhiteSpace(part)));

        if (!string.IsNullOrWhiteSpace(name))
        {
            return id is null ? name : $"{name} ({id})";
        }

        return id is null ? "USB-Seriell" : $"USB-Seriell ({id})";
    }

    /// <summary>
    /// Liest die Benutzerkennung und die Gruppen des laufenden Prozesses.
    /// </summary>
    private (int? Uid, IReadOnlyList<int> Groups) ReadProcessIdentity()
    {
        int? uid = null;
        var groups = new List<int>();

        try
        {
            if (!File.Exists(_procStatusPath))
            {
                return (null, groups);
            }

            foreach (var line in File.ReadLines(_procStatusPath))
            {
                if (line.StartsWith("Uid:", StringComparison.Ordinal))
                {
                    // „Uid:\t<real>\t<effective>\t<saved>\t<fs>"
                    var parts = line[4..].Split('\t', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                    if (parts.Length > 0 && int.TryParse(parts[0], CultureInfo.InvariantCulture, out var value))
                    {
                        uid = value;
                    }
                }
                else if (line.StartsWith("Groups:", StringComparison.Ordinal))
                {
                    foreach (var part in line[7..].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    {
                        if (int.TryParse(part, CultureInfo.InvariantCulture, out var gid))
                        {
                            groups.Add(gid);
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ohne /proc keine Angaben - die Diagnose bleibt dann allgemein.
        }

        return (uid, groups);
    }

    /// <summary>
    /// Formatiert die Rechte als Oktalzahl (z. B. <c>0660</c>).
    /// </summary>
    private static string FormatMode(UnixFileMode mode)
    {
        var value = 0;

        if (mode.HasFlag(UnixFileMode.UserRead)) { value |= 0b100_000_000; }
        if (mode.HasFlag(UnixFileMode.UserWrite)) { value |= 0b010_000_000; }
        if (mode.HasFlag(UnixFileMode.UserExecute)) { value |= 0b001_000_000; }
        if (mode.HasFlag(UnixFileMode.GroupRead)) { value |= 0b000_100_000; }
        if (mode.HasFlag(UnixFileMode.GroupWrite)) { value |= 0b000_010_000; }
        if (mode.HasFlag(UnixFileMode.GroupExecute)) { value |= 0b000_001_000; }
        if (mode.HasFlag(UnixFileMode.OtherRead)) { value |= 0b000_000_100; }
        if (mode.HasFlag(UnixFileMode.OtherWrite)) { value |= 0b000_000_010; }
        if (mode.HasFlag(UnixFileMode.OtherExecute)) { value |= 0b000_000_001; }

        return Convert.ToString(value, 8).PadLeft(4, '0');
    }

    /// <summary>
    /// Formatiert die Rechte wie <c>ls -l</c> (z. B. <c>crw-rw----</c>).
    /// </summary>
    private static string FormatModeText(UnixFileMode mode)
    {
        var builder = new System.Text.StringBuilder("c");

        builder.Append(mode.HasFlag(UnixFileMode.UserRead) ? 'r' : '-');
        builder.Append(mode.HasFlag(UnixFileMode.UserWrite) ? 'w' : '-');
        builder.Append(mode.HasFlag(UnixFileMode.UserExecute) ? 'x' : '-');
        builder.Append(mode.HasFlag(UnixFileMode.GroupRead) ? 'r' : '-');
        builder.Append(mode.HasFlag(UnixFileMode.GroupWrite) ? 'w' : '-');
        builder.Append(mode.HasFlag(UnixFileMode.GroupExecute) ? 'x' : '-');
        builder.Append(mode.HasFlag(UnixFileMode.OtherRead) ? 'r' : '-');
        builder.Append(mode.HasFlag(UnixFileMode.OtherWrite) ? 'w' : '-');
        builder.Append(mode.HasFlag(UnixFileMode.OtherExecute) ? 'x' : '-');

        return builder.ToString();
    }
}
