using System.Diagnostics;
using System.IO.Ports;
using ComfoBox.Bacnet;
using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;

namespace ComfoBox.Gateway.Bacnet;

/// <summary>
/// Die Verwaltung der seriellen Schnittstelle.
/// </summary>
/// <remarks>
/// <para>
/// Der Manager ist die Schnittstelle zwischen der Oberfläche und dem
/// BACnet-Stack: er sucht Ports, prüft sie und stellt die Schnittstelle auf
/// einen neuen Port um - **ohne** dass der Gateway neu gestartet werden muss.
/// </para>
/// <para>
/// Das Umstellen besteht aus drei Schritten:
/// </para>
/// <list type="number">
/// <item>die Konfiguration (<c>config.json</c>) wird geschrieben - der Wert
/// überlebt einen Neustart;</item>
/// <item>die <see cref="SerialPortSettings"/> werden aktualisiert - der
/// Transport öffnet den Port mit den neuen Werten;</item>
/// <item>der BACnet-Client baut die Verbindung neu auf.</item>
/// </list>
/// </remarks>
public sealed class BacnetSerialManager
{
    /// <summary>
    /// Der Name der Komponente im Health-Report.
    /// </summary>
    public const string HealthComponentName = "Bacnet";

    private readonly ConfigStore _config;
    private readonly SerialPortSettings _settings;
    private readonly IBacnetClient _client;
    private readonly BacnetDevice _device;
    private readonly IHealthReporter _health;
    private readonly ILogger<BacnetSerialManager> _logger;

    /// <summary>
    /// Erzeugt den Manager.
    /// </summary>
    /// <param name="config">Die Konfiguration.</param>
    /// <param name="settings">Die aktuellen Parameter der Schnittstelle.</param>
    /// <param name="client">Der BACnet-Client (für den Neuaufbau).</param>
    /// <param name="device">Das BACnet-Gerät (für den Zustand).</param>
    /// <param name="health">Der Health-Reporter.</param>
    /// <param name="logger">Logger.</param>
    public BacnetSerialManager(
        ConfigStore config,
        SerialPortSettings settings,
        IBacnetClient client,
        BacnetDevice device,
        IHealthReporter health,
        ILogger<BacnetSerialManager> logger)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(settings);
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(device);
        ArgumentNullException.ThrowIfNull(health);
        ArgumentNullException.ThrowIfNull(logger);

