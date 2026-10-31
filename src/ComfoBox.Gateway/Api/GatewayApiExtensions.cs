using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Configuration;
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
        services.AddSingleton(sp => new ApiAccessGuard(
            sp.GetRequiredService<ApiSecurityStore>(),
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
    /// <param name="app">Der Endpoint-Builder.</param>
    public static void MapGatewayApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapItems();
        app.MapStatus();
        app.MapEvents();
        app.MapConfig();
        app.MapService();
        app.MapTokens();
        app.MapSetup();
        app.MapLogs();
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
