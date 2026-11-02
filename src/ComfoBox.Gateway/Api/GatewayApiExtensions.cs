using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Hardware;
using ComfoBox.Gateway.Logging;
using ComfoBox.Gateway.Mqtt;
using ComfoBox.Gateway.Security;
using ComfoBox.Gateway.Setup;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway.Api;

/// <summary>
/// Die Registrierung und Abbildung der REST/SSE-Schnittstelle.
/// </summary>
/// <remarks>
/// <para>
/// Die Endpunkte liegen bewusst **nicht** in
/// <see cref="GatewayServiceCollectionExtensions"/>: so kann ein Test die
/// Schnittstelle isoliert aufbauen.
/// </para>
/// <para>
/// <c>AddGatewayApi()</c> setzt voraus, dass <c>AddGatewayServices()</c>
/// aufgerufen wurde (Item-Katalog, BACnet-Gerät, Wert-Cache, MQTT-Dienste).
/// </para>
/// </remarks>
public static class GatewayApiExtensions
{
    /// <summary>
    /// Registriert alle Dienste der REST/SSE-Schnittstelle.
    /// </summary>
    /// <param name="services">Der Service-Container.</param>
    /// <param name="configuration">Die Konfiguration.</param>
    /// <param name="contentRootPath">Der Arbeitsverzeichnis-Pfad (für das Datenverzeichnis).</param>
    /// <returns>Der Service-Container.</returns>
    public static IServiceCollection AddGatewayApi(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        var dataDirectory = GatewayConfiguration.ResolveDataDirectory(configuration, contentRootPath);

        services.AddOptions<ApiOptions>()
            .Bind(configuration.GetSection(ApiOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IItemCatalog>(_ => new ItemCatalog());
        services.TryAddSingleton(sp => new MqttTopicMap(
            sp.GetRequiredService<IItemCatalog>(),
            sp.GetRequiredService<IOptions<GatewayOptions>>().Value.BaseTopic,
            sp.GetRequiredService<IOptions<GatewayOptions>>().Value.ExpertMode));

        services.AddSingleton(sp => sp.GetRequiredService<IOptions<ApiOptions>>().Value);
        services.AddSingleton(sp => new ConfigStore(dataDirectory, sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<ApiSecurityStore>();
        services.AddSingleton<ISerialPortScanner, SerialPortScanner>();
        services.AddSingleton<BacnetSerialManager>();
        services.AddSingleton(sp => new ApiSessionStore(
            sp.GetRequiredService<TimeProvider>(),
            sp.GetRequiredService<IOptions<ApiOptions>>().Value.SessionLifetimeMinutes));
        services.AddSingleton(sp => new ApiAccessGuard(
            sp.GetRequiredService<ApiSecurityStore>(),
            sp.GetRequiredService<ApiSessionStore>(),
            sp.GetRequiredService<ApiOptions>()));
        services.AddSingleton<SetupService>();
        services.AddSingleton<LogStreamBroker>();
        services.AddSingleton<RuntimeInfo>();
        services.AddSingleton<IRestartService>(sp => new ServiceControl(
            sp.GetRequiredService<IHostApplicationLifetime>(),
            sp.GetRequiredService<IOptions<GatewayOptions>>(),
            dataDirectory,
            sp.GetRequiredService<ILogger<ServiceControl>>()));

        // OpenAPI/Swagger (die Oberfläche selbst wird nur im Development-Modus
        // aktiviert - siehe MapGatewaySwagger).
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new()
            {
                Title = "ComfoBox Gateway",
                Version = MqttVersion.Current,
                Description = "Die REST-Schnittstelle des ComfoBox-MQTT-Gateways.",
            });
        });

        return services;
    }

    /// <summary>
    /// Abbildet alle Endpunkte der Schnittstelle.
    /// </summary>
    /// <param name="app">Die Web-Anwendung.</param>
    public static void MapGatewayApi(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        // Ein unbekannter API-Pfad bekommt **kein** HTML: die Schnittstelle
        // antwortet immer mit JSON (bzw. 404), damit ein Client die
        // index.html des SPA-Fallbacks nicht als „erfolgreiche Antwort"
        // fehlinterpretiert.
        app.Use(async (context, next) =>
        {
            // GetEndpoint() liefert nur dann einen Wert, wenn das Routing
            // einen Endpunkt gefunden hat - sonst ist der Pfad unbekannt.
            if (context.GetEndpoint() is null
                && context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                context.Response.ContentType = "application/json; charset=utf-8";
                await context.Response.WriteAsync(
                    """{"error":"not_found","message":"Die angeforderte API-Ressource existiert nicht."}""");
                return;
            }

            await next(context);
        });

        // Ein **ungültiges** Zugangsmittel wird immer abgelehnt - auch auf
        // den offenen Endpunkten. Sonst hält die Home-Assistant-Integration
        // einen falschen Token für gültig (sie prüft ihn über /api/status).
        app.Use(async (context, next) =>
        {
            if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
            {
                var guard = context.RequestServices.GetRequiredService<ApiAccessGuard>();
                var denied = guard.RequireValidCredential(context);

                if (denied is not null)
                {
                    await denied.ExecuteAsync(context);
                    return;
                }
            }

            await next(context);
        });

        app.MapItems();
        app.MapStatus();
        app.MapEvents();
        app.MapConfig();
        app.MapSerial();
        app.MapService();
        app.MapReconnect();
        app.MapTokens();
        app.MapAuth();
        app.MapSetup();
        app.MapLogs();
        app.MapBacnetDiagnostics();
    }

    /// <summary>
    /// Aktiviert die Swagger-Oberfläche.
    /// </summary>
    /// <param name="app">Die Web-Anwendung.</param>
    /// <remarks>
    /// Die Oberfläche ist **nur** im Development-Modus erreichbar - im
    /// Betrieb wäre sie eine unnötige Angriffsfläche.
    /// </remarks>
    public static void MapGatewaySwagger(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseSwagger();
        app.UseSwaggerUI(options =>
        {
            options.RoutePrefix = "swagger";
            options.DocumentTitle = "ComfoBox Gateway API";
        });
    }
}
