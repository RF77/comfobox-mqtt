using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Energy;

/// <summary>
/// Der Zustand der Wärmemengenmessung (DP 3340).
/// </summary>
/// <remarks>
/// <see cref="Unknown"/> heißt **noch nie gelesen** - ein solcher Zustand ist
/// ausdrücklich **nicht** dasselbe wie „nicht verfügbar": es wird kein
/// <c>0</c> publiziert, weil über das Gerät noch nichts bekannt ist.
/// </remarks>
public enum EnergyAvailability
{
    /// <summary>
    /// DP 3340 wurde noch nie gelesen.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Die Wärmemengenmessung ist eingeschaltet (<c>On</c> oder <c>OnSeparateWarmWater</c>).
    /// </summary>
    Available = 1,

    /// <summary>
    /// Die Wärmemengenmessung ist ausgeschaltet oder der Wert ist nicht lesbar.
    /// </summary>
    Unavailable = 2,
}

/// <summary>
/// Das Gate der Wärmemengenmessung: es entscheidet, ob die Energie-Topics
/// publiziert werden dürfen.
/// </summary>
/// <remarks>
/// <para>
/// Ohne Wärmemengenmessung rechnet der Regler keine Wärme - die kWh-Zähler
/// und der COP bleiben ohne Aussage. Sie werden deshalb **nicht als 0**
/// publiziert, sondern gesperrt und als nicht verfügbar markiert
/// (<c>Status/Energy/Available = 0</c>).
/// </para>
/// <para>
/// <see cref="Availability"/> wird **bei jedem Zugriff** aus dem Wert-Cache
/// abgeleitet (DP 3340, ein Nachschlagevorgang): so kann der
/// <see cref="MqttStatePublisher"/> nie mit einem veralteten Zustand
/// publizieren. Die Nebenwirkungen (Availability-Topic, Leeren der retained
/// Energie-Topics, Fehlermeldung) laufen dagegen **nur bei einem
/// Zustandswechsel** - <see cref="ApplyAsync"/> merkt sich den zuletzt
/// angewandten Zustand.
/// </para>
/// <para>
/// Dieses Gate schreibt **nie** auf DP 3340: der Gateway kann nicht prüfen,
/// ob ein Durchflussfühler installiert ist. Das Einschalten bleibt eine
/// Entscheidung am Regler.
/// </para>
/// </remarks>
public sealed class EnergyMeasurementGate : BackgroundService, IMqttConnectionHandler
{
    /// <summary>
    /// Die Id des Katalog-Items, das die Betriebsart der Wärmemengenmessung trägt.
    /// </summary>
    public const string MeasurementModeId = "status.energy.measurement_mode";

    /// <summary>
    /// Die Quelle der Fehlermeldungen in der Fehler-Senke.
    /// </summary>
    public const string Source = "EnergyMeasurementGate";

    /// <summary>
    /// Der Fehlercode für „Wärmemengenmessung am Gerät ausgeschaltet" (DP 3340 = 0).
    /// </summary>
    public const string DisabledCode = "energy_measurement_disabled";

    /// <summary>
    /// Der Fehlercode für „Wert der Wärmemengenmessung nicht lesbar oder veraltet".
    /// </summary>
    public const string UnavailableCode = "energy_measurement_unavailable";

    /// <summary>
    /// Die Meldung zu <see cref="DisabledCode"/> (wörtlich aus der Spezifikation).
    /// </summary>
    public const string DisabledMessage =
        "Die Wärmemengenmessung ist am Gerät ausgeschaltet (DP 3340 = 0). Die kWh-Zähler und der\n"
        + "COP liefern deshalb keine Werte. Am Regler unter §6.3.14 „Wärmemengenmessung“ auf 1 oder 2\n"
        + "stellen und den Durchflussfühler prüfen (Status/Energy/FlowRate muss bei laufender Pumpe\n"
        + "größer 0 sein).";

