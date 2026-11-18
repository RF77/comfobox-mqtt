using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Health;
using ComfoBox.Gateway.Mqtt;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Hardware;

/// <summary>
/// Der Zustand einer Baugruppe (Pufferspeicher, Solarkreis, …).
/// </summary>
public enum HardwareState
{
    /// <summary>
    /// Der Typ-Parameter wurde noch nicht gelesen - es ist nichts bekannt.
    /// </summary>
    Unknown = 0,

    /// <summary>
    /// Die Baugruppe ist vorhanden, die zugehörigen Items werden gelesen.
    /// </summary>
    Available = 1,

    /// <summary>
    /// Die Baugruppe ist am Regler nicht eingerichtet.
    /// </summary>
    Unavailable = 2,
}

/// <summary>
/// Die Verfügbarkeit der Baugruppen und der angeschlossenen Fühler.
/// </summary>
/// <remarks>
/// <para>
/// Der Regler liefert für einen nicht vorhandenen Fühler den Sentinel
/// <c>32767</c>. Ob die Baugruppe vorhanden ist, sagt er über seine
/// Typ-Parameter (DP 805, 807, 117, 2800, 3918) - sie stehen als
/// <see cref="RefreshPriority.Slow"/> im Katalog und werden stündlich
/// gelesen.
/// </para>
/// <para>
/// Das Gate hat zwei Aufgaben:
/// </para>
/// <list type="number">
/// <item>
/// <b>Sperren:</b> Items einer nicht vorhandenen Baugruppe werden weder
/// gelesen noch publiziert. Der Leseweg ist der wichtigere Teil: ein Objekt,
/// das es am Regler nicht gibt, antwortet mit
/// <c>ERROR_CODE_UNKNOWN_OBJECT</c> - jede Abfrage erzeugt einen Fehler und
/// belegt den Bus.
/// </item>
/// <item>
/// <b>Melden:</b> je Baugruppe ein retained Topic
/// <c>{BaseTopic}/Status/{TopicPath}</c> (<c>1</c>/<c>0</c>, leer = unbekannt).
/// Es ist die <c>availability_topic</c> der zugehörigen Home-Assistant-Entitäten.
/// </item>
/// </list>
/// <para>
/// Der Zustand wird **bei jedem Zugriff** aus dem Wert-Cache abgeleitet; nur
/// die Nebenwirkungen (Topic setzen, Werte leeren, Meldung) laufen einmal je
/// Wechsel.
/// </para>
/// </remarks>
public sealed class HardwareAvailabilityGate : BackgroundService, IMqttConnectionHandler
{
    /// <summary>
    /// Die Quelle der Meldungen in der Fehler-Senke.
    /// </summary>
    public const string Source = "HardwareAvailability";

    /// <summary>
    /// Der Fehlercode für „Baugruppe am Regler nicht eingerichtet".
    /// </summary>
    public const string NotInstalledCode = "hardware_not_installed";

    /// <summary>
    /// Der Fehlercode für „Fühler liefert den Sentinel".
    /// </summary>
    public const string NoSensorCode = "sensor_not_connected";

    /// <summary>
    /// Der Wert, mit dem der Regler einen fehlenden Fühler meldet.
    /// </summary>
    /// <remarks>
    /// Das INT16-Maximum – kein Katalog-Item hat einen Bereich, der bis
    /// dorthin reicht. Das Handbuch dokumentiert den Wert nicht; er ist aus
    /// dem Verhalten abgeleitet.
    /// </remarks>
    public const float NoSensorValue = HardwareRequirements.NoSensorValue;

    private const string AvailablePayload = "1";
    private const string UnavailablePayload = "0";

    private readonly IValueCache _cache;
    private readonly IItemCatalog _catalog;
    private readonly MqttTopicMap _topics;
    private readonly IMqttPublisher _publisher;
    private readonly IErrorCollector _errors;
    private readonly MqttOptions _options;
    private readonly ILogger<HardwareAvailabilityGate> _logger;

    /// <summary>
    /// Der zuletzt angewandte Zustand je Baugruppe.
    /// </summary>
    private readonly Dictionary<string, HardwareState> _applied = new(StringComparer.Ordinal);

    /// <summary>
    /// Die Items, für die der Regler den Sentinel gemeldet hat.
    /// </summary>
    private readonly HashSet<string> _noSensor = new(StringComparer.Ordinal);

    /// <summary>
    /// Die Items, deren retained Topic noch geleert werden muss.
    /// </summary>
    private readonly Queue<string> _pendingClear = new();

    private readonly Lock _sync = new();

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
    public HardwareAvailabilityGate(
        IValueCache cache,
        IItemCatalog catalog,
        MqttTopicMap topics,
        IMqttPublisher publisher,
        IErrorCollector errors,
        MqttOptions options,
        ILogger<HardwareAvailabilityGate> logger)
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
    /// Der Zustand einer Baugruppe.
    /// </summary>
    /// <param name="requirement">Die Baugruppe.</param>
    /// <returns>Der abgeleitete Zustand.</returns>
    public HardwareState StateOf(HardwareRequirement requirement)
    {
        ArgumentNullException.ThrowIfNull(requirement);

        if (!_cache.TryGet(requirement.DetectorItemId, out var cached) || cached is null || cached.IsStale)
        {
            return HardwareState.Unknown;
        }

        var value = NumericValue(cached.Value);
        if (value is null)
        {
            return HardwareState.Unknown;
        }

        return requirement.IsPresent(value.Value)
            ? HardwareState.Available
            : HardwareState.Unavailable;
    }

