using System.Text.Json.Nodes;
using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Security;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte der Konfiguration.
/// </summary>
internal static class ConfigEndpoints
{
    /// <summary>
    /// Registriert die Konfigurations-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapConfig(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet("/api/config", (
                GatewayOptions gateway,
                BacnetOptions bacnet,
                MqttOptions mqtt,
                LoggingOptions logging,
                ApiSecurityStore security,
                ApiAccessGuard accessGuard,
                SerialPortSettings serial,
                BacnetRuntimeSettings bacnetRuntime,
                GatewayRuntimeSettings gatewayRuntime,
                ConfigStore store) =>
            {
                var dto = new ConfigDto
                {
                    Gateway = new GatewayConfigDto
                    {
                        BaseTopic = gateway.BaseTopic,
                        DeviceId = gateway.DeviceId,

                        // Die änderbaren Werte kommen aus der config.json.
                        // Die Options sind der Stand vom **Start**: wer hier
                        // die Options liest, meldet dem Benutzer den alten
                        // Wert zurück - und die Oberfläche springt nach dem
                        // Speichern scheinbar zurück.
                        ExpertMode = gatewayRuntime.ExpertMode,
                        DataDirectory = gateway.DataDirectory,
                        PollIntervalSeconds = Persisted(store, GatewayOptions.SectionName, "PollIntervalSeconds", gateway.PollIntervalSeconds),
                        SecondaryPollFactor = gateway.SecondaryPollFactor,
                        MaxWritesPer24h = Persisted(store, GatewayOptions.SectionName, "MaxWritesPer24h", gateway.MaxWritesPer24h),
                        RestartEnabled = gateway.RestartEnabled,
                    },
                    Bacnet = new BacnetConfigDto
                    {
                        // Der laufende Port - nach einem Portwechsel steht hier
                        // der neue Wert.
                        Port = serial.PortName,
                        Baudrate = serial.BaudRate,
                        DeviceId = bacnetRuntime.DeviceId,
                        ThisStation = Persisted(store, BacnetOptions.SectionName, "ThisStation", bacnet.ThisStation),
                        MaxMaster = bacnet.MaxMaster,
                        ItemTimeoutMs = bacnetRuntime.ItemTimeoutMs,
                        Retries = bacnet.Retries,
                        WritePriority = Persisted(store, BacnetOptions.SectionName, "WritePriority", bacnet.WritePriority),
                    },
                    Mqtt = new MqttConfigDto
                    {
                        Enabled = Persisted(store, MqttOptions.SectionName, "Enabled", mqtt.Enabled),
                        ClientId = mqtt.ClientId,
                        KeepAliveSeconds = mqtt.KeepAliveSeconds,
                        DiscoveryPrefix = mqtt.DiscoveryPrefix,
                        EnableDiscovery = mqtt.EnableDiscovery,
                        UseMqttRetain = mqtt.UseMqttRetain,
                        StatusIntervalSeconds = mqtt.StatusIntervalSeconds,
                        Brokers = mqtt.Brokers
                            .Select(broker => new MqttBrokerConfigDto
                            {
                                Host = broker.Host,
                                Port = broker.Port,
                                Username = broker.Username,

                                // Der Wert selbst wird **nie** ausgegeben.
                                PasswordSet = !string.IsNullOrEmpty(broker.Password),
                                UseTls = broker.UseTls,
                                AllowUntrustedCertificates = broker.AllowUntrustedCertificates,
                            })
                            .ToArray(),
                    },
                    Logging = new LoggingConfigDto
                    {
                        Directory = logging.Directory,
                        RetainedFileCount = logging.RetainedFileCount,
                        RingBufferCapacity = logging.RingBuffer.Capacity,
                        RingBufferMinimumLevel = logging.RingBuffer.MinimumLevel,
                    },
                    Api = new ApiConfigDto
                    {
                        RequireAuthentication = accessGuard.AuthenticationRequired,
                        SetupCompleted = security.SetupCompleted,
                        AdminUsername = security.AdminUsername,
                        TokenCount = security.Tokens.Count,
                    },
                    Source = DetermineSource(store),
                };

                return Results.Ok(dto);
            })
            .WithTags("Konfiguration")
            .WithName("GetConfig");

        app.MapPut("/api/config", (
                HttpContext context,
                ConfigUpdateRequest request,
                ConfigStore store,
                BacnetRuntimeSettings bacnetRuntime,
                GatewayRuntimeSettings gatewayRuntime,
                ApiAccessGuard guard,
                ILoggerFactory loggerFactory) =>
            {
                var denied = guard.RequireAdmin(context);
                if (denied is not null)
                {
                    return denied;
                }

                if (request is null)
                {
                    return Results.Json(
                        new ApiError { Error = "invalid_request", Message = "Es wurde keine Konfiguration übergeben." },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (request.PollIntervalSeconds is < 1 or > 3600)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Das Poll-Intervall muss zwischen 1 und 3600 Sekunden liegen.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (request.MaxWritesPer24h is < 0 or > 100_000)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Das Limit der Schreibvorgänge muss zwischen 0 und 100000 liegen.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (request.DeviceId is < BacnetRuntimeSettings.MinDeviceId or > BacnetRuntimeSettings.MaxDeviceId)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = $"Die Geräte-ID muss zwischen {BacnetRuntimeSettings.MinDeviceId} und "
                                      + $"{BacnetRuntimeSettings.MaxDeviceId} liegen.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (request.ItemTimeoutMs is < BacnetRuntimeSettings.MinItemTimeoutMs
                    or > BacnetRuntimeSettings.MaxItemTimeoutMs)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = $"Das Zeitfenster muss zwischen {BacnetRuntimeSettings.MinItemTimeoutMs} und "
                                      + $"{BacnetRuntimeSettings.MaxItemTimeoutMs} ms liegen.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (request.ThisStation is < 0 or > 254)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Die MSTP-Adresse muss zwischen 0 und 254 liegen.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                if (request.WritePriority is < 0 or > 16)
                {
                    return Results.Json(
                        new ApiError
                        {
                            Error = "invalid_request",
                            Message = "Die Schreibpriorität muss zwischen 0 und 16 liegen.",
                        },
                        statusCode: StatusCodes.Status400BadRequest);
                }

                var changed = new List<string>();

                store.Mutate(config =>
                {
                    var gateway = ConfigStore.EnsureSection(config, GatewayOptions.SectionName);
                    var bacnet = ConfigStore.EnsureSection(config, BacnetOptions.SectionName);

                    if (request.ExpertMode is not null)
                    {
                        gateway["ExpertMode"] = request.ExpertMode.Value;
                        changed.Add("ExpertMode");
                    }

                    if (request.PollIntervalSeconds is not null)
                    {
                        gateway["PollIntervalSeconds"] = request.PollIntervalSeconds.Value;
                        changed.Add("PollIntervalSeconds");
                    }

                    if (request.MaxWritesPer24h is not null)
                    {
                        gateway["MaxWritesPer24h"] = request.MaxWritesPer24h.Value;
                        changed.Add("MaxWritesPer24h");
                    }

                    if (request.DeviceId is not null)
                    {
                        bacnet["DeviceId"] = request.DeviceId.Value;
                        changed.Add("DeviceId");
                    }

                    if (request.ItemTimeoutMs is not null)
                    {
                        bacnet["ItemTimeoutMs"] = request.ItemTimeoutMs.Value;
                        changed.Add("ItemTimeoutMs");
                    }

                    if (request.ThisStation is not null)
                    {
                        bacnet["ThisStation"] = request.ThisStation.Value;
                        changed.Add("ThisStation");
                    }

                    if (request.WritePriority is not null)
                    {
                        bacnet["WritePriority"] = request.WritePriority.Value;
                        changed.Add("WritePriority");
                    }

                    if (request.UseMqttRetain is not null)
                    {
                        var mqtt = ConfigStore.EnsureSection(config, MqttOptions.SectionName);
                        mqtt["UseMqttRetain"] = request.UseMqttRetain.Value;
                        changed.Add("UseMqttRetain");
                    }

                    if (request.MqttEnabled is not null)
                    {
                        var mqtt = ConfigStore.EnsureSection(config, MqttOptions.SectionName);
                        mqtt["Enabled"] = request.MqttEnabled.Value;
                        changed.Add("Enabled");
                    }
                });

                // Geräte-ID und Zeitfenster gelten **sofort** - sie werden bei
                // jedem Lese- und Schreibzugriff neu gelesen. Die MSTP-Adresse
                // steckt im Zustandsautomaten und braucht einen Neustart.
                var immediate = new List<string>();

                if (request.ExpertMode is not null)
                {
                    // Der Experten-Modus wirkt sofort: er entscheidet bei jedem
                    // Schreibvorgang. Über GatewayRuntimeSettings.ExpertModeChanged
                    // zieht der ExpertModeCoordinator zusätzlich die /Set-Topics
                    // (Subscribe) und die Home-Assistant-Discovery nach - ohne
                    // Neustart. Deshalb wird hier **nur** der Laufzeitwert
                    // gesetzt und kein MQTT-Code aufgerufen.
                    gatewayRuntime.ExpertMode = request.ExpertMode.Value;
                    immediate.Add("ExpertMode");
                }

                if (request.DeviceId is not null || request.ItemTimeoutMs is not null)
                {
                    bacnetRuntime.Update(
                        request.DeviceId ?? bacnetRuntime.DeviceId,
                        request.ItemTimeoutMs ?? bacnetRuntime.ItemTimeoutMs);

                    if (request.DeviceId is not null)
                    {
                        immediate.Add("DeviceId");
                    }

                    if (request.ItemTimeoutMs is not null)
                    {
                        immediate.Add("ItemTimeoutMs");
                    }
                }

                var logger = loggerFactory.CreateLogger("ComfoBox.Gateway.Api");
                logger.LogInformation(
                    "Die Konfiguration wurde geändert: {Changes}. Sofort wirksam: {Immediate}. "
                    + "Alles Übrige wird nach einem Neustart wirksam.",
                    changed.Count == 0 ? "nichts" : string.Join(", ", changed),
                    immediate.Count == 0 ? "nichts" : string.Join(", ", immediate));

                return Results.NoContent();
            })
            .WithTags("Konfiguration")
            .WithName("UpdateConfig");
    }

    /// <summary>
    /// Liest einen gespeicherten Wert aus der <c>config.json</c>.
    /// </summary>
    /// <typeparam name="T">Der Typ des Werts.</typeparam>
    /// <param name="store">Der Konfigurationsspeicher.</param>
    /// <param name="section">Der Abschnitt, z. B. <c>Gateway</c>.</param>
    /// <param name="key">Der Schlüssel, z. B. <c>ExpertMode</c>.</param>
    /// <param name="fallback">Der Wert, wenn nichts gespeichert ist.</param>
    /// <returns>Der gespeicherte Wert - oder <paramref name="fallback"/>.</returns>
    /// <remarks>
    /// <para>
    /// <b>Warum nicht einfach die Options?</b> <c>IOptions&lt;T&gt;</c> wird
    /// beim Start gebunden und ändert sich danach nicht mehr. Ein über
    /// <c>PUT /api/config</c> geschriebener Wert steht zwar in der Datei, würde
    /// von hier aber nie zurückgemeldet - die Oberfläche lädt nach dem
    /// Speichern neu und zeigt wieder den alten Wert.
    /// </para>
    /// <para>
    /// Die Suche ist unabhängig von der Groß-/Kleinschreibung, weil
    /// <c>config.json</c> auch von Hand bearbeitet wird.
    /// </para>
    /// </remarks>
    private static T Persisted<T>(ConfigStore store, string section, string key, T fallback)
    {
        var root = store.Read();

        var sectionNode = root
            .FirstOrDefault(pair => string.Equals(pair.Key, section, StringComparison.OrdinalIgnoreCase))
            .Value as JsonObject;

        if (sectionNode is null)
        {
            return fallback;
        }

        var valueNode = sectionNode
            .FirstOrDefault(pair => string.Equals(pair.Key, key, StringComparison.OrdinalIgnoreCase))
            .Value;

        if (valueNode is JsonValue value && value.TryGetValue<T>(out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    /// <summary>
    /// Ermittelt, woher die Konfiguration kommt.
    /// </summary>
    private static string DetermineSource(ConfigStore store)
    {
        if (store.Exists)
        {
            return "file";
        }

        var hasEnvironment = Environment.GetEnvironmentVariables().Keys
            .Cast<object>()
            .Any(key => key.ToString()?.StartsWith("COMFOBOX_", StringComparison.OrdinalIgnoreCase) == true);

        return hasEnvironment ? "environment" : "defaults";
    }
}
