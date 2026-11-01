using System.Text.Json;
using System.Text.Json.Serialization;
using ComfoBox.Domain.Items;
using ComfoBox.Gateway.Configuration;
using MQTTnet;
using MQTTnet.Protocol;

namespace ComfoBox.Gateway.Mqtt;

/// <summary>
/// Die Home-Assistant-Discovery.
/// </summary>
/// <remarks>
/// <para>
/// Der Gateway meldet sich bei Home Assistant an - es sind **keine** manuellen
/// MQTT-Entitäten mehr nötig.
/// </para>
/// <para>
/// Jede Entität bekommt dieselbe <c>device.identifiers</c>-Referenz - so
/// entsteht **eine** Gerätekarte mit allen Komponenten. Die Discovery-Payloads
/// werden retained publiziert, sonst fehlen die Entitäten nach einem
/// Neustart von Home Assistant.
/// </para>
/// <para>
/// Die Enum-Namen werden **nicht** übersetzt: sie sind Teil des
/// MQTT-Vertrags.
/// </para>
/// </remarks>
public sealed class HomeAssistantDiscovery : IMqttConnectionHandler
{
    private const string Manufacturer = "Zehnder";
    private const string Model = "ComfoBox Series 5 (ELESTA)";
    private const string OriginName = "comfobox-gateway";

    private readonly MqttTopicMap _topics;
    private readonly IItemCatalog _catalog;
    private readonly MqttOptions _options;
    private readonly GatewayOptions _gatewayOptions;
    private readonly ILogger<HomeAssistantDiscovery> _logger;

    /// <summary>
    /// Erzeugt die Discovery.
    /// </summary>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="options">Die MQTT-Einstellungen.</param>
    /// <param name="gatewayOptions">Die Grundeinstellungen (Experten-Modus).</param>
    /// <param name="logger">Logger.</param>
    public HomeAssistantDiscovery(
        MqttTopicMap topics,
        IItemCatalog catalog,
        MqttOptions options,
        GatewayOptions gatewayOptions,
        ILogger<HomeAssistantDiscovery> logger)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(gatewayOptions);
        ArgumentNullException.ThrowIfNull(logger);

        _topics = topics;
        _catalog = catalog;
        _options = options;
        _gatewayOptions = gatewayOptions;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task OnConnectedAsync(IMqttClient client, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(client);

        if (!_options.EnableDiscovery)
        {
            // Der Nutzer hat die Discovery abgeschaltet - meist, weil er die
            // Werte ueber die HA-Integration bezieht (ohne Broker).
            _logger.LogInformation("Home-Assistant-Discovery ist abgeschaltet (Mqtt:EnableDiscovery=false).");
            return;
        }

        var count = 0;

        foreach (var item in _catalog.All)
        {
            var writable = item.IsWritableWithExpertMode(_gatewayOptions.ExpertMode);
            var payload = BuildItemPayload(item, writable);

            if (await PublishAsync(client, DiscoveryTopic(item, writable), payload, cancellationToken)
                .ConfigureAwait(false))
            {
                count++;
            }
        }

        foreach (var (topic, payload) in BuildStatusPayloads())
        {
            if (await PublishAsync(client, topic, payload, cancellationToken).ConfigureAwait(false))
            {
                count++;
            }
        }

        await PublishAsync(client, DeviceTopic, BuildDevicePayload(), cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Home-Assistant-Discovery publiziert: {Count} Komponenten.", count);
    }

    /// <summary>
    /// Das Discovery-Topic des Geräts.
    /// </summary>
    public string DeviceTopic => $"{_options.DiscoveryPrefix}/device/{DeviceIdentifier}/config";

    /// <summary>
    /// Die Kennung des Geräts in der Discovery.
    /// </summary>
    /// <remarks>
    /// Konfigurierbar über <c>Mqtt:DiscoveryIdentifier</c> - für den
    /// Parallelbetrieb müssen zwei Gateways verschiedene Kennungen haben,
    /// sonst überschreiben sich ihre Entitäten (Aufgabe 7.3).
    /// </remarks>
    public string DeviceIdentifier => _options.DiscoveryIdentifier;

    /// <summary>
    /// Die Plattform eines Items.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="writable">Ist das Item schreibbar?</param>
    /// <returns>
    /// <c>number</c> für schreibbare Zahlen, <c>select</c> für schreibbare
    /// Auswahlen und <c>sensor</c> für alles andere.
    /// </returns>
    public static string ComponentFor(ItemDefinition item, bool writable)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!writable)
        {
            return "sensor";
        }

