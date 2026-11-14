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

    /// <summary>Der Benutzername des Prozesses, z. B. <c>pi</c> (oder <c>null</c>).</summary>
    public string? ProcessUser { get; init; }

    /// <summary>Die Gruppen des Prozesses (ergänzende Gruppen eingeschlossen).</summary>
    public IReadOnlyList<int> ProcessGroups { get; init; } = [];

    /// <summary>Die Benutzerkennung des Besitzers der Gerätedatei (oder <c>null</c>).</summary>
    public int? DeviceUid { get; init; }

    /// <summary>Die Gruppenkennung des Besitzers der Gerätedatei (oder <c>null</c>).</summary>
    /// <remarks>
    /// Der Wert kommt aus <c>/run/udev/data</c>. Fehlt er (typisch im
    /// Container, wo <c>/run/udev</c> nicht eingebunden ist), gilt
    /// <see cref="DialoutGid"/> als Annahme.
    /// </remarks>
    public int? DeviceGid { get; init; }

    /// <summary>Der Gruppenname der Gerätedatei, z. B. <c>dialout</c> (oder <c>null</c>).</summary>
    public string? DeviceGroupName { get; init; }

    /// <summary>Läuft der Gateway in einem Container?</summary>
    /// <remarks>
    /// Davon hängt die Lösung ab: im Container entscheidet <c>group_add</c>,
    /// auf dem Host die Gruppenzugehörigkeit des Benutzers.
    /// </remarks>
    public bool IsContainer { get; init; }

    /// <summary>
    /// Der bekannte GID-Bereich des seriellen Zugriffs (<c>dialout</c> ist auf
    /// Debian und Raspberry Pi OS die 20).
    /// </summary>
    public const int DialoutGid = 20;

    /// <summary>
    /// Die Gruppe, die den Zugriff entscheidet - die des Geräts, sonst <c>dialout</c>.
    /// </summary>
    public int RequiredGid => DeviceGid ?? DialoutGid;

    /// <summary>Der Name der entscheidenden Gruppe.</summary>
    public string RequiredGroupName => DeviceGroupName ?? "dialout";

    /// <summary>Läuft der Prozess in der Gruppe der Gerätedatei?</summary>
    public bool HasRequiredGroup => ProcessGroups.Contains(RequiredGid);
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
    private readonly string _udevRoot;
    private readonly string _etcGroupPath;

    /// <summary>
    /// Erzeugt die Sonde für das laufende System.
    /// </summary>
    public SerialDeviceProbe()
        : this("/sys", "/dev", "/proc/self/status", "/run/udev", "/etc/group")
    {
    }

    /// <summary>
    /// Erzeugt die Sonde mit austauschbaren Wurzeln (für Tests).
    /// </summary>
    /// <param name="sysRoot">Das Verzeichnis von <c>sysfs</c>.</param>
    /// <param name="devRoot">Das Verzeichnis von <c>/dev</c>.</param>
    /// <param name="procStatusPath">Der Pfad zu <c>/proc/self/status</c>.</param>
    public SerialDeviceProbe(string sysRoot, string devRoot, string procStatusPath)
        : this(sysRoot, devRoot, procStatusPath, "/run/udev", "/etc/group")
    {
    }

    /// <summary>
    /// Erzeugt die Sonde mit austauschbaren Wurzeln (für Tests).
    /// </summary>
    /// <param name="sysRoot">Das Verzeichnis von <c>sysfs</c>.</param>
    /// <param name="devRoot">Das Verzeichnis von <c>/dev</c>.</param>
    /// <param name="procStatusPath">Der Pfad zu <c>/proc/self/status</c>.</param>
    /// <param name="udevRoot">Das Verzeichnis von <c>/run/udev</c>.</param>
    /// <param name="etcGroupPath">Der Pfad zu <c>/etc/group</c>.</param>
    public SerialDeviceProbe(
        string sysRoot,
        string devRoot,
        string procStatusPath,
        string udevRoot,
        string etcGroupPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sysRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(devRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(procStatusPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(udevRoot);
        ArgumentException.ThrowIfNullOrWhiteSpace(etcGroupPath);

        _sysRoot = sysRoot;
        _devRoot = devRoot;
        _procStatusPath = procStatusPath;
        _udevRoot = udevRoot;
        _etcGroupPath = etcGroupPath;
    }

    /// <inheritdoc />
    public SerialDeviceDetails Describe(string portName)
    {
        if (string.IsNullOrWhiteSpace(portName))
        {
            return new SerialDeviceDetails();
        }

        var name = ResolvePortName(portName);
        var stablePath = FindStablePath(portName);

        // Ein Name wie '/dev/serial/by-id/usb-1a86_USB2.0-Serial-if00-port0'
        // enthält die Herstellerkennung. Das ist die letzte Rückfalllinie,
        // wenn sysfs nicht weiterhilft.
        var vidFromName = VendorFromStablePath(stablePath);

        // Kein Betriebssystem-Zweig: auf Windows gibt es '/sys/class/tty'
        // nicht, damit fällt die Auswertung von selbst auf den Portnamen
        // zurück. Das macht die Auswertung mit einem nachgebauten sysfs-Baum
        // auf jedem Betriebssystem testbar.
        var ttyDirectory = Path.Combine(_sysRoot, "class", "tty", name);

        if (!Directory.Exists(ttyDirectory))
        {
            // Kein sysfs-Eintrag. Der Name entscheidet, ob es überhaupt ein
            // USB-Gerät sein kann - 'COM3' oder ein unbekannter Pfad bleibt
            // ohne Art.
            return IsUsbSerialName(name) || vidFromName is not null
                ? UsbWithoutDetails(name, stablePath, vidFromName)
                : new SerialDeviceDetails { Label = name, StablePath = stablePath };
        }

        var devicePath = Path.Combine(ttyDirectory, "device");
        var driver = ReadLinkName(Path.Combine(devicePath, "driver"))
                     ?? ReadLinkName(Path.Combine(devicePath, "..", "driver"));

        var usbDirectory = FindUsbDeviceDirectory(devicePath)
                           ?? FindUsbDeviceByName(name);

        // 1. Vollständige Angaben aus sysfs.
        if (usbDirectory is not null)
        {
            var vendorId = ReadText(Path.Combine(usbDirectory, "idVendor")) ?? vidFromName;
            var productId = ReadText(Path.Combine(usbDirectory, "idProduct"));
            var chip = ChipName(vendorId, productId);
            var manufacturer = ReadText(Path.Combine(usbDirectory, "manufacturer"));
            var product = ReadText(Path.Combine(usbDirectory, "product"));

            return new SerialDeviceDetails
            {
                Kind = SerialDeviceKinds.Usb,
                Chip = chip,
                VendorId = vendorId,
                ProductId = productId,
                Manufacturer = manufacturer,
                Product = product,
                SerialNumber = ReadText(Path.Combine(usbDirectory, "serial")),
                Driver = driver,
                StablePath = stablePath,
                Label = BuildLabel(chip, vendorId, productId, manufacturer, product),
            };
        }

        // 2. Kein USB-Verzeichnis gefunden, aber der Name verrät den Hersteller.
        if (vidFromName is not null)
        {
            var chip = ChipName(vidFromName, null);

            return new SerialDeviceDetails
            {
                Kind = SerialDeviceKinds.Usb,
                Chip = chip,
                VendorId = vidFromName,
                Driver = driver,
                StablePath = stablePath,
                Label = BuildLabel(chip, vidFromName, null, null, null),
            };
        }

        // 3. Ein ttyUSB*/ttyACM* ist auf Linux **immer** ein USB-Gerät - auch
        //    wenn die Details fehlen. Ein ttyAMA0/ttyS0 ist der UART der
        //    Platine.
        if (IsUsbSerialName(name))
        {
            return UsbWithoutDetails(name, stablePath, null) with { Driver = driver };
        }

        return new SerialDeviceDetails
        {
            Kind = SerialDeviceKinds.BuiltIn,
            Driver = driver,
            StablePath = stablePath,
            Label = $"{name} · UART auf der Platine",
        };
    }

    /// <summary>
    /// Ein USB-Seriell-Gerät ohne weitere Angaben.
    /// </summary>
    /// <remarks>
    /// Auf Linux heißt ein USB-Adapter immer <c>ttyUSB…</c> oder
    /// <c>ttyACM…</c>; der Name allein ist damit ein sicherer Hinweis. Fehlen
    /// die Details aus <c>sysfs</c>, wird das gesagt - aber die Art stimmt.
    /// </remarks>
    private static SerialDeviceDetails UsbWithoutDetails(
        string name,
        string? stablePath,
        string? vendorId)
    {
        var chip = ChipName(vendorId, null);

        return new SerialDeviceDetails
        {
            Kind = SerialDeviceKinds.Usb,
            Chip = chip,
            VendorId = vendorId,
            StablePath = stablePath,
            Label = chip is not null
                ? BuildLabel(chip, vendorId, null, null, null)
                : $"{name} · USB-Seriell",
        };
    }

    /// <summary>
    /// Ist der Name ein USB-Seriell-Gerät?
    /// </summary>
    private static bool IsUsbSerialName(string name) =>
        name.StartsWith("ttyUSB", StringComparison.Ordinal)
        || name.StartsWith("ttyACM", StringComparison.Ordinal);

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

        // Der Besitzer der Gerätedatei steht in den udev-Daten. Erst damit ist
        // die Diagnose eindeutig: sie nennt die Gruppe, die tatsächlich
        // fehlt - und nicht pauschal 'dialout'.
        var (deviceUid, deviceGid) = ReadDeviceOwner(ResolvePortName(portName));

        return new SerialPortAccess
        {
            Exists = true,
            Mode = mode is null ? null : FormatMode(mode.Value),
            ModeText = mode is null ? null : FormatModeText(mode.Value),
            Uid = uid,
            ProcessUser = ReadProcessUser(),
            ProcessGroups = groups,
            DeviceUid = deviceUid,
            DeviceGid = deviceGid,
            DeviceGroupName = deviceGid is null ? null : ReadGroupName(deviceGid.Value),
            IsContainer = DetectContainer(),
        };
    }

    /// <summary>
    /// Liest Besitzer und Gruppe einer Gerätedatei.
    /// </summary>
    /// <param name="name">Der Gerätename, z. B. <c>ttyUSB0</c>.</param>
    /// <returns>Die Kennungen - oder zwei Mal <c>null</c>.</returns>
    /// <remarks>
    /// <para>
    /// Der Weg führt über <c>sysfs</c> zur Gerätenummer
    /// (<c>MAJOR</c>/<c>MINOR</c>) und von dort nach
    /// <c>/run/udev/data/c&lt;major&gt;:&lt;minor&gt;</c>. Dort stehen in den
    /// Zeilen <c>U:</c> und <c>G:</c> die Kennungen, die udev beim Anstecken
    /// gesetzt hat.
    /// </para>
    /// <para>
    /// Im Container fehlt <c>/run/udev</c> meist - dann bleibt es bei
    /// <c>null</c> und die Diagnose nimmt <c>dialout</c> an.
    /// </para>
    /// </remarks>
    private (int? Uid, int? Gid) ReadDeviceOwner(string name)
    {
        try
        {
            var uevent = Path.Combine(_sysRoot, "class", "tty", name, "uevent");

            if (!File.Exists(uevent))
            {
                return (null, null);
            }

            int? major = null;
            int? minor = null;

            foreach (var line in File.ReadLines(uevent))
            {
                if (line.StartsWith("MAJOR=", StringComparison.Ordinal))
                {
                    major = ParseNumber(line[6..]);
                }
                else if (line.StartsWith("MINOR=", StringComparison.Ordinal))
                {
                    minor = ParseNumber(line[6..]);
                }
            }

            if (major is null || minor is null)
            {
                return (null, null);
            }

            var data = Path.Combine(
                _udevRoot,
                "data",
                "c" + major.Value.ToString(CultureInfo.InvariantCulture)
                    + ":" + minor.Value.ToString(CultureInfo.InvariantCulture));

            if (!File.Exists(data))
            {
                return (null, null);
            }

            int? uid = null;
            int? gid = null;

            foreach (var line in File.ReadLines(data))
            {
                if (line.StartsWith("U:", StringComparison.Ordinal))
                {
                    uid = ParseNumber(line[2..]);
                }
                else if (line.StartsWith("G:", StringComparison.Ordinal))
                {
                    gid = ParseNumber(line[2..]);
                }
            }

            return (uid, gid);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return (null, null);
        }
    }

    /// <summary>
    /// Sucht den Namen einer Gruppe in <c>/etc/group</c>.
    /// </summary>
    /// <param name="gid">Die Gruppenkennung.</param>
    /// <returns>Der Name (oder <c>null</c>).</returns>
    private string? ReadGroupName(int gid)
    {
        try
        {
            if (!File.Exists(_etcGroupPath))
            {
                return null;
            }

            foreach (var line in File.ReadLines(_etcGroupPath))
            {
                // „dialout:x:20:pi,norbert"
                var parts = line.Split(':');

                if (parts.Length >= 3 && ParseNumber(parts[2]) == gid)
                {
                    return parts[0];
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Ohne /etc/group bleibt der Name offen - die GID genügt.
        }

        return null;
    }

    /// <summary>
    /// Der Benutzername des laufenden Prozesses.
    /// </summary>
    /// <returns>Der Name (oder <c>null</c>).</returns>
    private static string? ReadProcessUser()
    {
        try
        {
            var name = Environment.UserName;
            return string.IsNullOrWhiteSpace(name) ? null : name;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return null;
        }
    }

    /// <summary>
    /// Läuft der Prozess in einem Container?
    /// </summary>
    /// <returns><c>true</c>, wenn eine Container-Markierung gefunden wurde.</returns>
    /// <remarks>
    /// Docker legt <c>/.dockerenv</c> an, Podman <c>/run/.containerenv</c>.
    /// Beides ist ein Hinweis, keine Zusage - für die Wahl des richtigen
    /// Lösungssatzes genügt er.
    /// </remarks>
    private static bool DetectContainer()
    {
        try
        {
            return File.Exists("/.dockerenv") || File.Exists("/run/.containerenv");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>
    /// Liest eine Zahl kultur-invariant.
    /// </summary>
    private static int? ParseNumber(string text) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : null;

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
        var directory = ResolveDirectory(devicePath)
                        ?? (Directory.Exists(devicePath) ? devicePath : null);

        for (var level = 0; level < 8 && directory is not null; level++)
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
    /// Sucht das USB-Gerät über den Gerätenamen im USB-Bus-Verzeichnis.
    /// </summary>
    /// <remarks>
    /// <c>/sys/bus/usb/devices/1-1.2/</c> ist das Gerät;
    /// <c>1-1.2:1.0/ttyUSB0</c> ist die Schnittstelle. Der Name des
    /// TTY-Geräts steht also ein oder zwei Ebenen tiefer. Dieser Weg ist der
    /// Rückfall, wenn der Symlink <c>/sys/class/tty/…/device</c> einmal nicht
    /// auflösbar ist.
    /// </remarks>
    private string? FindUsbDeviceByName(string ttyName)
    {
        var devicesRoot = Path.Combine(_sysRoot, "bus", "usb", "devices");

        try
        {
            if (!Directory.Exists(devicesRoot))
            {
                return null;
            }

            foreach (var device in Directory.EnumerateDirectories(devicesRoot))
            {
                if (!File.Exists(Path.Combine(device, "idVendor")))
                {
                    continue;
                }

                if (Directory.Exists(Path.Combine(device, ttyName)))
                {
                    return device;
                }

                foreach (var child in Directory.EnumerateDirectories(device))
                {
                    if (Directory.Exists(Path.Combine(child, ttyName)))
                    {
                        return device;
                    }
                }
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Kein Zugriff auf den USB-Baum - dann bleibt es bei den
            // Angaben, die der Name hergibt.
        }

        return null;
    }

    /// <summary>
    /// Liest die Herstellerkennung aus einem <c>by-id</c>-Namen.
    /// </summary>
    /// <remarks>
    /// <c>/dev/serial/by-id/usb-1a86_USB2.0-Serial-if00-port0</c> beginnt nach
    /// <c>usb-</c> mit der vierstelligen VID. Bei Adaptern ohne VID im Namen
    /// (z. B. FTDI) bleibt es bei <c>null</c>.
    /// </remarks>
    private static string? VendorFromStablePath(string? stablePath)
    {
        if (string.IsNullOrWhiteSpace(stablePath))
        {
            return null;
        }

        var name = Path.GetFileName(stablePath);

        if (!name.StartsWith("usb-", StringComparison.Ordinal) || name.Length < 9)
        {
            return null;
        }

        var rest = name[4..];

        // Die VID steht immer am Anfang und ist genau vier Zeichen lang.
        if (rest[4] != '_')
        {
            return null;
        }

        var vendorId = rest[..4];

        return vendorId.All(Uri.IsHexDigit) ? vendorId.ToLowerInvariant() : null;
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
    /// <remarks>
    /// Ohne Produktkennung bleibt die Herstellerfamilie
    /// (<c>1a86</c> → <c>CH34x</c>): sie ist immer noch aussagekräftig genug,
    /// um den Adapter von einem Platinen-UART zu unterscheiden.
    /// </remarks>
    private static string? ChipName(string? vendorId, string? productId)
    {
        if (string.IsNullOrWhiteSpace(vendorId))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(productId))
        {
            return vendorId.ToLowerInvariant() switch
            {
                "1a86" => "CH34x (WCH)",
                "0403" => "FTDI",
                "10c4" => "CP210x (Silicon Labs)",
                "067b" => "PL2303 (Prolific)",
                _ => null,
            };
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
