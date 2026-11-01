using ComfoBox.Bacnet;
using ComfoBox.Bacnet.Serial;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Bacnet;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Logging;
using ComfoBox.Gateway.Mqtt;
using ComfoBox.Gateway.Protection;
using Microsoft.Extensions.Options;

namespace ComfoBox.Gateway;

/// <summary>
/// Die Registrierung der Dienste des Gateways.
/// </summary>
/// <remarks>
/// Die Registrierung steht bewusst **nicht** in <c>Program.cs</c>, sondern
/// hier: so kann ein Test den kompletten DI-Container aufbauen und prüfen,
/// dass der Gateway tatsächlich startet.
/// </remarks>
public static class GatewayServiceCollectionExtensions
{
    /// <summary>
    /// Registriert alle Dienste des Gateways.
    /// </summary>
    /// <param name="services">Der Service-Container.</param>
    /// <param name="configuration">Die Konfiguration.</param>
    /// <param name="contentRootPath">Der Arbeitsverzeichnis-Pfad (für das Datenverzeichnis).</param>
    /// <param name="logBuffer">Der In-Memory-Logpuffer.</param>
    /// <param name="startBackgroundServices">
    /// Sollen die Hintergrunddienste (BACnet-Polling, MQTT-Verbindung) laufen?
    /// Ein Test schaltet sie ab - er darf weder den seriellen Port öffnen
    /// noch sich mit einem Broker verbinden.
    /// </param>
    /// <returns>Der Service-Container.</returns>
    public static IServiceCollection AddGatewayServices(
        this IServiceCollection services,
        IConfiguration configuration,
        string contentRootPath,
        InMemoryLogSink logBuffer,
        bool startBackgroundServices = true)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(logBuffer);

        // -------------------------------------------------------------------
        // Konfiguration: binden, validieren, bei Start durchsetzen.
        // -------------------------------------------------------------------
        services.AddOptions<GatewayOptions>()
            .Bind(configuration.GetSection(GatewayOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(o => o.BaseTopic == "ComfoBox" || !o.BaseTopic.Contains('/'), "Gateway:BaseTopic darf kein '/' enthalten.")
            .ValidateOnStart();

        services.AddOptions<BacnetOptions>()
            .Bind(configuration.GetSection(BacnetOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                o => BacnetOptions.IsAllowedBaudrate(o.Baudrate),
                $"Bacnet:Baudrate muss einer der folgenden Werte sein: {string.Join(", ", BacnetOptions.AllowedBaudrates)}.")
            .ValidateOnStart();

        services.AddOptions<MqttOptions>()
            .Bind(configuration.GetSection(MqttOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(
                o => MqttOptions.HasValidBroker(o),
                "Mqtt:Brokers muss mindestens einen Broker mit Host und Port enthalten.")
            .ValidateOnStart();

        services.AddOptions<LoggingOptions>()
            .Bind(configuration.GetSection(LoggingOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        // Die Options-Klassen selbst als Singleton - mehrere Dienste brauchen sie
        // direkt (nicht nur über IOptions<T>).
        //
        // Wichtig: jeder Typ, der als Parameter eines Minimal-API-Endpunkts
        // auftaucht, muss hier registriert sein. Sonst hält die
        // RequestDelegateFactory den Parameter für einen Request-Body - und
        // ein GET mit Body-Parameter ist ein Fehler ("Body was inferred but
        // the method does not allow inferred body parameters").
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<GatewayOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<BacnetOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<MqttOptions>>().Value);
        services.AddSingleton(sp => sp.GetRequiredService<IOptions<LoggingOptions>>().Value);

        // -------------------------------------------------------------------
        // Kern
        // -------------------------------------------------------------------
        var dataDirectory = ResolveDataDirectory(contentRootPath, configuration);

        services.AddSingleton<IItemCatalog>(_ => new ItemCatalog());
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton(logBuffer);
        services.AddSingleton<IErrorCollector>(sp => new ErrorCollector(sp.GetRequiredService<TimeProvider>()));
        services.AddSingleton<IHealthReporter, HealthReporter>();

        // Die zur Laufzeit geltenden Parameter der seriellen Schnittstelle.
        // Sie starten mit den Werten aus der Konfiguration und werden bei
        // einem Portwechsel aus der Oberfläche heraus aktualisiert.
        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BacnetOptions>>().Value;

            return new SerialPortSettings(options.Port, options.Baudrate);
        });
        services.AddSingleton<IValueCache>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;

            // Ein Wert gilt erst als veraltet, wenn er drei Poll-Zyklen alt ist -
            // sekundäre Items werden nur alle SecondaryPollFactor Zyklen gelesen.
            var staleAfter = TimeSpan.FromSeconds(
                options.PollIntervalSeconds * options.SecondaryPollFactor * 3);

            return new ValueCache(sp.GetRequiredService<TimeProvider>(), staleAfter);
        });
        services.AddSingleton<IWriteCounter>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;
            var path = Path.Combine(dataDirectory, WriteCounter.FileName);

            return new WriteCounter(path, options.MaxWritesPer24h, sp.GetRequiredService<TimeProvider>());
        });
        services.AddSingleton<IBacnetClient>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<BacnetOptions>>().Value;
            var settings = sp.GetRequiredService<SerialPortSettings>();