        return item.Kind == ItemValueKind.Enum ? "select" : "number";
    }

    /// <summary>
    /// Die eindeutige Id der Entität.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns>Die Id, z. B. <c>comfobox_zone_set_point_normal</c>.</returns>
    public string UniqueId(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return $"{DeviceIdentifier}_{item.Id.Replace('.', '_')}";
    }

    /// <summary>
    /// Das Discovery-Topic eines Items.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="writable">Ist das Item schreibbar?</param>
    /// <returns>Das Topic, z. B. <c>homeassistant/number/comfobox_zone_set_point_normal/config</c>.</returns>
    public string DiscoveryTopic(ItemDefinition item, bool writable)
    {
        ArgumentNullException.ThrowIfNull(item);
        return $"{_options.DiscoveryPrefix}/{ComponentFor(item, writable)}/{UniqueId(item)}/config";
    }

    /// <summary>
    /// Die Discovery-Payload eines Items.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="writable">Ist das Item schreibbar?</param>
    /// <returns>Die Payload.</returns>
    public IReadOnlyDictionary<string, object?> BuildItemPayload(ItemDefinition item, bool writable)
    {
        ArgumentNullException.ThrowIfNull(item);

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = MqttValueFormatter.DisplayName(item),
            ["unique_id"] = UniqueId(item),
            ["state_topic"] = _topics.ItemTopic(item),
            ["availability_topic"] = _topics.HealthTopic,
            ["payload_available"] = "online",
            ["payload_not_available"] = "offline",
            ["device"] = BuildDevice(),
            ["origin"] = BuildOrigin(),
        };

        // Ein schreibbares Item bekommt das Schreib-Topic. Die beiden
        // Legacy-DateItems sind read-only und haben deshalb kein command_topic.
        if (writable)
        {
            payload["command_topic"] = _topics.ItemSetTopic(item);
        }

        if (!string.IsNullOrEmpty(item.Unit))
        {
            payload["unit_of_measurement"] = item.Unit;
        }

        var deviceClass = DeviceClassFor(item.Unit);
        if (deviceClass is not null)
        {
            payload["device_class"] = deviceClass;
        }

        if (item.Kind == ItemValueKind.Analog)
        {
            if (writable)
            {
                AddNumberProperties(payload, item);
            }
            else
            {
                payload["state_class"] = "measurement";
            }
        }
        else if (item.Kind == ItemValueKind.Enum)
        {
            if (writable)
            {
                payload["options"] = item.EnumValues
                    .OrderBy(e => e.Key)
                    .Select(e => e.Value)
                    .ToArray();
            }
        }

        return payload;
    }

    /// <summary>
    /// Die Discovery-Payload des Geräts.
    /// </summary>
    /// <returns>Die Payload.</returns>
    public IReadOnlyDictionary<string, object?> BuildDevicePayload()
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["dev"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["ids"] = new[] { DeviceIdentifier },
                ["name"] = "ComfoBox",
                ["mf"] = Manufacturer,
                ["mdl"] = Model,
                ["sw"] = MqttVersion.Current,
            },
            ["o"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = OriginName,
                ["sw"] = MqttVersion.Current,
            },
        };

    /// <summary>
    /// Die Discovery-Payloads der Status-Entitäten.
    /// </summary>
    /// <returns>Topic → Payload.</returns>
    public IReadOnlyList<KeyValuePair<string, IReadOnlyDictionary<string, object?>>> BuildStatusPayloads()
    {
        var result = new List<KeyValuePair<string, IReadOnlyDictionary<string, object?>>>();

        void Add(string id, string name, string topic, string? valueTemplate = null, string? unit = null)
        {
            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["unique_id"] = $"{DeviceIdentifier}_{id}",
                ["state_topic"] = topic,
                ["availability_topic"] = _topics.HealthTopic,
                ["payload_available"] = "online",
                ["payload_not_available"] = "offline",
                ["entity_category"] = "diagnostic",
                ["device"] = BuildDevice(),
                ["origin"] = BuildOrigin(),
            };

            if (valueTemplate is not null)
            {
                payload["value_template"] = valueTemplate;
                payload["json_attributes_topic"] = topic;
            }

            if (unit is not null)
            {
                payload["unit_of_measurement"] = unit;
            }

            result.Add(new KeyValuePair<string, IReadOnlyDictionary<string, object?>>(
                $"{_options.DiscoveryPrefix}/sensor/{DeviceIdentifier}_{id}/config",
                payload));
        }

        Add("status_health", "Health", _topics.HealthTopic);
        Add("status_version", "Version", _topics.StatusVersionTopic);
        Add("status_uptime", "Laufzeit", _topics.UptimeTopic, unit: "s");
        Add("status_writes", "Schreibvorgänge (24 h)", _topics.WritesTopic);
        Add("status_writes_max", "Schreibvorgänge (Limit)", _topics.WritesMaxTopic);
        Add("status_bacnet", "BACnet-Verbindung", _topics.BacnetTopic, valueTemplate: "{{ value_json.state }}");
        Add("status_mqtt", "MQTT-Verbindung", _topics.MqttTopic, valueTemplate: "{{ value_json.state }}");
        Add("status_last_error", "Letzter Fehler", _topics.LastErrorTopic);

        return result;
    }

    /// <summary>
    /// Die Geräte-Referenz für die Entitäten.
    /// </summary>
    /// <returns>Die Referenz.</returns>
    private Dictionary<string, object?> BuildDevice()
        => new(StringComparer.Ordinal)
        {
            ["identifiers"] = new[] { DeviceIdentifier },
            ["name"] = "ComfoBox",
            ["manufacturer"] = Manufacturer,
            ["model"] = Model,
            ["sw_version"] = MqttVersion.Current,
        };

    /// <summary>
    /// Die Herkunfts-Referenz für die Entitäten (Pflicht für die Device-Discovery).
    /// </summary>
    /// <returns>Die Referenz.</returns>
    private static Dictionary<string, object?> BuildOrigin()
        => new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = OriginName,
            ["sw_version"] = MqttVersion.Current,
        };

    /// <summary>
    /// Der HA-Gerätetyp zu einer Einheit.
    /// </summary>
    /// <param name="unit">Die Einheit.</param>
    /// <returns>Der Gerätetyp (oder <c>null</c>).</returns>
    private static string? DeviceClassFor(string? unit)
        => unit switch
        {
            "°C" => "temperature",
            _ => null,
        };

    /// <summary>
    /// Ergänzt die Eigenschaften einer Zahl-Entität.
    /// </summary>
    /// <param name="payload">Die Payload.</param>
    /// <param name="item">Das Item.</param>
    private static void AddNumberProperties(Dictionary<string, object?> payload, ItemDefinition item)
    {
        if (item.Min is not null)
        {
            payload["min"] = item.Min.Value;
        }

        if (item.Max is not null)
        {
            payload["max"] = item.Max.Value;
        }

        // Schrittweite 1 bei einem Bereich bis 1 (Schalter/Flags), sonst 0.5 -
        // so bleiben Werte wie 21.5 erreichbar.
        var span = item.Min is not null && item.Max is not null ? item.Max.Value - item.Min.Value : double.MaxValue;
        payload["step"] = span <= 1 ? 1d : 0.5d;
        payload["mode"] = "box";
    }

    /// <summary>
    /// Publiziert eine Discovery-Payload.
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <param name="topic">Das Topic.</param>
    /// <param name="payload">Die Payload.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns><c>true</c>, wenn die Payload publiziert wurde.</returns>
    private static async Task<bool> PublishAsync(
        IMqttClient client,
        string topic,
        IReadOnlyDictionary<string, object?> payload,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, DiscoveryJsonOptions);

        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload(json)
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();

        await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);

        return true;
    }

    /// <summary>
    /// Die JSON-Optionen der Discovery.
    /// </summary>
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
