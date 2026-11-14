using ComfoBox.Gateway.Hardware;

namespace ComfoBox.Gateway.Tests.Hardware;

/// <summary>
/// Die Tests der Geräte-Erkennung am seriellen Port.
/// </summary>
/// <remarks>
/// Die Auswertung liest <c>sysfs</c> und <c>/proc/self/status</c>. Beide
/// Wurzeln sind austauschbar, damit der Test mit einem nachgebauten Baum
/// läuft - auf jedem Betriebssystem und ohne echte Hardware.
/// </remarks>
public sealed class SerialDeviceProbeTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        "comfobox-probe-" + Guid.NewGuid().ToString("N"));

    /// <inheritdoc />
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Aufräumen ist Kür.
        }
    }

    /// <summary>
    /// Ein CH340 wird als USB-Adapter mit Chipsatz-Namen erkannt.
    /// </summary>
    [Fact]
    public void Ein_Ch340_Wird_Als_Chipsatz_Erkannt()
    {
        var device = CreateUsbDevice("ttyUSB0", "1a86", "7523", "QinHeng Electronics", "USB Serial");

        var details = CreateProbe().Describe("/dev/ttyUSB0");

        Assert.Equal(SerialDeviceKinds.Usb, details.Kind);
        Assert.Equal("CH340", details.Chip);
        Assert.Equal("1a86", details.VendorId);
        Assert.Equal("7523", details.ProductId);
        Assert.Equal("QinHeng Electronics", details.Manufacturer);
        Assert.Contains("CH340", details.Label, StringComparison.Ordinal);
        Assert.Contains("1a86:7523", details.Label, StringComparison.Ordinal);
        Assert.True(Directory.Exists(device));
    }

    /// <summary>
    /// Ein unbekannter Chipsatz wird trotzdem benannt.
    /// </summary>
    [Fact]
    public void Ein_Unbekannter_Chipsatz_Wird_Trotzdem_Benannt()
    {
        CreateUsbDevice("ttyUSB1", "1234", "5678", "Beispiel GmbH", "RS485-Interface");

        var details = CreateProbe().Describe("/dev/ttyUSB1");

        Assert.Equal(SerialDeviceKinds.Usb, details.Kind);
        Assert.Null(details.Chip);
        Assert.Contains("Beispiel GmbH", details.Label, StringComparison.Ordinal);
        Assert.Contains("1234:5678", details.Label, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein UART ohne USB-Gerät gilt als „auf der Platine".
    /// </summary>
    [Fact]
    public void Ein_Uart_Ohne_Usb_Gilt_Als_Platine()
    {
        var device = Path.Combine(_root, "sys", "class", "tty", "ttyAMA0", "device");
        Directory.CreateDirectory(device);

        var details = CreateProbe().Describe("/dev/ttyAMA0");

        Assert.Equal(SerialDeviceKinds.BuiltIn, details.Kind);
        Assert.Null(details.VendorId);
        Assert.Contains("Platine", details.Label, StringComparison.Ordinal);
    }

    /// <summary>
    /// Ein unbekannter Port bleibt benennbar.
    /// </summary>
    [Fact]
    public void Ein_Unbekannter_Port_Bleibt_Benennbar()
    {
        var details = CreateProbe().Describe("COM3");

        Assert.Equal("COM3", details.Label);
        Assert.Equal(SerialDeviceKinds.Other, details.Kind);
    }

    /// <summary>
    /// Ein ttyUSB ohne sysfs-Angaben gilt trotzdem als USB-Adapter.
    /// </summary>
    /// <remarks>
    /// Regression: auf dem Pi meldete der Gateway <c>/dev/ttyUSB0</c> als
    /// „UART auf der Platine", weil <c>sysfs</c> keine Details lieferte. Der
    /// Name ist auf Linux aber eindeutig - ein <c>ttyUSB*</c> gibt es nur für
    /// USB-Seriell-Adapter.
    /// </remarks>
    [Fact]
    public void Ein_TtyUsb_Ohne_Sysfs_Gilt_Als_Usb()
    {
        var details = CreateProbe().Describe("/dev/ttyUSB0");

        Assert.Equal(SerialDeviceKinds.Usb, details.Kind);
        Assert.Contains("USB-Seriell", details.Label, StringComparison.Ordinal);
    }

    /// <summary>
    /// Das USB-Gerät wird über den USB-Bus gefunden, wenn der Symlink fehlt.
    /// </summary>
    [Fact]
    public void Das_Usb_Geraet_Wird_Ueber_Den_Bus_Gefunden()
    {
        // /sys/class/tty/ttyUSB0 existiert, 'device' aber nicht - genau der
        // Fall, in dem der Symlink nicht auflösbar ist.
        Directory.CreateDirectory(Path.Combine(_root, "sys", "class", "tty", "ttyUSB0"));

        var usbDevice = Path.Combine(_root, "sys", "bus", "usb", "devices", "1-1.2");

        // Im echten sysfs heißt die Schnittstelle '1-1.2:1.0' - der Doppelpunkt
        // ist unter Windows kein gültiges Zeichen im Verzeichnisnamen, für die
        // Auswertung aber unerheblich (es zählt nur: eine Ebene tiefer).
        var iface = Path.Combine(usbDevice, "1-1.2-1.0");
        Directory.CreateDirectory(Path.Combine(iface, "ttyUSB0"));
        File.WriteAllText(Path.Combine(usbDevice, "idVendor"), "1a86\n");
        File.WriteAllText(Path.Combine(usbDevice, "idProduct"), "7523\n");
        File.WriteAllText(Path.Combine(usbDevice, "product"), "USB2.0-Serial\n");

        var details = CreateProbe().Describe("/dev/ttyUSB0");

        Assert.Equal(SerialDeviceKinds.Usb, details.Kind);
        Assert.Equal("CH340", details.Chip);
        Assert.Equal("1a86", details.VendorId);
        Assert.Contains("1a86:7523", details.Label, StringComparison.Ordinal);
    }

    /// <summary>
    /// Die Gruppen des Prozesses werden gelesen.
    /// </summary>
    [Fact]
    public void Die_Gruppen_Des_Prozesses_Werden_Gelesen()
    {
        var probe = CreateProbe("Uid:\t1001\t1001\t1001\t1001\nGid:\t1001\t1001\t1001\t1001\nGroups:\t0 20 1001\n");
        var port = Path.Combine(_root, "ttyUSB0");
        File.WriteAllText(port, string.Empty);

        var access = probe.DescribeAccess(port);

        Assert.True(access.Exists);
        Assert.Equal(1001, access.Uid);
        Assert.Equal([0, 20, 1001], access.ProcessGroups);
        Assert.True(access.HasDialoutGroup);
    }

    /// <summary>
    /// Fehlt die Gruppe, sagt die Meldung „group_add".
    /// </summary>
    [Fact]
    public void Ohne_Dialout_Nennt_Die_Meldung_Die_Gruppe()
    {
        var access = new SerialPortAccess
        {
            Exists = true,
            Mode = "0660",
            ModeText = "crw-rw----",
            Uid = 1001,
            ProcessGroups = [0, 1001],
        };

        var details = SerialAccessMessages.PermissionDeniedDetails("/dev/ttyUSB0", access);

        Assert.Contains(details, line => line.Contains("group_add", StringComparison.Ordinal));
        Assert.DoesNotContain(details, line => line.Contains("device_cgroup_rules", StringComparison.Ordinal));
    }

    /// <summary>
    /// Ist die Gruppe vorhanden, bleibt die Device-Cgroup als Ursache.
    /// </summary>
    [Fact]
    public void Mit_Dialout_Nennt_Die_Meldung_Die_Device_Cgroup()
    {
        var access = new SerialPortAccess
        {
            Exists = true,
            Mode = "0660",
            ModeText = "crw-rw----",
            Uid = 1001,
            ProcessGroups = [0, 20, 1001],
        };

        var details = SerialAccessMessages.PermissionDeniedDetails("/dev/ttyUSB0", access);

        Assert.Contains(details, line => line.Contains("device_cgroup_rules", StringComparison.Ordinal));
        Assert.DoesNotContain(details, line => line.Contains("group_add", StringComparison.Ordinal));
    }

    /// <summary>
    /// Fehlt die Gerätedatei, ist das eine andere Ursache.
    /// </summary>
    [Fact]
    public void Eine_Fehlende_Geraetedatei_Wird_Getrennt_Gemeldet()
    {
        var access = new SerialPortAccess { Exists = false };

        var details = SerialAccessMessages.PermissionDeniedDetails("/dev/ttyUSB0", access);

        Assert.Contains(details, line => line.Contains("gibt es in diesem Container nicht", StringComparison.Ordinal));
    }

    /// <summary>
    /// Baut ein nachgebautes USB-Gerät in <c>sysfs</c>.
    /// </summary>
    private string CreateUsbDevice(
        string ttyName,
        string vendorId,
        string productId,
        string manufacturer,
        string product)
    {
        // 'device' ist im echten sysfs ein Symlink in den USB-Baum. Für die
        // Auswertung genügt ein Verzeichnis: gesucht wird das nächste
        // Verzeichnis mit 'idVendor' - und das ist hier 'device' selbst.
        var device = Path.Combine(_root, "sys", "class", "tty", ttyName, "device");
        Directory.CreateDirectory(device);

        File.WriteAllText(Path.Combine(device, "idVendor"), vendorId + "\n");
        File.WriteAllText(Path.Combine(device, "idProduct"), productId + "\n");
        File.WriteAllText(Path.Combine(device, "manufacturer"), manufacturer + "\n");
        File.WriteAllText(Path.Combine(device, "product"), product + "\n");
        File.WriteAllText(Path.Combine(device, "serial"), "ABC123\n");

        return device;
    }

    /// <summary>
    /// Erzeugt die Sonde mit den Wurzeln des Testverzeichnisses.
    /// </summary>
    private SerialDeviceProbe CreateProbe(string? procStatus = null)
    {
        Directory.CreateDirectory(_root);

        var statusPath = Path.Combine(_root, "proc-status.txt");
        File.WriteAllText(statusPath, procStatus ?? "Uid:\t1000\t1000\t1000\t1000\nGroups:\t0 20\n");

        return new SerialDeviceProbe(
            Path.Combine(_root, "sys"),
            Path.Combine(_root, "dev"),
            statusPath);
    }
}