            var serial = new BacnetSerialOptions
            {
                PortName = options.Port,
                BaudRate = options.Baudrate,
                ReadTimeoutMs = options.ItemTimeoutMs,
                WriteTimeoutMs = options.ItemTimeoutMs,
            };

            // Die Fabrik liest ihre Parameter **bei jedem Öffnen** aus den
            // SerialPortSettings - so kann der Port zur Laufzeit gewechselt
            // werden, ohne dass der Gateway neu startet.
            var transport = new BacnetMstpTransport(
                new GatewaySerialPortFactory(settings, options.ItemTimeoutMs),
                new BacnetMstpOptions
                {
                    ThisStation = (short)options.ThisStation,
                    MaxMaster = (byte)options.MaxMaster,
                    Serial = serial,
                },
                sp.GetRequiredService<ILogger<BacnetMstpTransport>>());

            var client = new BacnetClient(transport, sp.GetRequiredService<ILogger<BacnetClient>>())
            {
                Timeout = options.ItemTimeoutMs,
                Retries = options.Retries,
            };

            return client;
        });
        services.AddSingleton<BacnetDevice>();

        if (startBackgroundServices)
        {
            services.AddHostedService<BacnetPollingService>();
        }

        // -------------------------------------------------------------------
        // MQTT (Phase 3): Verbindungen, Publisher, Subscriber, Discovery, Status.
        // -------------------------------------------------------------------
        services.AddSingleton(sp =>
        {
            var gateway = sp.GetRequiredService<IOptions<GatewayOptions>>().Value;

            return new MqttTopicMap(
                sp.GetRequiredService<IItemCatalog>(),
                gateway.BaseTopic,
                gateway.ExpertMode);
        });

        services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<MqttOptions>>().Value;

            return new MqttConnectionFactory(options, sp.GetRequiredService<MqttTopicMap>());
        });

        services.AddSingleton<MqttClientRegistry>();
        services.AddSingleton<IMqttClientProvider>(sp => sp.GetRequiredService<MqttClientRegistry>());
        services.AddSingleton<MqttConnectionManager>();
        services.AddSingleton<IMqttPublisher, MqttPublisher>();
        services.AddSingleton<MqttSubscriber>();
        services.AddSingleton<HomeAssistantDiscovery>();
        services.AddSingleton<MqttStatePublisher>();
        services.AddSingleton<MqttStatusPublisher>();
        services.AddSingleton<HomeAssistantBirthHandler>();

        services.AddSingleton<IMqttConnectionHandler>(sp => sp.GetRequiredService<MqttSubscriber>());
        services.AddSingleton<IMqttConnectionHandler>(sp => sp.GetRequiredService<HomeAssistantDiscovery>());
        services.AddSingleton<IMqttConnectionHandler>(sp => sp.GetRequiredService<MqttStatePublisher>());
        services.AddSingleton<IMqttConnectionHandler>(sp => sp.GetRequiredService<MqttStatusPublisher>());
        services.AddSingleton<IMqttConnectionHandler>(sp => sp.GetRequiredService<HomeAssistantBirthHandler>());

        if (startBackgroundServices)
        {
            services.AddHostedService(sp => sp.GetRequiredService<MqttStatePublisher>());
            services.AddHostedService(sp => sp.GetRequiredService<MqttStatusPublisher>());
            services.AddHostedService(sp => sp.GetRequiredService<MqttConnectionManager>());
        }

        return services;
    }

    /// <summary>
    /// Ermittelt das Datenverzeichnis (Schreibzähler, später die Setup-Konfiguration).
    /// </summary>
    /// <param name="contentRootPath">Der Arbeitsverzeichnis-Pfad.</param>
    /// <param name="configuration">Die Konfiguration.</param>
    /// <returns>Der absolute Pfad.</returns>
    private static string ResolveDataDirectory(string contentRootPath, IConfiguration configuration)
    {
        var configured = configuration.GetSection(GatewayOptions.SectionName)[nameof(GatewayOptions.DataDirectory)]
            ?? new GatewayOptions().DataDirectory;

        return Path.IsPathRooted(configured)
            ? configured
            : Path.Combine(contentRootPath, configured);
    }
}