    /// <summary>
    /// Ist die Baugruppe eines Items vorhanden?
    /// </summary>
    /// <param name="itemId">Die Id des Items.</param>
    /// <remarks>
    /// Ein Item ohne Baugruppe ist immer verfügbar. Ein unbekannter Zustand
    /// gilt als **nicht** verfügbar: solange der Typ-Parameter nicht gelesen
    /// ist, wird nichts publiziert – ein <c>32767</c> wäre schlimmer als ein
    /// fehlender Wert.
    /// </remarks>
    public bool IsAvailable(string itemId)
    {
        var requirement = HardwareRequirements.ForItem(itemId);

        return requirement is null || StateOf(requirement) == HardwareState.Available;
    }

    /// <summary>
    /// Meldet der Regler für dieses Item einen fehlenden Fühler?
    /// </summary>
    /// <param name="itemId">Die Id des Items.</param>
    public bool IsNoSensor(string itemId)
    {
        lock (_sync)
        {
            return _noSensor.Contains(itemId);
        }
    }

    /// <summary>
    /// Darf das Item gelesen werden?
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <remarks>
    /// Die Typ-Parameter werden **immer** gelesen – sie entscheiden ja erst
    /// über die übrigen Items.
    /// </remarks>
    public bool ShouldPoll(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return HardwareRequirements.IsDetector(item.Id) || IsAvailable(item.Id);
    }

    /// <summary>
    /// Der Grund, warum ein Item keinen Wert hat (oder <c>null</c>).
    /// </summary>
    /// <param name="itemId">Die Id des Items.</param>
    /// <returns>
    /// <c>not_installed</c>, <c>no_sensor</c>, <c>unknown</c> oder <c>null</c>.
    /// </returns>
    public string? UnavailableReason(string itemId)
    {
        if (IsNoSensor(itemId))
        {
            return "no_sensor";
        }

        var requirement = HardwareRequirements.ForItem(itemId);
        if (requirement is null)
        {
            return null;
        }

        return StateOf(requirement) switch
        {
            HardwareState.Available => null,
            HardwareState.Unavailable => "not_installed",
            _ => "unknown",
        };
    }

    /// <summary>
    /// Merkt sich, dass der Regler für dieses Item keinen Fühler hat.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <remarks>
    /// Wird von <see cref="Bacnet.BacnetDevice"/> beim Lesen aufgerufen. Die
    /// Meldung entsteht **einmal** je Item – sonst würde der Fehler-Ringpuffer
    /// (50 Einträge) bei jedem Poll-Zyklus gefüllt.
    /// </remarks>
    public void ReportNoSensor(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);

        lock (_sync)
        {
            if (!_noSensor.Add(item.Id))
            {
                return;
            }

            _pendingClear.Enqueue(item.Id);
        }

        _logger.LogInformation(
            "{Item} liefert den Sentinel {Sentinel} – es ist kein Fühler angeschlossen.",
            item.TopicPath,
            NoSensorValue);

