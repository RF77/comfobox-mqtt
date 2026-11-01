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
                ApiOptions api,
                ApiSecurityStore security,
                SerialPortSettings serial,
                ConfigStore store) =>
            {
                var dto = new ConfigDto
                {
                    Gateway = new GatewayConfigDto
                    {
                        BaseTopic = gateway.BaseTopic,
                        DeviceId = gateway.DeviceId,
                        ExpertMode = gateway.ExpertMode,
                        DataDirectory = gateway.DataDirectory,
                        PollIntervalSeconds = gateway.PollIntervalSeconds,
                        SecondaryPollFactor = gateway.SecondaryPollFactor,
                        MaxWritesPer24h = gateway.MaxWritesPer24h,
                        RestartEnabled = gateway.RestartEnabled,
                    },
                    Bacnet = new BacnetConfigDto
                    {
                        // Der laufende Port - nach einem Portwechsel steht hier
                        // der neue Wert.
                        Port = serial.PortName,
                        Baudrate = serial.BaudRate,
                        DeviceId = bacnet.DeviceId,
                        ThisStation = bacnet.ThisStation,
                        MaxMaster = bacnet.MaxMaster,
                        ItemTimeoutMs = bacnet.ItemTimeoutMs,
                        Retries = bacnet.Retries,
                    },
                    Mqtt = new MqttConfigDto
                    {
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
                        RequireAuthentication = api.RequireAuthentication,
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

                var changed = new List<string>();

                store.Mutate(config =>
                {
                    var gateway = ConfigStore.EnsureSection(config, GatewayOptions.SectionName);

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

                    if (request.UseMqttRetain is not null)
                    {
                        var mqtt = ConfigStore.EnsureSection(config, MqttOptions.SectionName);
                        mqtt["UseMqttRetain"] = request.UseMqttRetain.Value;
                        changed.Add("UseMqttRetain");
                    }
                });

                var logger = loggerFactory.CreateLogger("ComfoBox.Gateway.Api");
                logger.LogInformation(
                    "Die Konfiguration wurde geändert: {Changes}. Die neuen Werte werden nach einem Neustart wirksam.",
                    changed.Count == 0 ? "nichts" : string.Join(", ", changed));

                return Results.NoContent();
            })
            .WithTags("Konfiguration")
            .WithName("UpdateConfig");
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