    /// <summary>
    /// Die Meldung zu <see cref="UnavailableCode"/>.
    /// </summary>
    public const string UnavailableMessage =
        "Die Betriebsart der Wärmemengenmessung (DP 3340) ist nicht lesbar oder veraltet. Die kWh-Zähler\n"
        + "und der COP werden deshalb nicht publiziert. BACnet-Bus und Verbindung zur ComfoBox prüfen -\n"
        + "der Wert muss spätestens nach einem Poll-Zyklus wieder vorliegen.";

    private const string AvailablePayload = "1";
    private const string UnavailablePayload = "0";

    private readonly IValueCache _cache;
    private readonly IItemCatalog _catalog;
    private readonly MqttTopicMap _topics;
    private readonly IMqttPublisher _publisher;
    private readonly IErrorCollector _errors;
    private readonly MqttOptions _options;
    private readonly ILogger<EnergyMeasurementGate> _logger;

    /// <summary>
    /// Der Zustand, für den die Nebenwirkungen zuletzt ausgeführt wurden.
    /// </summary>
    /// <remarks>
    /// Nur dieser Wert entscheidet über „einmal pro Wechsel" - die
    /// veröffentlichte Verfügbarkeit selbst wird aus dem Cache abgeleitet.
    /// </remarks>
    private int _applied = (int)EnergyAvailability.Unknown;

    /// <summary>
    /// Erzeugt das Gate.
    /// </summary>
    /// <param name="cache">Der Wert-Cache.</param>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="publisher">Der Publisher.</param>
    /// <param name="errors">Die Fehler-Senke.</param>
    /// <param name="options">Die MQTT-Einstellungen.</param>
    /// <param name="logger">Logger.</param>
    public EnergyMeasurementGate(
        IValueCache cache,
        IItemCatalog catalog,
        MqttTopicMap topics,
        IMqttPublisher publisher,
        IErrorCollector errors,
        MqttOptions options,
        ILogger<EnergyMeasurementGate> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(errors);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        _cache = cache;
        _catalog = catalog;
        _topics = topics;
        _publisher = publisher;
        _errors = errors;
        _options = options;
        _logger = logger;
    }

    /// <summary>
    /// Die aktuelle Verfügbarkeit der Energiemessung.
    /// </summary>
    /// <remarks>
    /// Der Wert wird bei jedem Zugriff aus dem Cache abgeleitet und ist damit
    /// immer aktuell - auch für einen Publish, der gerade zwischen zwei
    /// Cache-Änderungen läuft.
    /// </remarks>
    public EnergyAvailability Availability => ReadAvailability();

    /// <summary>
    /// Dürfen die Energie-Topics (kWh, COP) gerade publiziert werden?
    /// </summary>
    public bool IsAvailable => Availability == EnergyAvailability.Available;