        _errors.Report(
            Source,
            NoSensorCode,
            $"Für {item.TopicPath} ist kein Fühler angeschlossen (Wert {NoSensorValue:0}). "
            + "Der Wert wird nicht publiziert.",
            exception: null,
            severity: "warning");
    }

    /// <summary>
    /// Meldet, dass ein Item wieder einen echten Wert geliefert hat.
    /// </summary>
    /// <param name="itemId">Die Id des Items.</param>
    public void ConfirmSensor(string itemId)
    {
        lock (_sync)
        {
            if (_noSensor.Remove(itemId))
            {
                _logger.LogInformation(
                    "{Item} liefert wieder einen Wert – der Fühler ist angeschlossen.",
                    itemId);
            }
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await ClearAvailabilityAsync(stoppingToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            return;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Die Verfügbarkeits-Topics konnten beim Start nicht geleert werden.");
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
                    _logger.LogError(ex, "Die Auswertung der Baugruppen ist fehlgeschlagen.");
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

        // Der Broker behält retained Nachrichten über einen Neustart hinweg.
        // Deshalb wird jeder Zustand nach **jedem** Verbindungsaufbau gesetzt.
        foreach (var requirement in HardwareRequirements.All)
        {
            var state = StateOf(requirement);

            await PublishAvailabilityAsync(requirement, state, cancellationToken).ConfigureAwait(false);

            if (state != HardwareState.Available)
            {
                await ClearItemsAsync(requirement, cancellationToken).ConfigureAwait(false);
            }
        }

        await ClearPendingNoSensorAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Leert alle Verfügbarkeits-Topics (retained, leere Payload).
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der die Publishes abschließt.</returns>
    /// <remarks>
    /// Wird beim Start aufgerufen: ein <c>1</c> aus der vorigen Sitzung darf
    /// nicht stehen bleiben, bevor dieser Prozess es bestätigt hat.
    /// </remarks>
    public async Task ClearAvailabilityAsync(CancellationToken cancellationToken)
    {
        foreach (var requirement in HardwareRequirements.All)
        {
            await PublishAvailabilityAsync(requirement, HardwareState.Unknown, cancellationToken)
                .ConfigureAwait(false);
        }

        lock (_sync)
        {
            _applied.Clear();
        }
    }

    /// <summary>
    /// Wertet alle Baugruppen aus und führt bei einem **Wechsel** die
    /// Nebenwirkungen aus.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der die Auswertung abschließt.</returns>
    /// <remarks>
    /// Wird von <see cref="ExecuteAsync"/> bei jeder Cache-Änderung
    /// aufgerufen und ist zugleich die Testnaht.
    /// </remarks>
    public async Task ApplyAsync(CancellationToken cancellationToken)
    {
        foreach (var requirement in HardwareRequirements.All)
        {
            var state = StateOf(requirement);

            lock (_sync)
            {
                if (_applied.TryGetValue(requirement.Key, out var applied) && applied == state)
                {
                    continue;
                }

                _applied[requirement.Key] = state;
            }

            await PublishAvailabilityAsync(requirement, state, cancellationToken).ConfigureAwait(false);

            if (state == HardwareState.Available)
            {
                continue;
            }

            // Ohne Baugruppe dürfen keine alten Werte im Broker stehen -
            // auch nicht aus einer früheren Sitzung oder Konfiguration.
            await ClearItemsAsync(requirement, cancellationToken).ConfigureAwait(false);

            if (state == HardwareState.Unavailable)
            {
                ReportNotInstalled(requirement);
            }
        }

        await ClearPendingNoSensorAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Die Items einer Baugruppe aus dem Katalog.
    /// </summary>
    private List<ItemDefinition> ItemsOf(HardwareRequirement requirement)
    {
        var items = new List<ItemDefinition>(requirement.ItemIds.Count);

        foreach (var id in requirement.ItemIds)
        {
            var item = _catalog.Find(id);
            if (item is not null)
            {
                items.Add(item);
            }
        }

        return items;
    }

    /// <summary>
    /// Meldet eine fehlende Baugruppe (Log + Fehler-Senke).
    /// </summary>
    private void ReportNotInstalled(HardwareRequirement requirement)
    {
        var message = $"{requirement.Label}: {requirement.DetectorLabel} meldet die Baugruppe als nicht vorhanden. "
            + "Die zugehörigen Werte werden weder gelesen noch publiziert."
            + (requirement.Hint is null ? string.Empty : " " + requirement.Hint);

        _logger.LogInformation("Baugruppe nicht vorhanden: {Message}", message);

        _errors.Report(Source, NotInstalledCode, message, exception: null, severity: "warning");
    }

    /// <summary>
    /// Publiziert das Verfügbarkeits-Topic einer Baugruppe.
    /// </summary>
    private Task<bool> PublishAvailabilityAsync(
        HardwareRequirement requirement,
        HardwareState state,
        CancellationToken cancellationToken)
    {
        var payload = state switch
        {
            HardwareState.Available => AvailablePayload,
            HardwareState.Unavailable => UnavailablePayload,
            _ => string.Empty,
        };

        return _publisher.PublishAsync(
            _topics.HardwareTopic(requirement),
            payload,
            MqttQualityOfServiceLevel.AtMostOnce,
            retain: true,
            cancellationToken);
    }

    /// <summary>
    /// Leert die Topics aller Items einer Baugruppe (retained).
    /// </summary>
    private async Task ClearItemsAsync(HardwareRequirement requirement, CancellationToken cancellationToken)
    {
        var cleared = 0;

        foreach (var item in ItemsOf(requirement))
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
            _logger.LogInformation(
                "{Count} Topics der Baugruppe {Group} geleert.",
                cleared,
                requirement.Label);
        }
    }

    /// <summary>
    /// Leert die Topics der Items, für die der Regler keinen Fühler meldet.
    /// </summary>
    private async Task ClearPendingNoSensorAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            string? itemId;

            lock (_sync)
            {
                if (_pendingClear.Count == 0)
                {
                    return;
                }

                itemId = _pendingClear.Dequeue();
            }

            var item = _catalog.Find(itemId);
            if (item is null)
            {
                continue;
            }

            await _publisher.PublishAsync(
                _topics.ItemTopic(item),
                string.Empty,
                MqttQualityOfServiceLevel.AtMostOnce,
                retain: true,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Der Zahlenwert eines gecachten Werts.
    /// </summary>
    private static float? NumericValue(ItemValue? value) => value switch
    {
        AnalogValue analog => analog.Value,
        EnumValue enumValue => enumValue.Value,
        _ => null,
    };
}
