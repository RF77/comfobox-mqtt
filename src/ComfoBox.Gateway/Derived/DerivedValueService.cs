using ComfoBox.Domain.Derived;
using ComfoBox.Domain.Items;
using ComfoBox.Domain.Values;
using ComfoBox.Gateway.Configuration;
using ComfoBox.Gateway.Mqtt;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Derived;

/// <summary>
/// Rechnet die abgeleiteten Werte aus dem Wert-Cache und publiziert sie.
/// </summary>
/// <remarks>
/// <para>
/// Der Dienst hört auf den Wert-Cache und rechnet **nur die betroffenen**
/// Definitionen neu - eine Änderung von DP 24 löst keine COP-Rechnung aus.
/// Publiziert wird nur, wenn sich die Payload geändert hat (kein Spam).
/// </para>
/// <para>
/// Die Payload ist eine Zahl mit <see cref="MqttPayload.Format(float)"/>
/// (kultur-invariant, <c>0.###</c>) oder das Wort <c>unknown</c>. Ein fehlender
/// oder veralteter Eingang ergibt <c>unknown</c> - **nie** eine <c>0</c>.
/// </para>
/// <para>
/// Das Topic ist <c>{BaseTopic}/Status/{TopicPath}</c>, z. B.
/// <c>ComfoBox/Status/Derived/Superheat</c>. Abgeleitete Werte sind nie
/// schreibbar - es gibt kein <c>/Set</c>-Topic.
/// </para>
/// <para>
/// <c>Status/Derived/CopInstant</c> ist der einzige zustandsbehaftete Wert: er
/// braucht zwei Proben, die mindestens eine Stunde auseinanderliegen. Die
/// Historie lebt **nur** in diesem Dienst - nach einem Neustart ist der Wert
/// deshalb <c>unknown</c>, bis ein Fenster geschlossen ist.
/// </para>
/// </remarks>
public sealed class DerivedValueService : BackgroundService
{
    /// <summary>
    /// Die Payload für „kein Wert berechenbar".
    /// </summary>
    /// <remarks>
    /// Wörtlich <c>unknown</c> - Home Assistant zeigt das als unbekannten
    /// Zustand an. Eine <c>0</c> wäre hier eine falsche Aussage.
    /// </remarks>
    public const string UnknownPayload = "unknown";

    /// <summary>
    /// Die Mindestlänge des Fensters für den Momentan-COP.
    /// </summary>
    public static readonly TimeSpan CopInstantWindow = TimeSpan.FromHours(1);

    private readonly IValueCache _cache;
    private readonly IItemCatalog _catalog;
    private readonly IMqttPublisher _publisher;
    private readonly MqttTopicMap _topics;
    private readonly MqttOptions _options;
    private readonly GatewayOptions _gatewayOptions;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DerivedValueService> _logger;

    private readonly IReadOnlyList<DerivedValueDefinition> _definitions = DerivedValues.All;
    private readonly Dictionary<int, ItemDefinition> _itemsByObjectId;
    private readonly Dictionary<string, string> _lastPayloads = new(StringComparer.Ordinal);

    /// <summary>
    /// Die erste Probe des COP-Fensters (beim Start <c>null</c>).
    /// </summary>
    private CopSample? _baseline;

    /// <summary>
    /// Der zuletzt berechnete COP (bleibt zwischen zwei Fenstern stehen).
    /// </summary>
    private float? _lastCop;

    /// <summary>
    /// Erzeugt den Dienst.
    /// </summary>
    /// <param name="cache">Der Wert-Cache.</param>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="publisher">Der Publisher.</param>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="options">Die MQTT-Einstellungen.</param>
    /// <param name="gatewayOptions">Die Grundeinstellungen (Poll-Intervalle).</param>
    /// <param name="timeProvider">Die Zeitquelle (für das COP-Fenster).</param>
    /// <param name="logger">Logger.</param>
    public DerivedValueService(
        IValueCache cache,
        IItemCatalog catalog,
        IMqttPublisher publisher,
        MqttTopicMap topics,
        MqttOptions options,
        GatewayOptions gatewayOptions,
        TimeProvider timeProvider,
        ILogger<DerivedValueService> logger)
    {
        ArgumentNullException.ThrowIfNull(cache);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(publisher);
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(gatewayOptions);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        _cache = cache;
        _catalog = catalog;
        _publisher = publisher;
        _topics = topics;
        _options = options;
        _gatewayOptions = gatewayOptions;
        _timeProvider = timeProvider;
        _logger = logger;

        // Die Eingänge werden über die BACnet-Objekt-Id aufgelöst. Für diese
        // Datenpunkte ist der Katalog eindeutig (drei andere Objekte sind
        // bewusst doppelt belegt - keines davon ist ein Eingang hier).
        _itemsByObjectId = catalog.All
            .GroupBy(static item => item.BacnetObjectId)
            .ToDictionary(static group => group.Key, static group => group.First());
    }

