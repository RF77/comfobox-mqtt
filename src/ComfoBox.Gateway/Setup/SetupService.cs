using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serial;
using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Hardware;
using ComfoBox.Gateway.Security;
using MQTTnet;
using MQTTnet.Exceptions;
using MQTTnet.Formatter;

namespace ComfoBox.Gateway.Setup;

/// <summary>
/// Die Dienste des Getting-Started-Assistenten.
/// </summary>
/// <remarks>
/// <para>
/// Der Assistent prüft die Umgebung (serielle Ports, BACnet, MQTT) und
/// schreibt anschließend die <c>config.json</c>. Das Passwort wird dabei
/// **serverseitig** gehasht - im Klartext verlässt es den Gateway nur auf dem
/// Weg <c>PUT /api/setup/config</c>.
/// </para>
/// </remarks>
public sealed class SetupService
{
    /// <summary>
    /// Der Name der Datei mit den erkannten Ports (vom Supervisor erzeugt).
    /// </summary>
    public const string PortsFileName = SerialPortScanner.PortsFileName;

    private readonly ConfigStore _config;
    private readonly ApiSecurityStore _security;
    private readonly ApiOptions _apiOptions;
    private readonly BacnetOptions _bacnetOptions;
    private readonly MqttOptions _mqttOptions;
    private readonly ISerialPortScanner _scanner;
    private readonly ILogger<SetupService> _logger;
    private readonly ILoggerFactory _loggerFactory;