    /// <summary>
    /// Die Items, die ohne Wärmemengenmessung gesperrt sind (11 Stück).
    /// </summary>
    public IReadOnlyList<ItemDefinition> GatedItems
        => [.. _catalog.All.Where(static item => item.RequiresEnergyMeasurement)];

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Beim Start wird ein alter "1"-Wert der vorigen Sitzung entfernt -
            // sonst zeigen die Energie-Entitäten einen Zustand an, den dieser
            // Prozess noch nicht bestätigt hat.
            await ClearAvailabilityAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Das Availability-Topic konnte beim Start nicht geleert werden.");
        }

        try
        {
            await foreach (var _ in _cache.SubscribeAsync(stoppingToken).ConfigureAwait(false))
            {
                try
                {
                    await ApplyAsync(stoppingToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    // Ein einzelner Fehlschlag darf das Gate nicht beenden -
                    // sonst bliebe der Zustand für immer stehen.
                    _logger.LogError(ex, "Die Auswertung der Wärmemengenmessung ist fehlgeschlagen.");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Der Shutdown läuft.
        }
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        var availability = Availability;

        // Der Broker behält retained Nachrichten über einen Neustart hinweg.
        // Deshalb wird der Zustand nach **jedem** Verbindungsaufbau gesetzt -
        // auch der leere (noch nie gelesen).
        await PublishAvailabilityAsync(availability, cancellationToken).ConfigureAwait(false);

        if (availability != EnergyAvailability.Available)
        {
            // Ohne verfügbare Messung dürfen keine alten kWh-/COP-Werte im
            // Broker stehen bleiben - auch nicht aus einer früheren Sitzung.
            await ClearEnergyValuesAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Leert <c>Status/Energy/Available</c> (retained, leere Payload).
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    /// <remarks>
    /// Wird beim Start von <see cref="ExecuteAsync"/> aufgerufen und ist die
    /// Testnaht für den Startfall.
    /// </remarks>
    public async Task ClearAvailabilityAsync(CancellationToken cancellationToken)
    {
        await PublishAvailabilityAsync(EnergyAvailability.Unknown, cancellationToken).ConfigureAwait(false);
        Interlocked.Exchange(ref _applied, (int)EnergyAvailability.Unknown);
    }

    /// <summary>
    /// Wertet den aktuellen Zustand aus und führt bei einem **Wechsel** die
    /// Nebenwirkungen aus.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der die Auswertung abschließt.</returns>
    /// <remarks>
    /// <para>
    /// Wird von <see cref="ExecuteAsync"/> bei jeder Cache-Änderung
    /// aufgerufen und ist zugleich die Testnaht. Die Methode ist billig,
    /// solange sich nichts geändert hat: ein Cache-Nachschlagevorgang und ein
    /// Vergleich.
    /// </para>
    /// <para>
    /// Sie wird vom **einen** Subscriber-Loop serialisiert aufgerufen und ist
    /// darum nicht für parallele Aufrufe ausgelegt.
    /// </para>
    /// </remarks>
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        var availability = ReadAvailability();
        var applied = (EnergyAvailability)Volatile.Read(ref _applied);

        if (availability == applied)
        {
            return;
        }

        switch (availability)
        {
            case EnergyAvailability.Available:
                // Erst die Verfügbarkeit, dann die Werte: Home Assistant legt
                // die Entität damit als verfügbar an und bekommt sofort Werte.
                await PublishAvailabilityAsync(availability, cancellationToken).ConfigureAwait(false);
                await RepublishEnergyValuesAsync(cancellationToken).ConfigureAwait(false);
                break;

            case EnergyAvailability.Unavailable:
                await PublishAvailabilityAsync(availability, cancellationToken).ConfigureAwait(false);
                await ClearEnergyValuesAsync(cancellationToken).ConfigureAwait(false);
                ReportUnavailable();
                break;

            default:
                await PublishAvailabilityAsync(availability, cancellationToken).ConfigureAwait(false);
                break;
        }

        // Erst nach den Nebenwirkungen festschreiben: bricht ein Publish ab,
        // wird der Wechsel beim nächsten Signal erneut versucht.
        Interlocked.Exchange(ref _applied, (int)availability);
    }

    /// <summary>
    /// Liest den Zustand aus dem Cache.
    /// </summary>
    /// <returns>Die abgeleitete Verfügbarkeit.</returns>
    private EnergyAvailability ReadAvailability()
    {
        if (!_cache.TryGet(MeasurementModeId, out var cached) || cached is null)
        {
            return EnergyAvailability.Unknown;
        }

        if (cached.IsStale)
        {
            // Ein veralteter Wert beweist nichts - er wird wie „nicht lesbar"
            // behandelt (die Tabelle der Spezifikation: Lesefehler ⇒ 0).
            return EnergyAvailability.Unavailable;
        }

        return cached.Value is EnumValue enumValue && IsEnabledMode(enumValue)
            ? EnergyAvailability.Available
            : EnergyAvailability.Unavailable;
    }

    /// <summary>
    /// Ist die Betriebsart eine der beiden eingeschalteten Arten?
    /// </summary>
    /// <remarks>
    /// <c>EnergyMeasurementModes</c>: <c>0 = None</c>, <c>1 = On</c>,
    /// <c>2 = OnSeparateWarmWater</c>. Die Zahlen sind Teil des Enum-Vertrags.
    /// </remarks>
    private static bool IsEnabledMode(EnumValue value) => value.Value is 1 or 2;

    /// <summary>
    /// Steht DP 3340 nachweislich auf <c>0</c>?
    /// </summary>
    /// <remarks>
    /// Unterscheidet die beiden Fehlercodes: <c>0</c> ist eine Entscheidung
    /// am Gerät, alles andere (kein Wert, anderer Wert, veraltet) ein
    /// Leseproblem.
    /// </remarks>
    private bool IsMeasurementDisabled()
    {
        if (!_cache.TryGet(MeasurementModeId, out var cached) || cached is null || cached.IsStale)
        {
            return false;
        }

        return cached.Value is EnumValue { Value: 0 };
    }

    /// <summary>
    /// Meldet den nicht verfügbaren Zustand (Log + Fehler-Senke).
    /// </summary>
    private void ReportUnavailable()
    {
        var disabled = IsMeasurementDisabled();
        var code = disabled ? DisabledCode : UnavailableCode;
        var message = disabled ? DisabledMessage : UnavailableMessage;

        _logger.LogWarning(
            "Die Energie-Topics werden gesperrt ({Code}): {Message}",
            code,
            message);

        // Der Fehler landet in **drei** Kanälen: Log (hier), Status/Errors
        // (über die Senke) und die UI (die Senke speist sie).
        _errors.Report(Source, code, message, exception: null, severity: "warning");
    }

    /// <summary>
    /// Publiziert das Availability-Topic.
    /// </summary>
    /// <param name="availability">Der zu setzende Zustand.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    /// <remarks>
    /// Das Topic ist **immer** retained - es ist der Availability-Kanal der
    /// Energie-Entitäten. Eine leere retained Payload löscht es beim Broker.
    /// </remarks>
    private Task<bool> PublishAvailabilityAsync(
        EnergyAvailability availability,
        CancellationToken cancellationToken)
    {
        var payload = availability switch
        {
            EnergyAvailability.Available => AvailablePayload,
            EnergyAvailability.Unavailable => UnavailablePayload,
            _ => string.Empty,
        };

        return _publisher.PublishAsync(
            _topics.EnergyAvailableTopic,
            payload,
            MqttQualityOfServiceLevel.AtMostOnce,
            retain: true,
            cancellationToken);
    }

    /// <summary>
    /// Publiziert die gecachten Energie-Werte erneut.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der die Publishes abschließt.</returns>
    /// <remarks>
    /// Nur Werte, die da sind und nicht veraltet sind: ein beim Wechsel
    /// gerade gelesener Wert ist frisch, ein älterer wird beim nächsten
    /// Poll-Zyklus nachgeliefert.
    /// </remarks>
    private async Task RepublishEnergyValuesAsync(CancellationToken cancellationToken)
    {
        var published = 0;

        foreach (var item in GatedItems)
        {
            if (!_cache.TryGet(item.Id, out var cached) || cached is null || cached.Value is null || cached.IsStale)
            {
                continue;
            }

            var payload = MqttValueFormatter.StatePayload(item, cached.Value);
            if (payload.Length == 0)
            {
                continue;
            }

            await _publisher.PublishAsync(
                _topics.ItemTopic(item),
                payload,
                MqttQualityOfServiceLevel.AtMostOnce,
                _options.UseMqttRetain,
                cancellationToken).ConfigureAwait(false);

            published++;
        }

        _logger.LogInformation(
            "{Count} Energie-Werte nach dem Einschalten der Wärmemengenmessung erneut publiziert.",
            published);
    }

    /// <summary>
    /// Leert die Topics der gesperrten Energie-Items (retained).
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der die Publishes abschließt.</returns>
    /// <remarks>
    /// Retain ist hier **immer** <c>true</c> - nur eine retained leere Payload
    /// entfernt die Nachricht beim Broker. Ohne das blieben veraltete
    /// kWh-/COP-Werte stehen.
    /// </remarks>
    private async Task ClearEnergyValuesAsync(CancellationToken cancellationToken)
    {
        var cleared = 0;

        foreach (var item in GatedItems)
        {
            var published = await _publisher.PublishAsync(
                _topics.ItemTopic(item),
                string.Empty,
                MqttQualityOfServiceLevel.AtMostOnce,
                retain: true,
                cancellationToken).ConfigureAwait(false);

            if (published)
            {
                cleared++;
            }
        }

        if (cleared > 0)
        {
            _logger.LogInformation("{Count} Energie-Topics geleert.", cleared);
        }
    }
}
