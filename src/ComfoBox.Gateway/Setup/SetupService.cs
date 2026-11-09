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
using MQTTnet.Protocol;

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

    /// <summary>
    /// Der Name des Abschnitts, in dem der Supervisor seine Angaben ablegt.
    /// </summary>
    /// <remarks>
    /// Der Abschnitt wird **nur** vom Supervisor gelesen - das Gateway selbst
    /// braucht ihn nicht. Er steht trotzdem in der <c>config.json</c>, weil
    /// das der einzige Ort ist, der dem Host und dem Container gemeinsam zur
    /// Verfügung steht.
    /// </remarks>
    public const string SupervisorSectionName = "Supervisor";

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
    /// <remarks>
    /// Der Zustand enthält auch die aktuell gespeicherten Werte. Damit kann
    /// der Assistent nach der Ersteinrichtung erneut durchlaufen, ohne dass
    /// der Benutzer alles neu eingeben muss.
    /// </remarks>
    public SetupStateDto GetState() => new()
    {
        SetupCompleted = _security.SetupCompleted,
        Steps = ["welcome", "serial", "bacnet", "mqtt", "security", "done"],
        Version = 1,
        Current = ReadCurrentValues(),
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
    /// Prüft die MQTT-Verbindung: CONNECT zu dem Broker und ein
    /// Publish/Subscribe-Rundlauf auf einem Test-Topic.
    /// </summary>
    /// <param name="host">Der Broker-Host.</param>
    /// <param name="port">Der Broker-Port.</param>
    /// <param name="username">Der Benutzername (oder <c>null</c>).</param>
    /// <param name="password">Das Passwort (oder <c>null</c>).</param>
    /// <param name="useTls">Soll TLS verwendet werden?</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Das Ergebnis.</returns>
    /// <remarks>
    /// Der Test unterscheidet drei Fälle:
    /// <list type="bullet">
    /// <item>Der Broker ist nicht erreichbar (<c>mqtt_unreachable</c>);</item>
    /// <item>die Anmeldung ist fehlgeschlagen (<c>mqtt_auth_failed</c>);</item>
    /// <item>der Broker nimmt die Verbindung an, leitet aber keine Nachrichten
    /// weiter (<c>mqtt_publish_failed</c>).</item>
    /// </list>
    /// </remarks>
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

            if (!client.IsConnected)
            {
                var rejected = DescribeConnectResult(result);
                return Result(false, rejected.Message, rejected.Hint, stopwatch);
            }

            var roundTrip = await RoundTripAsync(client, timeoutCts.Token).ConfigureAwait(false);

            if (!roundTrip.Ok)
            {
                return Result(false, roundTrip.Message, "mqtt_publish_failed", stopwatch);
            }

            return Result(
                true,
                "Der Broker ist erreichbar, die Anmeldung war erfolgreich und die Nachrichten werden zugestellt.",
                null,
                stopwatch);
        }
        catch (OperationCanceledException)
        {
            return Result(false, "Der Broker ist nicht erreichbar (Zeitüberschreitung).", "mqtt_unreachable", stopwatch);
        }
        catch (MqttCommunicationException ex)
        {
            _logger.LogWarning(ex, "Der MQTT-Test ist an der Verbindung gescheitert.");

            var rejected = DescribeException(ex);
            return Result(false, rejected.Message, rejected.Hint, stopwatch);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Der MQTT-Test ist fehlgeschlagen.");

            return Result(false, $"Die Verbindung ist fehlgeschlagen: {ex.Message}", "mqtt_unreachable", stopwatch);
        }
    }

    /// <summary>
    /// Schreibt die Konfiguration des Assistenten.
    /// </summary>
    /// <param name="request">Die Konfiguration.</param>
    /// <exception cref="ArgumentException">Wenn die Angaben unvollständig sind.</exception>
    /// <remarks>
    /// Das Passwort des Administrators wird **serverseitig** gehasht - im
    /// Klartext steht es nirgends in der <c>config.json</c>.
    /// </remarks>
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

        if (!BacnetOptions.IsAllowedBaudrate(request.Baudrate))
        {
            throw new ArgumentException(
                $"Die Baudrate muss einer der folgenden Werte sein: {string.Join(", ", BacnetOptions.AllowedBaudrates)}.",
                nameof(request));
        }

        // Ein Benutzername wird nur geprüft, wenn auch ein Passwort kommt -
        // ohne Passwort bleibt der bestehende Administrator unangetastet.
        var adminHash = string.IsNullOrWhiteSpace(request.AdminUsername) || string.IsNullOrEmpty(request.AdminPassword)
            ? null
            : PasswordHasher.Hash(request.AdminPassword);

        if (adminHash is not null)
        {
            if (request.AdminUsername!.Trim().Length < 3)
            {
                throw new ArgumentException(
                    "Der Benutzername muss mindestens 3 Zeichen lang sein.",
                    nameof(request));
            }

            var hints = PasswordPolicy.Validate(request.AdminPassword);

            if (hints.Count > 0)
            {
                throw new ArgumentException(
                    $"Das Passwort entspricht nicht den Regeln: {string.Join(" ", hints)}",
                    nameof(request));
            }
        }

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

            // Die Angaben des Supervisors: er rendert daraus den
            // devices-Block der Compose-Datei.
            var supervisor = ConfigStore.EnsureSection(config, SupervisorSectionName);
            supervisor["PassAllSerialDevices"] = request.PassAllSerialDevices;

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
    /// <remarks>
    /// Sobald ein Administrator existiert, wird die Anmeldung <b>eingeschaltet</b>
    /// - die Oberfläche ist dann ohne Benutzername und Passwort nicht mehr
    /// erreichbar. Eine ausdrückliche Einstellung in der <c>config.json</c>
    /// bleibt unangetastet.
    /// </remarks>
    public void CompleteSetup()
    {
        _security.SetSetupCompleted(true);

        if (_security.HasAdmin && _security.RequireAuthenticationOverride is null)
        {
            _security.SetRequireAuthentication(true);
        }

        _logger.LogInformation("Die Ersteinrichtung ist abgeschlossen - die Setup-Endpunkte sind jetzt geschützt.");
    }

    /// <summary>
    /// Liest die aktuell gespeicherten Werte für den wiederholbaren Assistenten.
    /// </summary>
    /// <returns>Die aktuellen Werte (ohne Passwörter).</returns>
    /// <remarks>
    /// Maßgeblich ist die <c>config.json</c> - sie ist das, was nach dem
    /// Neustart gilt. Nur wenn dort nichts steht (frische Installation), kommen
    /// die Werte aus der laufenden Konfiguration.
    /// </remarks>
    private SetupCurrentValuesDto ReadCurrentValues()
    {
        var config = _config.Read();
        var bacnet = config[BacnetOptions.SectionName] as JsonObject;
        var supervisor = config[SupervisorSectionName] as JsonObject;

        var filePort = ReadString(bacnet, nameof(BacnetOptions.Port));
        var fileBaudrate = ReadInt(bacnet, nameof(BacnetOptions.Baudrate));

        var fileBroker = ReadFirstBroker(config);

        var port = string.IsNullOrWhiteSpace(filePort) ? _bacnetOptions.Port : filePort;

        return new SetupCurrentValuesDto
        {
            Port = string.IsNullOrWhiteSpace(port) ? null : port,
            Baudrate = fileBaudrate ?? _bacnetOptions.Baudrate,

            // Nur der Broker aus der Datei: der eingebaute Standard
            // ("localhost") ist keine Benutzerwahl - der Assistent soll den
            // Schritt auch überspringen können.
            BrokerHost = fileBroker?.Host,
            BrokerPort = fileBroker?.Port ?? 1883,
            BrokerUsername = fileBroker?.Username,
            AdminUsername = _security.AdminUsername,
            PassAllSerialDevices = ReadPassAllSerialDevices(supervisor),
        };
    }

    /// <summary>
    /// Liest einen Textwert aus einem JSON-Abschnitt.
    /// </summary>
    /// <param name="section">Der Abschnitt (oder <c>null</c>).</param>
    /// <param name="key">Der Schlüssel.</param>
    /// <returns>Der Wert (oder <c>null</c>).</returns>
    private static string? ReadString(JsonObject? section, string key)
    {
        var value = section?[key];

        if (value is null || value.GetValueKind() != JsonValueKind.String)
        {
            return null;
        }

        return value.GetValue<string>();
    }

    /// <summary>
    /// Liest einen Zahlenwert aus einem JSON-Abschnitt.
    /// </summary>
    /// <param name="section">Der Abschnitt (oder <c>null</c>).</param>
    /// <param name="key">Der Schlüssel.</param>
    /// <returns>Der Wert (oder <c>null</c>).</returns>
    private static int? ReadInt(JsonObject? section, string key)
    {
        var value = section?[key];

        if (value is null || value.GetValueKind() != JsonValueKind.Number)
        {
            return null;
        }

        return value.GetValue<int>();
    }

    /// <summary>
    /// Liest den ersten Broker aus der Konfiguration.
    /// </summary>
    /// <param name="config">Die Konfiguration.</param>
    /// <returns>Der Broker (oder <c>null</c>).</returns>
    private static MqttBrokerOptions? ReadFirstBroker(JsonObject config)
    {
        var brokers = config[MqttOptions.SectionName]?["Brokers"] as JsonArray;

        if (brokers is null || brokers.Count == 0)
        {
            return null;
        }

        if (brokers[0] is not JsonObject entry)
        {
            return null;
        }

        var host = ReadString(entry, nameof(MqttBrokerOptions.Host));

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        return new MqttBrokerOptions
        {
            Host = host,
            Port = ReadInt(entry, nameof(MqttBrokerOptions.Port)) ?? 1883,
            Username = ReadString(entry, nameof(MqttBrokerOptions.Username)),
        };
    }

    /// <summary>
    /// Liest die Angabe „alle seriellen Ports durchreichen".
    /// </summary>
    /// <param name="supervisor">Der Abschnitt „Supervisor" (oder <c>null</c>).</param>
    /// <returns>
    /// Der Wert - **standardmäßig wahr**, weil nur so der Verbindungstest im
    /// Assistenten funktioniert, solange der endgültige Port nicht feststeht.
    /// </returns>
    private static bool ReadPassAllSerialDevices(JsonObject? supervisor)
    {
        var value = supervisor?["PassAllSerialDevices"];

        if (value is null)
        {
            return true;
        }

        return value.GetValueKind() switch
        {
            JsonValueKind.True => true,
            JsonValueKind.False => false,
            JsonValueKind.String => bool.TryParse(value.GetValue<string>(), out var parsed) && parsed,
            _ => true,
        };
    }

    /// <summary>
    /// Führt einen Publish/Subscribe-Rundlauf auf einem Test-Topic durch.
    /// </summary>
    /// <param name="client">Der verbundene Client.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Das Ergebnis des Rundlaufs.</returns>
    /// <remarks>
    /// Der Rundlauf beweist, dass der Broker die Nachrichten tatsächlich
    /// zustellt - ein Broker, der zwar die Anmeldung erlaubt, aber keine
    /// Nachrichten weitergibt, ist für den Betrieb unbrauchbar.
    /// </remarks>
    private static async Task<(bool Ok, string Message)> RoundTripAsync(
        IMqttClient client,
        CancellationToken cancellationToken)
    {
        var topic = $"comfobox/setup/test/{Environment.ProcessId}";
        var payload = Guid.NewGuid().ToString("N");
        var received = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        Task Handler(MqttApplicationMessageReceivedEventArgs args)
        {
            var text = args.ApplicationMessage.ConvertPayloadToString();

            if (string.Equals(text, payload, StringComparison.Ordinal))
            {
                received.TrySetResult(true);
            }

            return Task.CompletedTask;
        }

        client.ApplicationMessageReceivedAsync += Handler;

        try
        {
            var subscribeOptions = new MqttClientSubscribeOptionsBuilder()
                .WithTopicFilter(new MqttTopicFilterBuilder()
                    .WithTopic(topic)
                    .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                    .Build())
                .Build();

            await client.SubscribeAsync(subscribeOptions, cancellationToken).ConfigureAwait(false);

            var publish = new MqttApplicationMessageBuilder()
                .WithTopic(topic)
                .WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtMostOnce)
                .Build();

            await client.PublishAsync(publish, cancellationToken).ConfigureAwait(false);

            var winner = await Task
                .WhenAny(received.Task, Task.Delay(TimeSpan.FromSeconds(5), cancellationToken))
                .ConfigureAwait(false);

            cancellationToken.ThrowIfCancellationRequested();

            return winner == received.Task
                ? (true, "Der Broker stellt die Nachrichten zu.")
                : (false, "Der Broker hat die Testnachricht nicht zugestellt.");
        }
        finally
        {
            client.ApplicationMessageReceivedAsync -= Handler;
        }
    }

    /// <summary>
    /// Beschreibt einen abgelehnten CONNECT.
    /// </summary>
    /// <param name="result">Das Ergebnis des CONNECT (oder <c>null</c>).</param>
    /// <returns>Die Meldung und der maschinenlesbare Hinweis.</returns>
    private static (string Message, string? Hint) DescribeConnectResult(MqttClientConnectResult? result)
    {
        var code = result?.ResultCode ?? MqttClientConnectResultCode.UnspecifiedError;

        if (IsAuthFailure(code))
        {
            return ("Die Anmeldung am Broker ist fehlgeschlagen - Benutzername und Passwort prüfen.", "mqtt_auth_failed");
        }

        var reason = string.IsNullOrWhiteSpace(result?.ReasonString) ? string.Empty : $" ({result!.ReasonString})";

        return ($"Der Broker hat die Verbindung abgelehnt: {code}{reason}", "mqtt_unreachable");
    }

    /// <summary>
    /// Beschreibt eine Kommunikationsstörung.
    /// </summary>
    /// <param name="exception">Die Ausnahme.</param>
    /// <returns>Die Meldung und der maschinenlesbare Hinweis.</returns>
    private static (string Message, string? Hint) DescribeException(MqttCommunicationException exception)
    {
        var text = exception.Message ?? string.Empty;

        if (LooksLikeAuthFailure(text))
        {
            return ("Die Anmeldung am Broker ist fehlgeschlagen - Benutzername und Passwort prüfen.", "mqtt_auth_failed");
        }

        if (LooksLikeTlsFailure(text))
        {
            return ("Die TLS-Verhandlung ist fehlgeschlagen - Zertifikat und Broker-Konfiguration prüfen.", "mqtt_tls_failed");
        }

        return ($"Der Broker ist nicht erreichbar: {text}", "mqtt_unreachable");
    }

    /// <summary>
    /// Ist der CONNECT wegen der Anmeldung abgelehnt worden?
    /// </summary>
    /// <param name="code">Der Rückgabecode des Brokers.</param>
    /// <returns><c>true</c>, wenn die Anmeldung schuld war.</returns>
    private static bool IsAuthFailure(MqttClientConnectResultCode code) => code
        is MqttClientConnectResultCode.BadUserNameOrPassword
        or MqttClientConnectResultCode.NotAuthorized
        or MqttClientConnectResultCode.Banned
        or MqttClientConnectResultCode.BadAuthenticationMethod;

    /// <summary>
    /// Sieht die Fehlermeldung nach einem Anmeldefehler aus?
    /// </summary>
    /// <param name="message">Die Fehlermeldung.</param>
    /// <returns><c>true</c>, wenn die Anmeldung schuld war.</returns>
    private static bool LooksLikeAuthFailure(string message)
    {
        var text = message.ToLowerInvariant();

        return text.Contains("badusernameorpassword", StringComparison.Ordinal)
            || text.Contains("notauthorized", StringComparison.Ordinal)
            || text.Contains("bad user name", StringComparison.Ordinal)
            || text.Contains("bad username", StringComparison.Ordinal)
            || text.Contains("not authorized", StringComparison.Ordinal)
            || text.Contains("unauthorized", StringComparison.Ordinal)
            || text.Contains("authentication", StringComparison.Ordinal)
            || text.Contains("banned", StringComparison.Ordinal);
    }

    /// <summary>
    /// Sieht die Fehlermeldung nach einem TLS-Problem aus?
    /// </summary>
    /// <param name="message">Die Fehlermeldung.</param>
    /// <returns><c>true</c>, wenn TLS schuld war.</returns>
    private static bool LooksLikeTlsFailure(string message)
    {
        var text = message.ToLowerInvariant();

        return text.Contains("certificate", StringComparison.Ordinal)
            || text.Contains("tls", StringComparison.Ordinal)
            || text.Contains("ssl", StringComparison.Ordinal);
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