    /// <summary>
    /// Erzeugt den Dienst.
    /// </summary>
    /// <param name="config">Die Konfiguration.</param>
    /// <param name="security">Der Sicherheitsspeicher.</param>
    /// <param name="apiOptions">Die API-Einstellungen.</param>
    /// <param name="bacnetOptions">Die BACnet-Einstellungen.</param>
    /// <param name="mqttOptions">Die MQTT-Einstellungen.</param>
    /// <param name="scanner">Die Suche nach den seriellen Ports.</param>
    /// <param name="logger">Logger.</param>
    /// <param name="loggerFactory">Die Logger-Fabrik (für den BACnet-Stack).</param>
    public SetupService(
        ConfigStore config,
        ApiSecurityStore security,
        ApiOptions apiOptions,
        BacnetOptions bacnetOptions,
        MqttOptions mqttOptions,
        ISerialPortScanner scanner,
        ILogger<SetupService> logger,
        ILoggerFactory loggerFactory)
    {
        ArgumentNullException.ThrowIfNull(config);
        ArgumentNullException.ThrowIfNull(security);
        ArgumentNullException.ThrowIfNull(apiOptions);
        ArgumentNullException.ThrowIfNull(bacnetOptions);
        ArgumentNullException.ThrowIfNull(mqttOptions);
        ArgumentNullException.ThrowIfNull(scanner);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(loggerFactory);

        _config = config;
        _security = security;
        _apiOptions = apiOptions;
        _bacnetOptions = bacnetOptions;
        _mqttOptions = mqttOptions;
        _scanner = scanner;
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    /// <summary>
    /// Der Zustand der Ersteinrichtung.
    /// </summary>
    /// <returns>Der Zustand.</returns>
    public SetupStateDto GetState() => new()
    {
        SetupCompleted = _security.SetupCompleted,
        Steps = ["serial", "bacnet", "mqtt", "security", "done"],
        Version = 1,
    };

    /// <summary>
    /// Die erkannten seriellen Ports.
    /// </summary>
    /// <returns>
    /// Die Ports - bevorzugt aus <c>ports.json</c> (vom Supervisor erzeugt),
    /// sonst aus der Liste des Betriebssystems.
    /// </returns>
    public IReadOnlyList<SerialPortInfo> GetPorts() => _scanner.Scan();

    /// <summary>
    /// Schlägt einen freien Web-Port vor.
    /// </summary>
    /// <returns>Der Port (oder <c>0</c>, wenn keiner frei ist).</returns>
    public int SuggestPort()
    {
        for (var port = _apiOptions.SuggestedUiPort; port <= 65535; port++)
        {
            if (IsPortFree(port))
            {
                return port;
            }
        }

        return 0;
    }

    /// <summary>
    /// Prüft die BACnet-Verbindung: öffnet den Port und wartet auf ein <c>IAm</c>.
    /// </summary>
    /// <param name="port">Der serielle Port.</param>
    /// <param name="baudrate">Die Baudrate.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<TestResultDto> TestBacnetAsync(
        string? port,
        int baudrate,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(port))
        {
            return Result(false, "Es wurde kein serielle Port angegeben.", "serial_port_not_found", stopwatch);
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

        var timeout = TimeSpan.FromSeconds(_apiOptions.BacnetTestTimeoutSeconds);

        var serial = new BacnetSerialOptions
        {
            PortName = port,
            BaudRate = baudrate,
            ReadTimeoutMs = 500,
            WriteTimeoutMs = 500,
        };

        var transport = new BacnetMstpTransport(
            new SerialPortFactory(serial),
            new BacnetMstpOptions
            {
                ThisStation = (short)_bacnetOptions.ThisStation,
                MaxMaster = (byte)_bacnetOptions.MaxMaster,
                Serial = serial,
            },
            _loggerFactory.CreateLogger<BacnetMstpTransport>());

        var client = new BacnetClient(transport, _loggerFactory.CreateLogger<BacnetClient>())
        {
            Timeout = _bacnetOptions.ItemTimeoutMs,
            Retries = 1,
        };

        var iam = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        client.IamReceived += (_, _) => iam.TrySetResult(true);

        try
        {
            client.Start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(timeout);

            await client.WhoIsAsync(-1, -1, timeoutCts.Token).ConfigureAwait(false);

            var winner = await Task
                .WhenAny(iam.Task, Task.Delay(timeout, timeoutCts.Token))
                .ConfigureAwait(false);

            if (winner == iam.Task)
            {
                return Result(true, "Die ComfoBox hat geantwortet.", null, stopwatch);
            }

            return Result(false, "Die ComfoBox antwortet nicht.", "no_iam", stopwatch);
        }
        catch (OperationCanceledException)
        {
            return Result(false, "Die ComfoBox antwortet nicht.", "no_iam", stopwatch);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Result(false, $"Der Port konnte nicht geöffnet werden: {ex.Message}", "busy", stopwatch);
        }
        finally
        {
            await client.StopAsync().ConfigureAwait(false);
            client.Dispose();
        }
    }

    /// <summary>
    /// Prüft die MQTT-Verbindung: CONNECT zu dem Broker.
    /// </summary>
    /// <param name="host">Der Broker-Host.</param>
    /// <param name="port">Der Broker-Port.</param>
    /// <param name="username">Der Benutzername (oder <c>null</c>).</param>
    /// <param name="password">Das Passwort (oder <c>null</c>).</param>
    /// <param name="useTls">Soll TLS verwendet werden?</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    public async Task<TestResultDto> TestMqttAsync(
        string? host,
        int port,
        string? username,
        string? password,
        bool useTls,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();

        if (string.IsNullOrWhiteSpace(host))
        {
            return Result(false, "Es wurde kein Broker angegeben.", "mqtt_unreachable", stopwatch);
        }

        var timeout = TimeSpan.FromSeconds(_apiOptions.MqttTestTimeoutSeconds);

        var builder = new MqttClientOptionsBuilder()
            .WithClientId($"comfobox-setup-{Environment.ProcessId}")
            .WithTcpServer(host, port)
            .WithCleanStart()
            .WithKeepAlivePeriod(TimeSpan.FromSeconds(15))
            .WithProtocolVersion(MqttProtocolVersion.V500);

        if (!string.IsNullOrEmpty(username))
        {
            builder = builder.WithCredentials(username, password ?? string.Empty);
        }