    /// <summary>
    /// Die sieben Definitionen, die dieser Dienst publiziert.
    /// </summary>
    public IReadOnlyList<DerivedValueDefinition> Definitions => _definitions;

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Erst abonnieren, dann den Anfangszustand publizieren: so geht keine
        // Änderung verloren, die zwischen den beiden Schritten passiert.
        var changes = _cache.SubscribeAsync(stoppingToken);

        await PublishAllAsync(stoppingToken).ConfigureAwait(false);

        try
        {
            await foreach (var cached in changes.ConfigureAwait(false))
            {
                await PublishForItemAsync(cached.Id, stoppingToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Der Shutdown läuft - kein Fehler.
        }
    }

    /// <summary>
    /// Rechnet **alle** Definitionen neu und publiziert Änderungen.
    /// </summary>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    /// <remarks>
    /// Die Methode ist die Naht für die Tests: sie braucht weder den
    /// Hintergrunddienst noch einen Broker.
    /// </remarks>
    public async Task PublishAllAsync(CancellationToken cancellationToken)
    {
        foreach (var definition in _definitions)
        {
            await PublishDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rechnet die Definitionen neu, die ein Item als Eingang verwenden.
    /// </summary>
    /// <param name="itemId">Die Id des geänderten Items.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    public async Task PublishForItemAsync(string itemId, CancellationToken cancellationToken)
    {
        var item = _catalog.Find(itemId);
        if (item is null)
        {
            return;
        }

        foreach (var definition in _definitions)
        {
            if (!definition.InputObjectIds.Contains(item.BacnetObjectId))
            {
                continue;
            }

            await PublishDefinitionAsync(definition, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Rechnet eine Definition und publiziert sie, wenn sich die Payload geändert hat.
    /// </summary>
    private async Task PublishDefinitionAsync(
        DerivedValueDefinition definition,
        CancellationToken cancellationToken)
    {
        try
        {
            var value = Evaluate(definition);
            var payload = value is null ? UnknownPayload : MqttPayload.Format(value.Value);

            if (IsUnchanged(definition.Id, payload))
            {
                return;
            }

            var topic = $"{_topics.BaseTopic}/Status/{definition.TopicPath}";

            await _publisher.PublishAsync(
                topic,
                payload,
                MqttQualityOfServiceLevel.AtMostOnce,
                _options.UseMqttRetain,
                cancellationToken).ConfigureAwait(false);

            // Erst nach dem Publish merken: scheitert er, wird die Payload beim
            // nächsten Zyklus erneut versucht.
            _lastPayloads[definition.Id] = payload;

            _logger.LogDebug("{Id} = {Payload} auf {Topic} publiziert.", definition.Id, payload, topic);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Shutdown - die Nachricht ist nicht mehr wichtig.
        }
        catch (Exception ex)
        {
            // Ein abgeleiteter Wert darf den Gateway nie stören: loggen, weiter.
            _logger.LogWarning(ex, "Der abgeleitete Wert {Id} konnte nicht publiziert werden.", definition.Id);
        }
    }

    /// <summary>
    /// Ist die Payload unverändert (und damit kein zweiter Publish nötig)?
    /// </summary>
    private bool IsUnchanged(string id, string payload)
        => _lastPayloads.TryGetValue(id, out var previous)
            && string.Equals(previous, payload, StringComparison.Ordinal);

    /// <summary>
    /// Berechnet den Wert einer Definition aus dem Cache.
    /// </summary>
    private float? Evaluate(DerivedValueDefinition definition)
    {
        var inputs = ReadInputs(definition);

        if (definition.RequiresHistory)
        {
            return EvaluateCopInstant(inputs);
        }

        return definition.Compute?.Invoke(inputs);
    }

    /// <summary>
    /// Liest alle Eingänge einer Definition (fehlend oder veraltet ⇒ <c>null</c>).
    /// </summary>
    private Dictionary<int, float?> ReadInputs(DerivedValueDefinition definition)
    {
        var inputs = new Dictionary<int, float?>(definition.InputObjectIds.Count);

        foreach (var objectId in definition.InputObjectIds)
        {
            inputs[objectId] = ReadNumeric(objectId);
        }

        return inputs;
    }

    /// <summary>
    /// Liest einen Zahlenwert aus dem Cache.
    /// </summary>
    /// <remarks>
    /// Ein Wert, der nie gelesen wurde, der als veraltet markiert ist oder der
    /// älter als das Frische-Fenster seiner Priorität ist, gilt als
    /// **unbekannt**. Das ist die Umsetzung von „beide Eingänge im gleichen
    /// Zyklus": die Eingänge haben unterschiedliche Poll-Prioritäten (Primary,
    /// Secondary, Slow), ein Vergleich der Zeitstempel wäre deshalb nie
    /// erfüllbar.
    /// </remarks>
    private float? ReadNumeric(int objectId)
    {
        if (!_itemsByObjectId.TryGetValue(objectId, out var item))
        {
            return null;
        }

        if (!_cache.TryGet(item.Id, out var cached) || cached?.Value is null)
        {
            return null;
        }

        if (!IsFresh(item, cached))
        {
            return null;
        }

        return cached.Value switch
        {
            AnalogValue analog => analog.Value,
            EnumValue enumValue => enumValue.Value,
            _ => null,
        };
    }

    /// <summary>
    /// Ist ein Wert frisch genug für eine Ableitung?
    /// </summary>
    /// <remarks>
    /// <para>
    /// Das Ablauf-Fenster des Caches ist auf drei Zyklen der **sekundären**
    /// Priorität ausgelegt. Ein stündlich gelesenes Item wäre damit 45 von 60
    /// Minuten „veraltet" - und <c>RuntimeRatio</c>/<c>ServiceDue</c> würden
    /// fast immer <c>unknown</c> liefern. Deshalb gilt hier das Fenster der
    /// **eigenen** Priorität: das Dreifache des erwarteten Leseabstands.
    /// </para>
    /// <para>
    /// Ein ausdrücklich als veraltet markierter Wert (fehlgeschlagener Read)
    /// wird **nie** verwendet - auch nicht innerhalb des Fensters.
    /// </para>
    /// </remarks>
    private bool IsFresh(ItemDefinition item, CachedValue cached)
    {
        if (_cache.IsMarkedStale(item.Id))
        {
            return false;
        }

        return _timeProvider.GetUtcNow() - cached.Timestamp <= FreshnessWindow(item.Priority);
    }

    /// <summary>
    /// Das Frische-Fenster einer Priorität (dreifacher Leseabstand).
    /// </summary>
    /// <param name="priority">Die Poll-Priorität des Items.</param>
    /// <returns>Die zulässige Zeitspanne seit dem letzten erfolgreichen Read.</returns>
    private TimeSpan FreshnessWindow(RefreshPriority priority)
    {
        var poll = Math.Max(1, _gatewayOptions.PollIntervalSeconds);
        var secondary = poll * Math.Max(1, _gatewayOptions.SecondaryPollFactor);

        var seconds = priority switch
        {
            RefreshPriority.Primary => poll,
            RefreshPriority.Slow => Math.Max(3600, secondary),
            RefreshPriority.None => poll,
            _ => secondary,
        };

        return TimeSpan.FromSeconds(3.0 * seconds);
    }

    /// <summary>
    /// Der Momentan-COP aus zwei Proben, die mindestens eine Stunde auseinanderliegen.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Beim ersten Aufruf wird nur die erste Probe gemerkt - der Wert ist dann
    /// <c>unknown</c> (auch nach jedem Neustart, weil die Historie nur im
    /// Prozess lebt). Liegt die neue Probe weniger als eine Stunde nach der
    /// ersten, bleibt der zuletzt berechnete COP stehen: das Fenster ist
    /// „mindestens 1 h", nicht „genau 1 h".
    /// </para>
    /// <para>
    /// Nach jeder Rechnung wird die neue Probe zur Basis - damit bezieht sich
    /// jeder Wert auf ein frisches Fenster und nicht auf den Zählerstand von
    /// vor Tagen.
    /// </para>
    /// </remarks>
    private float? EvaluateCopInstant(IReadOnlyDictionary<int, float?> inputs)
    {
        var thermal = Input(inputs, DerivedValues.ThermalEnergyTotalObjectId);
        var electrical = Input(inputs, DerivedValues.ElectricalEnergyTotalObjectId);

        if (thermal is null || electrical is null)
        {
            // Ein Eingang fehlt oder ist veraltet: keine Aussage, keine 0.
            return null;
        }

        var now = _timeProvider.GetUtcNow();

        if (_baseline is null)
        {
            _baseline = new CopSample(now, thermal.Value, electrical.Value);
            return null;
        }

        if (now - _baseline.Timestamp < CopInstantWindow)
        {
            return _lastCop;
        }

        var thermalDelta = (double)thermal.Value - _baseline.Thermal;
        var electricalDelta = (double)electrical.Value - _baseline.Electrical;

        _baseline = new CopSample(now, thermal.Value, electrical.Value);
        _lastCop = DerivedValues.CopInstant(thermalDelta, electricalDelta);

        return _lastCop;
    }

    /// <summary>
    /// Liest einen Eingang aus den bereits gelesenen Werten.
    /// </summary>
    private static float? Input(IReadOnlyDictionary<int, float?> inputs, int objectId)
        => inputs.TryGetValue(objectId, out var value) ? value : null;

    /// <summary>
    /// Eine Probe des COP-Fensters (Zeitpunkt und beide Zählerstände).
    /// </summary>
    private sealed record CopSample(DateTimeOffset Timestamp, double Thermal, double Electrical);
}
