using System.Text.Json;
using System.Text.Json.Serialization;
using ComfoBox.Domain.Derived;
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

    // Die Payloads der Baugruppen-Availability (Status/Hardware/…).
    private const string HardwareAvailablePayload = "1";
    private const string HardwareUnavailablePayload = "0";

    private const string CoolingActiveDerivedId = "derived.cooling_active";

    /// <summary>
    /// Die deutschen Anzeigenamen der abgeleiteten Werte.
    /// </summary>
    /// <remarks>
    /// Die Namen stehen hier und nicht in der Domäne: die Domäne beschreibt
    /// die Formel, die Discovery den Namen in Home Assistant.
    /// </remarks>
    private static readonly Dictionary<string, string> DerivedDisplayNames =
        new(StringComparer.Ordinal)
        {
            ["derived.superheat"] = "Überhitzung",
            ["derived.heating_circuit_delta"] = "Spreizung Heizkreis",
            ["derived.buffer_delta"] = "Puffer-Schichtung",
            ["derived.runtime_ratio"] = "Laufzeitverhältnis",
            ["derived.service_due"] = "Wartung fällig",
            [CoolingActiveDerivedId] = "Kühlung aktiv",
        };

    // Die Plattformen, zwischen denen ein Item wechseln kann. Wird der
    // Experten-Modus umgeschaltet, entsteht die Entität unter einem **anderen**
    // Discovery-Topic (number ↔ sensor) - das alte muss geräumt werden, sonst
    // behält Home Assistant die erste Fassung.
    private static readonly string[] WritableComponents = ["number", "select"];

    private readonly MqttTopicMap _topics;
    private readonly IItemCatalog _catalog;
    private readonly MqttOptions _options;
    private readonly GatewayRuntimeSettings _runtime;
    private readonly ILogger<HomeAssistantDiscovery> _logger;

    /// <summary>
    /// Erzeugt die Discovery.
    /// </summary>
    /// <param name="topics">Die Topic-Zuordnung.</param>
    /// <param name="catalog">Der Item-Katalog.</param>
    /// <param name="options">Die MQTT-Einstellungen.</param>
    /// <param name="runtime">Die Laufzeit-Einstellungen (Experten-Modus).</param>
    /// <param name="logger">Logger.</param>
    public HomeAssistantDiscovery(
        MqttTopicMap topics,
        IItemCatalog catalog,
        MqttOptions options,
        GatewayRuntimeSettings runtime,
        ILogger<HomeAssistantDiscovery> logger)
    {
        ArgumentNullException.ThrowIfNull(topics);
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(logger);

        _topics = topics;
        _catalog = catalog;
        _options = options;
        _runtime = runtime;
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

        // Der Experten-Modus wird **bei jedem Publish** gelesen - nicht beim
        // Start gebunden. Ein Umschalten wirkt damit sofort auf die Plattform
        // (sensor ↔ number/select) und auf das command_topic.
        var expertModeEnabled = _runtime.ExpertMode;

        var count = 0;

        // Zuerst die nicht mehr gültige Plattform räumen: ein Wechsel von
        // number auf sensor lässt das retained number-Topic sonst stehen -
        // Home Assistant behielte die erste Entität. Nur experten-pflichtige
        // Items können die Plattform überhaupt wechseln; bei allen anderen
        // wäre das Räumen eine leere Nachricht je Connect.
        foreach (var item in _catalog.All)
        {
            if (!item.IsWritable || !item.RequiresExpertMode)
            {
                continue;
            }

            var component = ComponentFor(item, item.IsWritableWithExpertMode(expertModeEnabled));
            await ClearStaleComponentsAsync(client, item, component, cancellationToken).ConfigureAwait(false);
        }

        foreach (var item in _catalog.All)
        {
            var writable = item.IsWritableWithExpertMode(expertModeEnabled);
            var payload = BuildItemPayload(item, writable);

            if (await PublishAsync(client, DiscoveryTopic(item, writable), payload, cancellationToken)
                .ConfigureAwait(false))
            {
                count++;
            }
        }

        foreach (var (topic, payload) in BuildDerivedPayloads())
        {
            if (await PublishAsync(client, topic, payload, cancellationToken).ConfigureAwait(false))
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

        // Die Gerätekarte entsteht aus den Entitäten: **jede** Payload trägt
        // den `device`-Block. Eine eigene Geräte-Discovery braucht Home
        // Assistant 2024.11+ mit `components` oder `child_devices`; ohne diese
        // Felder meldet die MQTT-Integration eine Warnung. Deshalb wird die
        // Altlast mit einer **leeren** Payload entfernt (retained löschen).
        await ClearAsync(client, DeviceTopic, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Home-Assistant-Discovery publiziert: {Count} Komponenten.", count);
    }

    /// <summary>
    /// Publiziert die Discovery auf allen Clients neu (Wechsel des Experten-Modus).
    /// </summary>
    /// <param name="clients">Die verbundenen Clients.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der das Neu-Publizieren abschließt.</returns>
    /// <remarks>
    /// <para>
    /// Die Discovery ist retained und idempotent: dieselbe Payload zweimal zu
    /// senden ist gefahrlos. Der Weg über die Clients ist trotzdem der einzige,
    /// der auch einen <c>number</c> → <c>sensor</c>-Wechsel abbildet - dafür muss
    /// das alte Topic geleert werden (siehe <see cref="OnConnectedAsync"/>).
    /// </para>
    /// <para>
    /// <c>Mqtt:EnableDiscovery=false</c> wird weiterhin respektiert: dann tut
    /// <see cref="OnConnectedAsync"/> nichts, und diese Methode auch nicht.
    /// </para>
    /// </remarks>
    public async Task RepublishAsync(IReadOnlyList<IMqttClient> clients, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(clients);

        if (!_options.EnableDiscovery)
        {
            return;
        }

        foreach (var client in clients)
        {
            await OnConnectedAsync(client, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Das Discovery-Topic des Geräts.
    /// </summary>
    /// <remarks>
    /// Das Topic wird **geleert**, nicht beschrieben: die Entitäten bringen
    /// den <c>device</c>-Block selbst mit, eine eigene Geräte-Discovery wäre
    /// ein Duplikat. Ältere Gateway-Versionen haben hier eine Payload ohne
    /// <c>components</c>/<c>child_devices</c> hinterlassen - die MQTT-Integration
    /// beanstandet das. Eine leere retained Payload entfernt sie.
    /// </remarks>
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
    /// <remarks>
    /// Die Id wird über <see cref="DiscoveryId.Slug"/> **ASCII-tauglich**
    /// gemacht: Umlaute in Item-Ids würden sonst im Discovery-Topic landen -
    /// dort sind sie unzulässig. Der Topic-Vertrag (State-Topics) bleibt
    /// unverändert.
    /// </remarks>
    public string UniqueId(ItemDefinition item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return $"{DeviceIdentifier}_{DiscoveryId.Slug(item.Id)}";
    }

    /// <summary>
    /// Das Discovery-Topic eines Items.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="writable">Ist das Item schreibbar?</param>
    /// <returns>Das Topic, z. B. <c>homeassistant/number/comfobox_zone_set_point_normal/config</c>.</returns>
    public string DiscoveryTopic(ItemDefinition item, bool writable)
        => DiscoveryTopicFor(item, ComponentFor(item, writable));

    /// <summary>
    /// Das Discovery-Topic eines Items für eine bestimmte Plattform.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <param name="component">Die Plattform, z. B. <c>sensor</c>.</param>
    /// <returns>Das Topic, z. B. <c>homeassistant/sensor/comfobox_.../config</c>.</returns>
    private string DiscoveryTopicFor(ItemDefinition item, string component)
        => $"{_options.DiscoveryPrefix}/{component}/{UniqueId(item)}/config";

    /// <summary>
    /// Entfernt die Discovery-Topics, die für dieses Item nicht mehr gelten.
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <param name="item">Das Item.</param>
    /// <param name="activeComponent">Die Plattform, die jetzt publiziert wird.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der das Räumen abschließt.</returns>
    /// <remarks>
    /// Betroffen sind nur schreibbare Items: bei Nur-Lese-Items ist
    /// <c>sensor</c> die einzige Plattform, ein Wechsel ist dort unmöglich.
    /// </remarks>
    private async Task ClearStaleComponentsAsync(
        IMqttClient client,
        ItemDefinition item,
        string activeComponent,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(activeComponent, "sensor", StringComparison.Ordinal))
        {
            await ClearAsync(client, DiscoveryTopicFor(item, "sensor"), cancellationToken).ConfigureAwait(false);
        }

        foreach (var component in WritableComponents)
        {
            if (string.Equals(component, activeComponent, StringComparison.Ordinal))
            {
                continue;
            }

            await ClearAsync(client, DiscoveryTopicFor(item, component), cancellationToken).ConfigureAwait(false);
        }
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

        // Items einer Baugruppe (Pufferspeicher, Solarkreis, …) hängen an der
        // Verfügbarkeit dieser Baugruppe, nicht am Gateway-Health: fehlt die
        // Baugruppe, liefert der Regler nur den Sentinel 32767.
        var requirement = HardwareRequirements.ForItem(item.Id);

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["name"] = MqttValueFormatter.DisplayName(item),
            ["unique_id"] = UniqueId(item),
            ["state_topic"] = _topics.ItemTopic(item),
            ["availability_topic"] = requirement is null
                ? _topics.HealthTopic
                : _topics.HardwareTopic(requirement),
            ["payload_available"] = requirement is null
                ? "online"
                : HardwareAvailablePayload,
            ["payload_not_available"] = requirement is null
                ? "offline"
                : HardwareUnavailablePayload,
            ["device"] = BuildDevice(),
            ["origin"] = BuildOrigin(),
        };

        // Neue (V2-)Config-Items sind Experten-Werkzeug: Sie liegen in der
        // Config-Kategorie und sind standardmässig aus. Das gilt unabhängig
        // vom Experten-Modus - der entscheidet nur über number/select.
        if (item.IsV2 && item.IsWritable && item.Group.StartsWith("Config/", StringComparison.Ordinal))
        {
            payload["entity_category"] = "config";
            payload["entity_registry_enabled_default"] = false;
        }
        else if (IsDiagnosticGroup(item.Group))
        {
            payload["entity_category"] = "diagnostic";
        }

        // P3 wird nur stündlich gelesen (Zähler, langsame Fühler) - solche
        // Entitäten sollen Home Assistant nicht mit Recorder-Einträgen füllen.
        if (!writable && item.Priority == RefreshPriority.Slow)
        {
            payload["entity_registry_enabled_default"] = false;
        }

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

        // Die Geräteklasse hängt an der Einheit (°C, kWh, kW).
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
                payload["state_class"] = StateClassFor(item);
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
    /// Die Discovery-Payloads der sieben abgeleiteten Werte.
    /// </summary>
    /// <returns>Topic → Payload.</returns>
    /// <remarks>
    /// <para>
    /// Die abgeleiteten Werte sind keine Katalog-Items (kein BACnet-Objekt):
    /// sie werden vom Gateway gerechnet und unter
    /// <c>{BaseTopic}/Status/Derived/…</c> publiziert. Sie erscheinen deshalb
    /// als eigene Entitäten - bis auf <c>CoolingActive</c> als
    /// <c>binary_sensor</c>, weil der Wert <c>1</c>/<c>0</c> ein Zustand ist.
    /// </para>
    /// <para>
    /// Alle sieben sind Diagnose (<c>entity_category: diagnostic</c>) und
    /// hängen an der Health-Availability: ohne Gateway-Verbindung ist keiner
    /// der Werte belegbar.
    /// </para>
    /// </remarks>
    public IReadOnlyList<KeyValuePair<string, IReadOnlyDictionary<string, object?>>> BuildDerivedPayloads()
    {
        var result = new List<KeyValuePair<string, IReadOnlyDictionary<string, object?>>>(
            DerivedValues.All.Count);

        foreach (var definition in DerivedValues.All)
        {
            var binary = string.Equals(definition.Id, CoolingActiveDerivedId, StringComparison.Ordinal);
            var component = binary ? "binary_sensor" : "sensor";

            // Discovery-Kennungen sind ASCII-only; die Ids enthalten Punkte.
            // Nur das **letzte** Segment wird verwendet: die Id lautet bereits
            // "derived.superheat", der Präfix steht schon in der Kennung -
            // sonst entsteht "comfobox_derived_derived_superheat".
            var uniqueId = $"{DeviceIdentifier}_derived_{DiscoveryId.Slug(LastSegment(definition.Id))}";

            var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = DerivedDisplayNames.TryGetValue(definition.Id, out var displayName)
                    ? displayName
                    : definition.TopicPath,
                ["unique_id"] = uniqueId,
                ["state_topic"] = $"{_topics.BaseTopic}/Status/{definition.TopicPath}",
                ["availability_topic"] = _topics.HealthTopic,
                ["payload_available"] = "online",
                ["payload_not_available"] = "offline",
                ["entity_category"] = "diagnostic",
                ["device"] = BuildDevice(),
                ["origin"] = BuildOrigin(),
            };

            if (!string.IsNullOrEmpty(definition.Unit))
            {
                payload["unit_of_measurement"] = definition.Unit;
            }

            if (binary)
            {
                // Der Dienst publiziert 1/0 - nicht die HA-Vorgabe ON/OFF.
                payload["payload_on"] = "1";
                payload["payload_off"] = "0";
            }
            else if (string.Equals(definition.Unit, "%", StringComparison.Ordinal))
            {
                payload["state_class"] = "measurement";
            }

            result.Add(new KeyValuePair<string, IReadOnlyDictionary<string, object?>>(
                $"{_options.DiscoveryPrefix}/{component}/{uniqueId}/config",
                payload));
        }

        return result;
    }

    /// <summary>
    /// Das letzte Segment einer punktgetrennten Id.
    /// </summary>
    /// <param name="id">Die Id, z. B. <c>derived.superheat</c>.</param>
    /// <returns>Das letzte Segment, z. B. <c>superheat</c>.</returns>
    private static string LastSegment(string id)
    {
        var index = id.LastIndexOf('.');
        return index < 0 ? id : id[(index + 1)..];
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
    /// <remarks>
    /// Für <c>K</c> (Differenz), <c>%</c>, <c>m³/h</c> und <c>h</c> gibt es
    /// keine passende Home-Assistant-Geräteklasse - dort bleibt es bei der
    /// reinen Einheit.
    /// </remarks>
    private static string? DeviceClassFor(string? unit)
        => unit switch
        {
            "°C" => "temperature",
            "kWh" => "energy",
            "kW" => "power",
            _ => null,
        };

    /// <summary>
    /// Die <c>state_class</c> eines read-only Analogwerts.
    /// </summary>
    /// <param name="item">Das Item.</param>
    /// <returns><c>total_increasing</c> für kWh-Zähler, sonst <c>measurement</c>.</returns>
    private static string StateClassFor(ItemDefinition item)
        => string.Equals(item.Unit, "kWh", StringComparison.Ordinal)
            ? "total_increasing"
            : "measurement";

    /// <summary>
    /// Gehört die Gruppe in die Diagnose-Kategorie?
    /// </summary>
    /// <param name="group">Die Topic-Gruppe.</param>
    /// <returns><c>true</c> für Zähler und abgeleitete Werte.</returns>
    private static bool IsDiagnosticGroup(string group)
        => string.Equals(group, "Status/Counters", StringComparison.Ordinal)
            || group.StartsWith("Status/Counters/", StringComparison.Ordinal)
            || string.Equals(group, "Status/Derived", StringComparison.Ordinal);

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
    /// Entfernt ein retained Topic (leere Payload).
    /// </summary>
    /// <param name="client">Der Client.</param>
    /// <param name="topic">Das Topic.</param>
    /// <param name="cancellationToken">Abbruchsignal.</param>
    /// <returns>Ein Task, der den Publish abschließt.</returns>
    private static async Task ClearAsync(IMqttClient client, string topic, CancellationToken cancellationToken)
    {
        var message = new MqttApplicationMessageBuilder()
            .WithTopic(topic)
            .WithPayload([])
            .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)
            .WithRetainFlag(true)
            .Build();

        await client.PublishAsync(message, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Die JSON-Optionen der Discovery.
    /// </summary>
    private static readonly JsonSerializerOptions DiscoveryJsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };
}