        if (useTls)
        {
            builder = builder.WithTlsOptions(tls => tls.UseTls());
        }

        using var client = new MqttClientFactory().CreateMqttClient();

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(timeout);

        try
        {
            var result = await client
                .ConnectAsync(builder.Build(), timeoutCts.Token)
                .ConfigureAwait(false);

            if (client.IsConnected)
            {
                return Result(true, "Der Broker ist erreichbar.", null, stopwatch);
            }

            return Result(
                false,
                result?.ReasonString ?? "Der Broker hat die Verbindung abgelehnt.",
                "mqtt_auth_failed",
                stopwatch);
        }
        catch (OperationCanceledException)
        {
            return Result(false, "Der Broker ist nicht erreichbar (Zeitüberschreitung).", "mqtt_unreachable", stopwatch);
        }
        catch (MqttCommunicationException ex)
        {
            return Result(false, $"Der Broker ist nicht erreichbar: {ex.Message}", "mqtt_unreachable", stopwatch);
        }
        catch (Exception ex)
        {
            return Result(false, $"Die Verbindung ist fehlgeschlagen: {ex.Message}", "mqtt_unreachable", stopwatch);
        }
    }

    /// <summary>
    /// Schreibt die Konfiguration des Assistenten.
    /// </summary>
    /// <param name="request">Die Konfiguration.</param>
    /// <exception cref="ArgumentException">Wenn die Angaben unvollständig sind.</exception>
    public void WriteConfig(SetupConfigRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (string.IsNullOrWhiteSpace(request.Port))
        {
            throw new ArgumentException("Der serielle Port fehlt.", nameof(request));
        }

        if (request.Baudrate <= 0)
        {
            throw new ArgumentException("Die Baudrate muss größer als 0 sein.", nameof(request));
        }

        var adminHash = string.IsNullOrWhiteSpace(request.AdminUsername) || string.IsNullOrEmpty(request.AdminPassword)
            ? null
            : PasswordHasher.Hash(request.AdminPassword);

        // Alles in **einer** Änderung: verschachtelte Mutate-Aufrufe würden
        // sich gegenseitig überschreiben.
        _config.Mutate(config =>
        {
            var bacnet = ConfigStore.EnsureSection(config, BacnetOptions.SectionName);
            bacnet["Port"] = request.Port;
            bacnet["Baudrate"] = request.Baudrate;

            if (!string.IsNullOrWhiteSpace(request.BrokerHost))
            {
                var mqtt = ConfigStore.EnsureSection(config, MqttOptions.SectionName);
                mqtt["Brokers"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["Host"] = request.BrokerHost,
                        ["Port"] = request.BrokerPort,
                        ["Username"] = request.BrokerUsername,
                        ["Password"] = request.BrokerPassword,
                        ["UseTls"] = request.UseTls,
                        ["AllowUntrustedCertificates"] = false,
                    },
                };
            }

            if (adminHash is not null)
            {
                var api = ConfigStore.EnsureSection(config, ApiSecurityStore.SectionName);
                api["AdminUsername"] = request.AdminUsername;
                api["AdminPasswordHash"] = adminHash;
            }
        });

        _logger.LogInformation(
            "Die Konfiguration wurde vom Assistenten geschrieben (Port {Port}, Admin {Admin}).",
            request.Port,
            request.AdminUsername ?? "–");
    }

    /// <summary>
    /// Schließt die Ersteinrichtung ab.
    /// </summary>
    public void CompleteSetup()
    {
        _security.SetSetupCompleted(true);
        _logger.LogInformation("Die Ersteinrichtung ist abgeschlossen - die Setup-Endpunkte sind jetzt geschützt.");
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
    /// Ist der TCP-Port frei?
    /// </summary>
    private static bool IsPortFree(int port)
    {
        try
        {
            var listener = new TcpListener(IPAddress.Loopback, port);
            listener.Start();
            listener.Stop();
            return true;
        }
        catch (SocketException)
        {
            return false;
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