        _config = config;
        _settings = settings;
        _client = client;
        _device = device;
        _health = health;
        _logger = logger;
    }

    /// <summary>
    /// Der aktuell eingestellte Port.
    /// </summary>
    public string PortName => _settings.PortName;

    /// <summary>
    /// Die aktuell eingestellte Baudrate.
    /// </summary>
    public int BaudRate => _settings.BaudRate;

    /// <summary>
    /// Prüft einen Port, ohne die Einstellung zu ändern.
    /// </summary>
    /// <param name="port">Der zu prüfende Port (oder <c>null</c>).</param>
    /// <param name="baudrate">Die Baudrate.</param>
    /// <returns>Das Ergebnis.</returns>
    /// <remarks>
    /// <para>
    /// Der **aktive** Port kann nicht geöffnet werden - der Gateway hält ihn
    /// selbst. In dem Fall wird der aktuelle Zustand gemeldet (die ComfoBox
    /// antwortet oder antwortet nicht).
    /// </para>
    /// <para>
    /// Jeder andere Port wird kurz geöffnet und wieder geschlossen: so
    /// sieht der Benutzer sofort, ob der Adapter vorhanden und frei ist.
    /// </para>
    /// </remarks>
    public TestResultDto Test(string? port, int baudrate)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(port))
        {
            return Result(false, "Es wurde kein serieller Port angegeben.", "serial_port_not_found", stopwatch);
        }

        if (!BacnetOptions.IsAllowedBaudrate(baudrate))
        {
            return Result(
                false,
                $"Die Baudrate {baudrate} wird von BACnet nicht unterstützt.",
                "invalid_baudrate",
                stopwatch);
        }

        if (string.Equals(port, _settings.PortName, StringComparison.OrdinalIgnoreCase))
        {
            return CurrentState(port, stopwatch);
        }

        var probe = ProbeSerialPort(port, baudrate);
        if (probe is not null)
        {
            return Result(
                false,
                probe == "permission_denied"
                    ? $"Keine Berechtigung für den Port '{port}' (Gruppe 'dialout' prüfen)."
                    : probe == "busy"
                        ? $"Der Port '{port}' wird von einem anderen Prozess gehalten."
                        : $"Der Port '{port}' wurde nicht gefunden.",
                probe,
                stopwatch);
        }

        return Result(
            true,
            $"Der Port '{port}' ist vorhanden und frei. Ob die ComfoBox antwortet, zeigt der Verbindungstest.",
            null,
            stopwatch);
    }

    /// <summary>
    /// Stellt die serielle Schnittstelle auf einen neuen Port um.
    /// </summary>
    /// <param name="port">Der neue Port.</param>
    /// <param name="baudrate">Die neue Baudrate.</param>
    /// <exception cref="ArgumentException">Wenn die Angaben ungültig sind.</exception>
    /// <remarks>
    /// Der neue Port ist **sofort** wirksam - ein Neustart ist nicht nötig.
    /// </remarks>
    public void Apply(string? port, int baudrate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(port);

        if (!BacnetOptions.IsAllowedBaudrate(baudrate))
        {
            throw new ArgumentException(
                $"Die Baudrate muss einer der folgenden Werte sein: {string.Join(", ", BacnetOptions.AllowedBaudrates)}.",
                nameof(baudrate));
        }

        var previousPort = _settings.PortName;
        var previousBaudrate = _settings.BaudRate;

        // 1. Persistieren - der Wert überlebt einen Neustart.
        _config.Mutate(config =>
        {
            var bacnet = ConfigStore.EnsureSection(config, BacnetOptions.SectionName);
            bacnet["Port"] = port;
            bacnet["Baudrate"] = baudrate;
        });

        // 2. Zur Laufzeit übernehmen.
        _settings.Update(port, baudrate);

        // 3. Die Verbindung neu aufbauen - ohne Neustart.
        _client.RequestReconnect();

        _logger.LogInformation(
            "Die serielle Schnittstelle wurde umgestellt: {PreviousPort} @ {PreviousBaudrate} -> {Port} @ {Baudrate}.",
            previousPort,
            previousBaudrate,
            port,
            baudrate);
    }

    /// <summary>
    /// Meldet den aktuellen Zustand der Schnittstelle.
    /// </summary>
    private TestResultDto CurrentState(string port, Stopwatch stopwatch)
    {
        // Nur ein **erfolgreicher** Buszugriff gilt als „verbunden" - der
        // Health-Reporter ist da zu optimistisch (er meldet „online", obwohl
        // die Heizung nicht antwortet).
        if (_device.IsConnected)
        {
            return Result(true, $"Die ComfoBox antwortet auf '{port}'.", null, stopwatch);
        }

        return Result(false, DescribeError(), "not_connected", stopwatch);
    }

    /// <summary>
    /// Beschreibt den letzten Fehler der Schnittstelle.
    /// </summary>
    private string DescribeError()
    {
        var component = _health.Components.TryGetValue(HealthComponentName, out var value) ? value : null;
        var health = component?.Message;

        return _device.LastError switch
        {
            "timeout" => "Die ComfoBox antwortet nicht (Zeitüberschreitung). Bitte Port und Baudrate prüfen.",
            "read_failed" => "Die ComfoBox antwortet nicht (Lesefehler). Bitte Port und Baudrate prüfen.",
            "write_failed" => "Die ComfoBox antwortet nicht (Schreibfehler). Bitte Port und Baudrate prüfen.",
            not null => $"Die ComfoBox antwortet nicht ({_device.LastError}).",
            null => string.IsNullOrWhiteSpace(health)
                ? "Der BACnet-Bus ist nicht erreichbar."
                : health,
        };
    }

    /// <summary>
    /// Prüft, ob der Port geöffnet werden kann.
    /// </summary>
    /// <returns>Der Fehlercode (oder <c>null</c>, wenn der Port nutzbar ist).</returns>
    private static string? ProbeSerialPort(string portName, int baudRate)
    {
        try
        {
            using var serial = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
            {
                ReadTimeout = 500,
                WriteTimeout = 500,
            };

            serial.Open();
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            // Unter Linux (Unix) meldet .NET eine UnauthorizedAccessException,
            // wenn die **Gerätedatei fehlt** – nicht nur, wenn sie dem
            // Benutzer nicht gehört. Deshalb erst prüfen, ob es sie überhaupt
            // gibt: „serial_port_not_found" (nicht vorhanden) und
            // „permission_denied" (vorhanden, aber kein Zugriff) sind
            // verschiedene Fehler mit verschiedenen Lösungen.
            //
            // Unter Windows liefert `SerialPort.Open` für einen nicht
            // vorhandenen Port eine ArgumentException (siehe unten) – dort
            // ist `File.Exists` für COM-Ports ohnehin unbrauchbar.
            if (!OperatingSystem.IsWindows() && !File.Exists(portName))
            {
                return "serial_port_not_found";
            }

            return "permission_denied";
        }
        catch (ArgumentException)
        {
            return "serial_port_not_found";
        }
        catch (IOException)
        {
            return "busy";
        }
    }

    /// <summary>
    /// Erzeugt das Ergebnis eines Tests.
    /// </summary>
    private static TestResultDto Result(bool ok, string message, string? hint, Stopwatch stopwatch) => new()
    {
        Ok = ok,
        Message = message,
        Hint = hint,
        DurationMs = stopwatch.ElapsedMilliseconds,
    };
}
