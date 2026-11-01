using ComfoBox.Gateway.Api.Models;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using ComfoBox.Gateway.Protection;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Endpunkte für Health, Status und Diagnose.
/// </summary>
internal static class StatusEndpoints
{
    /// <summary>
    /// Registriert die Status-Endpunkte.
    /// </summary>
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapStatus(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Der Health-Endpunkt antwortet **immer** mit HTTP 200 - auch im
        // Zustand „degraded". Der Docker-Healthcheck wertet den Inhalt aus.
        app.MapGet("/api/health", (
                IHealthReporter health,
                RuntimeInfo runtime) =>
            {
                var report = new HealthReport
                {
                    State = StateOf(health.Current),
                    Message = health.Message,
                    Checks = health.Components.Values
                        .OrderBy(component => component.Name, StringComparer.Ordinal)
                        .Select(HealthCheckDto.From)
                        .ToArray(),
                    Version = MqttVersion.Current,
                    UptimeSeconds = runtime.UptimeSeconds,
                    StartedAt = runtime.StartedAt,
                };

                return Results.Ok(report);
            })
            .WithTags("Status")
            .WithName("GetHealth");

        app.MapGet("/api/status", (
                IHealthReporter health,
                RuntimeInfo runtime,
                GatewayOptions gateway,
                BacnetOptions bacnet,
                MqttOptions mqtt,
                BacnetDevice device,
                SerialPortSettings serial,
                IMqttClientProvider clients,
                IWriteCounter counter) =>
            {
                var stats = counter.GetStats();

                var report = new StatusReport
                {
                    DeviceId = gateway.DeviceId,
                    Version = MqttVersion.Current,
                    UptimeSeconds = runtime.UptimeSeconds,
                    Bacnet = new BacnetStatusDto
                    {
                        Connected = device.IsConnected,

                        // Der **laufende** Port - nicht der aus der Datei. Nach
                        // einem Portwechsel wird hier der neue stehen.
                        Port = serial.PortName,
                        Simulated = bacnet.Simulation,
                        LastError = device.LastError,
                    },
                    Mqtt = new MqttStatusDto
                    {
                        Connected = clients.IsConnected,
                        Broker = string.Join(
                            ",",
                            mqtt.Brokers.Select(broker => broker.Host)
                                .Where(host => !string.IsNullOrWhiteSpace(host))),
                    },
                    Writes = new WriteStatusDto
                    {
                        Count = stats.Count,
                        Limit = stats.Limit,
                        ResetsAt = stats.Oldest?.AddHours(24),
                    },
                };

                _ = health;
                return Results.Ok(report);
            })
            .WithTags("Status")
            .WithName("GetStatus");

        app.MapGet("/api/diagnostics/errors", (IErrorCollector errors) =>
            {
                var entries = errors.Snapshot()
                    .Select(ErrorEntryDto.From)
                    .ToArray();

                return Results.Ok(entries);
            })
            .WithTags("Diagnose")
            .WithName("GetErrors");

        app.MapGet("/api/diagnostics/writes", (IWriteCounter counter) =>
            {
                var stats = counter.GetStats();

                return Results.Ok(new WriteStatusDto
                {
                    Count = stats.Count,
                    Limit = stats.Limit,
                    ResetsAt = stats.Oldest?.AddHours(24),
                });
            })
            .WithTags("Diagnose")
            .WithName("GetWriteStats");
    }

    /// <summary>
    /// Der Name eines Health-Zustands.
    /// </summary>
    private static string StateOf(HealthState state) => state switch
    {
        HealthState.Online => "online",
        HealthState.Degraded => "degraded",
        _ => "offline",
    };
}
